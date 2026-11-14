using System.Collections.Concurrent;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Messenger.LoadTest;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

// dotnet run -c Release -- seed --connection "<postgres>" --users 10000
// dotnet run -c Release -- run --api http://127.0.0.1:18080 --secret <Jwt:SecretKey> --users 10000 --rate 100 --duration 300
var command = args.FirstOrDefault();
var options = ParseOptions(args.Skip(1).ToArray());

return command switch
{
    "seed" => await SeedAsync(options),
    "run" => await RunAsync(options),
    _ => Usage(),
};

static int Usage()
{
    Console.WriteLine("""
        seed --connection <postgres connection string> --users <n>
        run  --api <base url> --secret <Jwt:SecretKey> --users <n> [--rate <messages/s>=20] [--duration <s>=120]
             [--ramp <connections/s>=200] [--warmup <s>=10] [--channel-share <0..1>=0.01]
             [--large-size <200|500|1000>] [--large-share <0..1>]
             [--channel-viewers <0..1>=0.2] [--out <dir>=results]
        """);
    return 1;
}

static async Task<int> SeedAsync(Dictionary<string, string> options)
{
    var users = int.Parse(options["users"]);

    if (users < 100 || users % 50 != 0)
        throw new ArgumentException("--users must be a multiple of 50, at least 100.");

    await using var connection = new NpgsqlConnection(options["connection"]);
    await connection.OpenAsync();

    await using (var check = new NpgsqlCommand("""SELECT count(*) FROM "Users" WHERE "UserName" LIKE 'load-%'""", connection))
    {
        if ((long)(await check.ExecuteScalarAsync())! > 0)
            throw new InvalidOperationException("Load-test users already exist; seed an empty (freshly migrated) database.");
    }

    var started = Stopwatch.StartNew();
    await using var seed = new NpgsqlCommand(LoadData.SeedSql(users), connection) { CommandTimeout = 600 };
    await seed.ExecuteNonQueryAsync();

    Console.WriteLine($"Seeded {users} users, {users / 2} private chats, {users / 10} teams of 10, {users / 50} teams of 50 " +
                      $"and a channel of {users} in {started.Elapsed.TotalSeconds:0.0} s.");
    return 0;
}

