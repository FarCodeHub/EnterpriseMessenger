export const SignalREvents = {
  MessageCreated: 'message.created',
  MessageEdited: 'message.edited',
  MessageDeleted: 'message.deleted',

  ConversationUpdated: 'conversation.updated',

  TypingStarted: 'typing.started',
  TypingStopped: 'typing.stopped',

  MessageSeen: 'message.seen',
  MessageDelivered: 'message.delivered',

  PresenceOnline: 'presence.online',
  PresenceOffline: 'presence.offline',

  ConversationJoined: 'conversation.joined',

  HubError: 'hub.error'
} as const;