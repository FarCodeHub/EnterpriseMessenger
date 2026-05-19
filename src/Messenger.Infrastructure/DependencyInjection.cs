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
        return services;
    }
}