static async Task<int> RunAsync(Dictionary<string, string> options)
{
    var api = new Uri(options["api"].TrimEnd('/') + "/");
    var users = int.Parse(options["users"]);
    var rate = double.Parse(options.GetValueOrDefault("rate", "20"));
    var duration = TimeSpan.FromSeconds(double.Parse(options.GetValueOrDefault("duration", "120")));
    var rampPerSecond = int.Parse(options.GetValueOrDefault("ramp", "200"));
    var warmup = TimeSpan.FromSeconds(double.Parse(options.GetValueOrDefault("warmup", "10")));
    var channelViewerShare = double.Parse(options.GetValueOrDefault("channel-viewers", "0.2"));
    var channelShare = double.Parse(options.GetValueOrDefault("channel-share", "0.01"));
    var largeSize = int.Parse(options.GetValueOrDefault("large-size", "0"));
    var largeShare = double.Parse(options.GetValueOrDefault("large-share", "0"));

    if (largeSize != 0 && !LoadData.LargeGroupSizes.Contains(largeSize))
        throw new ArgumentException($"--large-size must be one of {string.Join(", ", LoadData.LargeGroupSizes)}.");
    var outDir = options.GetValueOrDefault("out", "results");
    var tokens = Enumerable.Range(0, users).Select(i => CreateToken(i, options["secret"])).ToArray();

    var metrics = new RunMetrics();
    var channelViewers = new ConcurrentDictionary<int, bool>();
    var connections = new HubConnection?[users];

    // 1. Connect every user, at most rampPerSecond new connections per second (a morning login wave).
    Console.WriteLine($"Connecting {users} users at {rampPerSecond}/s ...");
    var connectWatch = Stopwatch.StartNew();
    var connectTasks = new List<Task>();

    for (var i = 0; i < users; i++)
    {
        var user = i;
        connectTasks.Add(ConnectAsync(user));

        if ((i + 1) % rampPerSecond == 0)
            await Task.Delay(1000);
    }

    await Task.WhenAll(connectTasks);
    Console.WriteLine($"Connected {metrics.Connected}/{users} in {connectWatch.Elapsed.TotalSeconds:0.0} s " +
                      $"(failed {metrics.ConnectFailures}); {channelViewers.Count} have the channel open.");

    await Task.Delay(warmup);

    // 2. Send at the requested rate for the duration: the channel gets --channel-share of the messages, the rest
    //    goes 70% to private chats, 20% to teams of 10 and 10% to teams of 50.
    Console.WriteLine($"Sending {rate} messages/s for {duration.TotalSeconds:0} s ...");
    using var http = new HttpClient(new SocketsHttpHandler { MaxConnectionsPerServer = 2_000 }) { BaseAddress = api };
    var inFlight = new SemaphoreSlim(2_000);
    var sends = new List<Task>();
    var random = new Random(42);
    var sendWatch = Stopwatch.StartNew();
    double due = 0;

    while (sendWatch.Elapsed < duration)
    {
        await Task.Delay(50);
        due += rate * 0.05;

        for (; due >= 1; due--)
        {
            var roll = random.NextDouble();
            var special = random.NextDouble();
            var (kind, sender, conversationId) = special < channelShare
                ? Pick("channel", random.Next(Math.Min(LoadData.ChannelAdmins, users)), _ => LoadData.Channel())
                : special < channelShare + largeShare && largeSize > 0
                ? Pick($"group{largeSize}", random.Next(users / largeSize * largeSize), u => LoadData.LargeGroup(largeSize, u))
                : roll switch
                {
                    < 0.70 => Pick("private", random.Next(users), LoadData.PrivateConversation),
                    < 0.90 => Pick("team10", random.Next(users / 10 * 10), LoadData.Team10),
                    _ => Pick("team50", random.Next(users / 50 * 50), LoadData.Team50),
                };

            await inFlight.WaitAsync();
            sends.Add(SendAsync(kind, sender, conversationId).ContinueWith(_ => inFlight.Release()));
        }

        if (sendWatch.Elapsed.TotalSeconds % 10 < 0.05)
            Console.WriteLine($"  {sendWatch.Elapsed.TotalSeconds:0} s: sent {metrics.Sent.Count}, received {metrics.ReceivedTotal}");
    }

    await Task.WhenAll(sends);
    Console.WriteLine("Waiting 10 s for the last deliveries ...");
    await Task.Delay(TimeSpan.FromSeconds(10));

    // 3. Report.
    var expected = metrics.Expected;
    var report = new
    {
        StartedAtUtc = DateTime.UtcNow - sendWatch.Elapsed,
        Users = users,
        RateMessagesPerSecond = rate,
        DurationSeconds = duration.TotalSeconds,
        ChannelShare = channelShare,
        LargeGroupSize = largeSize,
        LargeGroupShare = largeShare,
        ChannelViewers = channelViewers.Count,
        metrics.Connected,
        metrics.ConnectFailures,
        metrics.Disconnects,
        ConnectMs = metrics.Connect.Summary(),
        SendHttpMs = metrics.Sent.Summary(),
        SendErrors = metrics.SendErrors.ToDictionary(x => x.Key, x => x.Value),
        // From the sender starting the request to a receiver getting the event: what users experience.
        SendToReceiveMs = metrics.Received.ToDictionary(x => x.Key, x => x.Value.Summary()),
        // From the message's createdAtUtc (set after the counter locks are taken) to a receiver getting it.
        CreatedToReceiveMs = metrics.CreatedToReceive.ToDictionary(x => x.Key, x => x.Value.Summary()),
        ReceivedTotal = metrics.ReceivedTotal,
        ExpectedReceives = expected,
        DeliveredShare = expected == 0 ? 0 : Math.Round((double)metrics.ReceivedTotal / expected, 4),
    };

    var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
    Console.WriteLine(json);
    Directory.CreateDirectory(outDir);
    var file = Path.Combine(outDir, $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{users}u-{rate}mps.json");
    await File.WriteAllTextAsync(file, json);
    Console.WriteLine($"Saved {file}");

    await Task.WhenAll(connections.Where(x => x is not null).Select(x => x!.DisposeAsync().AsTask()));
    return 0;

    (string Kind, int Sender, Guid ConversationId) Pick(string kind, int sender, Func<int, Guid> conversation) =>
        (kind, sender, conversation(sender));

    async Task ConnectAsync(int user)
    {
        var started = Stopwatch.GetTimestamp();
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(api, "hubs/chat"), o =>
            {
                o.Transports = HttpTransportType.WebSockets;
                o.SkipNegotiation = true;
                o.AccessTokenProvider = () => Task.FromResult<string?>(tokens[user]);
            })
            .Build();

        var self = LoadData.UserId(user);

        connection.On<JsonElement>("message.created", message =>
        {
            if (message.GetProperty("senderUserId").GetGuid() == self)
                return;

            var fromCreated = (DateTime.UtcNow - message.GetProperty("createdAtUtc").GetDateTime().ToUniversalTime()).TotalMilliseconds;
            var parts = message.GetProperty("body").GetString()?.Split('|');
            var kind = parts is ["lt", var k, ..] ? k : "other";
            metrics.RecordCreatedToReceive(kind, fromCreated);

            // End to end: from the sender starting the request (same process, same clock).
            if (parts is ["lt", _, var token] && metrics.SendStarted.TryGetValue(token, out var sentAt))
                metrics.RecordReceive(kind, Stopwatch.GetElapsedTime(sentAt).TotalMilliseconds);
        });

        connection.Closed += _ =>
        {
            Interlocked.Increment(ref metrics.Disconnects);
            return Task.CompletedTask;
        };

        try
        {
            await connection.StartAsync();
            await connection.InvokeAsync("RegisterUser");

            // The chats this user has open: their private chat and team of 10; some also have the channel open
            // (the channel has more than 500 members, so only viewers get its live events).
            await connection.InvokeAsync("JoinConversation", LoadData.PrivateConversation(user).ToString());
            await connection.InvokeAsync("JoinConversation", LoadData.Team10(user).ToString());

            // Everyone in the measured large group has it open, so every member counts as an expected receiver
            // (above 500 members only viewers get live events).
            if (largeSize > 0 && user < users / largeSize * largeSize)
                await connection.InvokeAsync("JoinConversation", LoadData.LargeGroup(largeSize, user).ToString());

            if (Random.Shared.NextDouble() < channelViewerShare)
            {
                await connection.InvokeAsync("JoinConversation", LoadData.Channel().ToString());
                channelViewers[user] = true;
            }

            connections[user] = connection;
            metrics.Connect.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            Interlocked.Increment(ref metrics.Connected);
        }
        catch (Exception exception)
        {
            if (Interlocked.Increment(ref metrics.ConnectFailures) <= 5)
                Console.WriteLine($"  connect failed for user {user}: {exception.GetBaseException().Message}");

            await connection.DisposeAsync();
        }
    }

    // Members other than the sender that are connected now (and, for the channel, have it open).
    int ConnectedReceivers(string kind, int sender)
    {
        bool Connected(int user) => user != sender && user < users && connections[user] is not null;

        return kind switch
        {
            "private" => Connected(sender ^ 1) ? 1 : 0,
            "team10" => Enumerable.Range(sender / 10 * 10, 10).Count(Connected),
            "team50" => Enumerable.Range(sender / 50 * 50, 50).Count(Connected),
            _ when kind == $"group{largeSize}" => Enumerable.Range(sender / largeSize * largeSize, largeSize).Count(Connected),
            _ => channelViewers.Keys.Count(Connected),
        };
    }

    async Task SendAsync(string kind, int sender, Guid conversationId)
    {
        var token = Guid.NewGuid().ToString("N");
        metrics.SendStarted[token] = Stopwatch.GetTimestamp();

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/messages")
        {
            Content = JsonContent.Create(new
            {
                conversationId,
                clientMessageId = Guid.NewGuid(),
                body = $"lt|{kind}|{token}",
            }),
        };
        request.Headers.Authorization = new("Bearer", tokens[sender]);

        var started = Stopwatch.GetTimestamp();

        try
        {
            using var response = await http.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                metrics.Sent.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
                metrics.AddExpected(ConnectedReceivers(kind, sender));
            }
            else
            {
                metrics.SendErrors.AddOrUpdate(((int)response.StatusCode).ToString(), 1, (_, n) => n + 1);
            }
        }
        catch (Exception exception)
        {
            metrics.SendErrors.AddOrUpdate(exception.GetType().Name, 1, (_, n) => n + 1);
        }
    }
}

