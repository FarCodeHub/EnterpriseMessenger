using Messenger.Domain.Attachments;
using Messenger.Domain.Conversations;
using Messenger.Domain.Messages;
using Messenger.Domain.Notifications;
using Messenger.Domain.Sync;
using Messenger.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Messenger.Domain.Auditing;
using Messenger.Domain.Outbox;

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
    public DbSet<ConversationCounter> ConversationCounters => Set<ConversationCounter>();
    public DbSet<MessageDelivery> MessageDeliveries => Set<MessageDelivery>();
    public DbSet<UserSyncCounter> UserSyncCounters => Set<UserSyncCounter>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<UploadedFile> UploadedFiles => Set<UploadedFile>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<DeadLetterOutboxMessage> DeadLetterOutboxMessages => Set<DeadLetterOutboxMessage>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MessengerDbContext).Assembly);
    }
}