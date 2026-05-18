using Messenger.Domain.Conversations;
using Messenger.Domain.Messages;
using Messenger.Domain.Sync;
using Messenger.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Infrastructure.Persistence;

public sealed class MessengerDbContext : DbContext
{
    public MessengerDbContext(DbContextOptions<MessengerDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<UserDevice> UserDevices => Set<UserDevice>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ConversationMember> ConversationMembers => Set<ConversationMember>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<UserSyncEvent> UserSyncEvents => Set<UserSyncEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MessengerDbContext).Assembly);
    }
}