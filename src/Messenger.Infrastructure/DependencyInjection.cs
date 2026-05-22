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
namespace Messenger.Infrastructure;


public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("MessengerDb");

        services.AddDbContext<MessengerDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
        });

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
        services.AddScoped<IFileUploader, LocalFileUploader>();
        services.AddScoped<IAttachmentDownloader, AttachmentDownloader>();
        var redisConnectionString = configuration["Redis:ConnectionString"];

        if (!string.IsNullOrWhiteSpace(redisConnectionString))
        {
            services.AddSingleton<IConnectionMultiplexer>(
                ConnectionMultiplexer.Connect(redisConnectionString));
        }


        services.AddMassTransit(options =>
        {
            options.UsingRabbitMq((context, cfg) =>
            {
                var host = configuration["RabbitMq:Host"] ?? "localhost";
                var username = configuration["RabbitMq:Username"] ?? "guest";
                var password = configuration["RabbitMq:Password"] ?? "guest";

                cfg.Host(host, "/", h =>
                {
                    h.Username(username);
                    h.Password(password);
                });

                cfg.ConfigureEndpoints(context);
            });


            options.AddConsumer<MessageCreatedIntegrationEventConsumer>();
            options.AddConsumer<NotificationRequestedIntegrationEventConsumer>();
        });
        services.AddSingleton<IConnectionTracker, RedisConnectionTracker>();
        services.AddScoped<IConversationMemberAdder, ConversationMemberAdder>();
        services.AddScoped<IConversationMemberRemover, ConversationMemberRemover>();
        services.AddScoped<IConversationMemberRoleSetter, ConversationMemberRoleSetter>();
        services.AddScoped<IConversationLeaver, ConversationLeaver>();
        services.AddScoped<IConversationMemberSettingsUpdater, ConversationMemberSettingsUpdater>();
        services.AddScoped<IConversationMemberReader, ConversationMemberReader>();
        services.AddScoped<IUserLoginService, UserLoginService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<ISessionRevoker, SessionRevoker>();
        services.AddScoped<IUserSessionReader, UserSessionReader>();
        services.AddScoped<IAuditLogWriter, AuditLogWriter>();
        services.AddScoped<IUserDeactivator, UserDeactivator>();
        services.AddScoped<IUserActivator, UserActivator>();
        services.AddScoped<IUserAdminMaker, UserAdminMaker>();
        return services;
    }
}