// Same token as JwtTokenService issues for a signed-in user.
static string CreateToken(int user, string secret)
{
    var userId = LoadData.UserId(user).ToString();
    var claims = new List<Claim>
    {
        new(JwtRegisteredClaimNames.Sub, userId),
        new(ClaimTypes.NameIdentifier, userId),
        new(ClaimTypes.Name, LoadData.UserName(user)),
        new(ClaimTypes.Role, "User"),
        new("display_name", $"Load User {user}"),
    };

    var credentials = new SigningCredentials(
        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
        SecurityAlgorithms.HmacSha256);

    return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
        "EnterpriseMessenger",
        "EnterpriseMessengerClients",
        claims,
        expires: DateTime.UtcNow.AddHours(8),
        signingCredentials: credentials));
}

static Dictionary<string, string> ParseOptions(string[] args)
{
    var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    for (var i = 0; i + 1 < args.Length; i += 2)
        options[args[i].TrimStart('-')] = args[i + 1];

    return options;
}

sealed class RunMetrics
{
    public readonly LatencyHistogram Connect = new();
    public readonly LatencyHistogram Sent = new();
    public readonly ConcurrentDictionary<string, LatencyHistogram> Received = new();
    public readonly ConcurrentDictionary<string, LatencyHistogram> CreatedToReceive = new();
    public readonly ConcurrentDictionary<string, long> SendStarted = new();
    public readonly ConcurrentDictionary<string, long> SendErrors = new();
    private long _expected;
    public int Connected;
    public int ConnectFailures;
    public int Disconnects;

    public long ReceivedTotal => CreatedToReceive.Values.Sum(x => x.Count);

    public void RecordReceive(string kind, double milliseconds) =>
        Received.GetOrAdd(kind, _ => new LatencyHistogram()).Record(milliseconds);

    public void RecordCreatedToReceive(string kind, double milliseconds) =>
        CreatedToReceive.GetOrAdd(kind, _ => new LatencyHistogram()).Record(milliseconds);

    public long Expected => Interlocked.Read(ref _expected);

    public void AddExpected(int receivers) => Interlocked.Add(ref _expected, receivers);
}
