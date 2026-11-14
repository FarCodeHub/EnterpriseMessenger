using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Messenger.Application.Messages.SendMessage;
using Messenger.Infrastructure.Messages;
using Messenger.Application.Messages.EditMessage;
using Messenger.Application.Messages.DeleteMessage;
using Messenger.Application.Sync;
using Messenger.Infrastructure.Sync;
using Messenger.Application.Conversations.CreatePrivateConversation;
using Messenger.Infrastructure.Conversations;
using Messenger.Application.Conversations.CreateGroupConversation;
using Messenger.Application.Conversations.GetUserConversations;
using Messenger.Application.Messages.GetMessages;
using Messenger.Application.Messages.SearchMessages;
using Messenger.Application.Conversations.MarkConversationRead;
using Messenger.Application.Users.CreateUser;
using Messenger.Application.Users.RegisterDevice;
using Messenger.Infrastructure.Users;
using Messenger.Application.Sync.GetSyncEvents;
using Messenger.Application.Sync.CommitSync;
using Messenger.Application.Files.UploadFile;
using Messenger.Application.Files.DownloadFile;
using Messenger.Infrastructure.Files;
using StackExchange.Redis;
using MassTransit;
using Messenger.Infrastructure.Consumers;
using Messenger.Application.Conversations.AddMember;
using Messenger.Application.Realtime;
using Messenger.Infrastructure.Realtime;
using Messenger.Application.Conversations.RemoveMember;
using Messenger.Application.Conversations.SetMemberRole;
using Messenger.Application.Conversations.LeaveConversation;
using Messenger.Application.Conversations.UpdateConversationMemberSettings;
using Messenger.Application.Conversations.GetMembers;
using Messenger.Application.Auth.Login;
using Messenger.Application.Auth.Refresh;
using Messenger.Infrastructure.Auth;
using Messenger.Application.Auth.RevokeSession;
using Messenger.Application.Auth.GetSessions;
using Messenger.Application.Auditing;
using Messenger.Infrastructure.Auditing;
using Messenger.Application.Users.DeactivateUser;
using Messenger.Application.Users.ActivateUser;
using Messenger.Application.Users.MakeAdmin;
using Messenger.Application.Outbox;
using Messenger.Infrastructure.Outbox;
using Minio;
using Messenger.Application.Files.GetDownloadUrl;
using Messenger.Application.Conversations;
using Messenger.Application.Users;
using Messenger.Application.Abstractions.Realtime;
using Microsoft.Extensions.Hosting;
using Messenger.Application.Search;
using Messenger.Infrastructure.Search;
using OpenSearch.Client;
using Messenger.Application.Messages.ForwardMessage;
 

namespace Messenger.Infrastructure;


