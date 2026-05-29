export const ChatSignalREvents = {
  MessageCreated: 'message.created',
  MessageEdited: 'message.edited',
  MessageDeleted: 'message.deleted',

  ConversationUpdated: 'conversation.updated',

  TypingStarted: 'typing.started',
  TypingStopped: 'typing.stopped',

  MessageSeen: 'message.seen',
  MessageDelivered: 'message.delivered',

  PresenceRegistered: 'presence.registered',
  PresenceSnapshot: 'presence.snapshot',
  PresenceChanged: 'presence.changed',

  ConversationJoined: 'conversation.joined',
  HubError: 'hub.error'
} as const;