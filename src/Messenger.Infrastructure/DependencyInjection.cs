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
        });

        return services;
    }
}