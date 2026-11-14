using System.Security.Cryptography;
using System.Text;

namespace Messenger.LoadTest;

/// <summary>
/// The synthetic organisation. Ids are md5 of a name, so the SQL seed (<c>md5('...')::uuid</c>) and this program
/// compute the same ids without sharing any state.
/// <list type="bullet">
/// <item>user i: <c>load-user-{i}</c>, user name <c>load-{i}</c></item>
/// <item>private conversation k: users 2k and 2k+1</item>
/// <item>team of 10 b: users 10b … 10b+9; team of 50 c: users 50c … 50c+49</item>
/// <item>large groups of <see cref="LargeGroupSizes"/> members, in blocks the same way (e.g. users 1000d … 1000d+999)</item>
/// <item>one company channel with every user; the first <see cref="ChannelAdmins"/> users post in it</item>
/// </list>
/// </summary>
public static class LoadData
{
    public const int ChannelAdmins = 50;

    public static Guid UserId(int i) => FromName($"load-user-{i}");

    public static string UserName(int i) => $"load-{i}";

    public static Guid PrivateConversation(int user) => FromName($"load-private-{user / 2}");

    public static Guid Team10(int user) => FromName($"load-group10-{user / 10}");

    public static Guid Team50(int user) => FromName($"load-group50-{user / 50}");

    public static Guid Channel() => FromName("load-channel");

    /// <summary>Sizes of the large groups, to measure how the per-member cost grows (hybrid sync threshold).</summary>
    public static readonly int[] LargeGroupSizes = [200, 500, 1000];

    public static Guid LargeGroup(int size, int user) => FromName($"load-group{size}-{user / size}");

    // Same as PostgreSQL's md5(name)::uuid: the 32 hex digits in order.
    private static Guid FromName(string name) =>
        Guid.ParseExact(Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(name))), "N");

    /// <summary>SQL that creates <paramref name="users"/> users and their conversations in an empty database.</summary>
    public static string SeedSql(int users)
    {
        var pairs = users / 2;
        var teams10 = users / 10;
        var teams50 = users / 50;

        var largeConversations = new StringBuilder();
        var largeMembers = new StringBuilder();

        foreach (var size in LargeGroupSizes)
        {
            largeConversations.AppendLine($"""
                UNION ALL
                SELECT md5('load-group{size}-' || g)::uuid, 2, 'Group of {size} #' || g, md5('load-user-' || ({size} * g))::uuid, now()
                FROM generate_series(0, {users / size - 1}) AS g
                """);

            largeMembers.AppendLine($"""
                UNION ALL
                SELECT md5('load-group{size}-' || (i / {size}))::uuid, i, 1 FROM generate_series(0, {users / size * size - 1}) AS i
                """);
        }

        return $"""
            INSERT INTO "Users" ("Id", "DisplayName", "UserName", "IsActive", "CreatedAtUtc", "IsAdmin")
            SELECT md5('load-user-' || i)::uuid, 'Load User ' || i, 'load-' || i, true, now(), false
            FROM generate_series(0, {users - 1}) AS i;

            INSERT INTO "Conversations" ("Id", "Type", "Title", "CreatedByUserId", "CreatedAtUtc")
            SELECT md5('load-private-' || k)::uuid, 1, NULL, md5('load-user-' || (2 * k))::uuid, now()
            FROM generate_series(0, {pairs - 1}) AS k
            UNION ALL
            SELECT md5('load-group10-' || b)::uuid, 2, 'Team of 10 #' || b, md5('load-user-' || (10 * b))::uuid, now()
            FROM generate_series(0, {teams10 - 1}) AS b
            UNION ALL
            SELECT md5('load-group50-' || c)::uuid, 2, 'Team of 50 #' || c, md5('load-user-' || (50 * c))::uuid, now()
            FROM generate_series(0, {teams50 - 1}) AS c
            UNION ALL
            SELECT md5('load-channel')::uuid, 3, 'Company', md5('load-user-0')::uuid, now()
            {largeConversations};

            INSERT INTO "ConversationMembers" ("Id", "ConversationId", "UserId", "LastReadSequenceNumber",
                "LastDeliveredSequenceNumber", "CreatedAtUtc", "JoinedAtUtc", "IsMuted", "IsPinned", "IsArchived", "Role")
            SELECT gen_random_uuid(), conversation_id, md5('load-user-' || i)::uuid, 0, 0, now(), now(),
                false, false, false, role
            FROM (
                SELECT md5('load-private-' || (i / 2))::uuid AS conversation_id, i, 1 AS role
                FROM generate_series(0, {pairs * 2 - 1}) AS i
                UNION ALL
                SELECT md5('load-group10-' || (i / 10))::uuid, i, 1 FROM generate_series(0, {teams10 * 10 - 1}) AS i
                UNION ALL
                SELECT md5('load-group50-' || (i / 50))::uuid, i, 1 FROM generate_series(0, {teams50 * 50 - 1}) AS i
                UNION ALL
                SELECT md5('load-channel')::uuid, i, CASE WHEN i < {ChannelAdmins} THEN 2 ELSE 1 END
                FROM generate_series(0, {users - 1}) AS i
                {largeMembers}
            ) AS members;

            INSERT INTO "ConversationCounters" ("Id", "ConversationId", "LastSequenceNumber", "CreatedAtUtc")
            SELECT gen_random_uuid(), "Id", 0, now() FROM "Conversations";
            """;
    }
}