public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
       this IServiceCollection services,
       IConfiguration configuration,
       IHostEnvironment environment)
    {
        var connectionString = configuration.GetConnectionString("MessengerDb");

        if (!environment.IsEnvironment("Testing"))
        {
            services.AddDbContext<MessengerDbContext>(options =>
            {
                options.UseNpgsql(configuration.GetConnectionString("MessengerDb"));
            });
        }

        services.AddScoped<MessageCreator>();
        services.AddScoped<IMessageSender, MessageSender>();
        services.AddScoped<IMessageEditor, MessageEditor>();
        services.AddScoped<IMessageDeleter, MessageDeleter>();
        services.AddScoped<IUserSyncEventWriter, UserSyncEventWriter>();
        services.AddScoped<IPrivateConversationCreator, PrivateConversationCreator>();
        services.AddScoped<IGroupConversationCreator, GroupConversationCreator>();
        services.AddScoped<IUserConversationReader, UserConversationReader>();
        services.AddScoped<IMessageReader, MessageReader>();
        services.AddScoped<IMessageSearchService, PostgresMessageSearchService>();
        services.AddScoped<IConversationReadMarker, ConversationReadMarker>();
        services.AddScoped<IUserCreator, UserCreator>();
        services.AddScoped<IUserDeviceRegistrar, UserDeviceRegistrar>();
        services.AddScoped<ISyncEventReader, SyncEventReader>();
        services.AddScoped<ISyncCommitter, SyncCommitter>();
        services.AddScoped<IAttachmentDownloader, AttachmentDownloader>();

        var isTesting = environment.IsEnvironment("Testing");

        var redisConnectionString = configuration["Redis:ConnectionString"];

        if (!isTesting && !string.IsNullOrWhiteSpace(redisConnectionString))
        {
            services.AddSingleton<IConnectionMultiplexer>(
                _ => ConnectionMultiplexer.Connect(redisConnectionString));

            services.AddSingleton<IPresenceService, RedisPresenceService>();
        }

        var openSearchUrl = configuration["OpenSearch:Url"];
        var searchEnabled = !string.IsNullOrWhiteSpace(openSearchUrl);

        services.AddMassTransit(options =>
        {
            options.UsingRabbitMq((context, cfg) =>
            {
                var host = configuration["RabbitMq:Host"] ?? "localhost";
                var username = configuration["RabbitMq:Username"] ?? "guest";
                var password = configuration["RabbitMq:Password"] ?? "guest";
                var port = configuration.GetValue<ushort?>("RabbitMq:Port") ?? 5672;

                cfg.Host(host, port, "/", h =>
                {
                    h.Username(username);
                    h.Password(password);
                });

                // Transient failures (database, search cluster) are retried before a message is faulted.
                cfg.UseMessageRetry(retry => retry.Intervals(
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromSeconds(5),
                    TimeSpan.FromSeconds(15)));

                cfg.ConfigureEndpoints(context);
            });


            options.AddConsumer<MessageCreatedIntegrationEventConsumer>();
            options.AddConsumer<NotificationRequestedIntegrationEventConsumer>();

            if (searchEnabled)
                options.AddConsumer<MessageSearchIndexingConsumer>();
        });
        services.AddSingleton<IConnectionTracker, RedisConnectionTracker>();
        services.AddScoped<IConversationMemberAdder, ConversationMemberAdder>();
        services.AddScoped<IConversationMemberRemover, ConversationMemberRemover>();
        services.AddScoped<IConversationMemberRoleSetter, ConversationMemberRoleSetter>();
        services.AddScoped<IConversationLeaver, ConversationLeaver>();
        services.AddScoped<IConversationMemberSettingsUpdater, ConversationMemberSettingsUpdater>();
        services.AddScoped<IConversationMemberReader, ConversationMemberReader>();
        services.AddScoped<IUserLoginService, UserLoginService>();
        services.AddScoped<Messenger.Application.Auth.External.IExternalUserProvisioner, ExternalUserProvisioner>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<ISessionRevoker, SessionRevoker>();
        services.AddScoped<IUserSessionReader, UserSessionReader>();
        services.AddScoped<IAuditLogWriter, AuditLogWriter>();
        services.AddScoped<IUserDeactivator, UserDeactivator>();
        services.AddScoped<IUserActivator, UserActivator>();
        services.AddScoped<IUserAdminMaker, UserAdminMaker>();
        services.AddScoped<IOutboxWriter, OutboxWriter>();
        var minioEndpoint = configuration["Minio:Endpoint"] ?? "localhost:9000";
        var minioAccessKey = configuration["Minio:AccessKey"] ?? "minioadmin";
        var minioSecretKey = configuration["Minio:SecretKey"] ?? "minioadmin";
        var minioUseSsl = bool.TryParse(configuration["Minio:UseSsl"], out var useSsl) && useSsl;

        services.AddSingleton<IMinioClient>(_ =>
        {
            var builder = new MinioClient()
                .WithEndpoint(minioEndpoint)
                .WithCredentials(minioAccessKey, minioSecretKey);

            if (minioUseSsl)
                builder = builder.WithSSL();

            return builder.Build();
        });

        services.AddScoped<IFileUploader, MinioFileUploader>();
        services.AddScoped<IAttachmentDownloadUrlGenerator, MinioAttachmentDownloadUrlGenerator>();
        services.AddScoped<IConversationPermissionChecker, ConversationPermissionChecker>();
        services.AddScoped<IActiveMemberReader, ActiveMemberReader>();
        services.AddScoped<IUserReader, UserReader>();
        services.AddScoped<AdminUserSeeder>();

        services.AddScoped<ITypingService, TypingService>();
        services.AddScoped<IReadReceiptService, ReadReceiptService>();
        services.AddScoped<IDeliveryReceiptService, DeliveryReceiptService>();
        if (searchEnabled)
        {
            var settings = new ConnectionSettings(new Uri(openSearchUrl!))
                .DefaultIndex(configuration["OpenSearch:MessagesIndex"] ?? "messenger-messages");

            services.AddSingleton<IOpenSearchClient>(new OpenSearchClient(settings));
            services.AddScoped<IMessageSearchIndexer, OpenSearchMessageSearchIndexer>();
            services.AddScoped<IMessageFullTextSearchService, OpenSearchMessageFullTextSearchService>();
        }
        else
        {
            // Without OpenSearch the message endpoints must still work: indexing is skipped and search
            // runs against PostgreSQL.
            services.AddScoped<IMessageSearchIndexer, NoOpMessageSearchIndexer>();
            services.AddScoped<IMessageFullTextSearchService, PostgresMessageFullTextSearchService>();
        }
        services.AddScoped<IMessageForwarder, MessageForwarder>();
        return services;
    }
}