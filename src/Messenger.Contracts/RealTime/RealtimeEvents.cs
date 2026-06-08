namespace Messenger.Contracts.Realtime;

public static class RealtimeEvents
{
    public const string MessageCreated = "message.created";
    public const string MessageEdited = "message.edited";
    public const string MessageDeleted = "message.deleted";
    public const string ConversationUpdated = "conversation.updated";

    public const string TypingStarted = "typing.started";
    public const string TypingStopped = "typing.stopped";

    public const string PresenceOnline = "presence.online";
    public const string PresenceOffline = "presence.offline";

    public const string MessageSeen = "message.seen";
    public const string MessageSeenACK = "message.seen.ack";

    public const string MessageDelivered = "message.delivered";
    public const string PresenceRegistered = "presence.registered";
    public const string MessageDeliveredACK = "message.delivered.ack";
    public const string ConversationRead = "conversation.read";

    public const string PresenceChanged = "presence.changed";
    public const string PresenceSnapshot = "presence.snapshot";




    public const string ConversationJoined = "conversation.joined";
    public const string HubError = "hub.error";
}