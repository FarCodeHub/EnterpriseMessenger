# EnterpriseMessenger API Contract

> **Outdated (May 2026).** Several endpoints and payloads here no longer match the code: login is Keycloak
> (the local login takes only `userName` and `deviceId`), there is no `GET /conversations` (use
> `GET /conversations/user/{userId}`), and many endpoints and SignalR events are missing (sync, messages,
> users, audit, `message.edited`, `message.deleted`, `conversation.read`, ...).
> The source of truth is Swagger UI in Development (`http://localhost:5113/swagger`), plus
> `src/Messenger.Contracts/RealTime/RealtimeEvents.cs` for SignalR event names.

Base URL:

```text
http://localhost:8080/api/v1
```

---

# Auth

## Login

```http
POST /auth/login
```

Request:

```json
{
  "userName": "farhad",
  "password": "123456",
  "deviceId": "web-001"
}
```

Response:

```json
{
  "accessToken": "jwt-token",
  "refreshToken": "refresh-token",
  "tokenType": "Bearer",
  "expiresInSeconds": 3600,
  "user": {
    "id": "guid",
    "displayName": "Farhad Salehi",
    "userName": "farhad"
  }
}
```

---

## Refresh Token

```http
POST /auth/refresh
```

Request:

```json
{
  "refreshToken": "refresh-token",
  "deviceId": "web-001"
}
```

Response:

```json
{
  "accessToken": "new-jwt-token",
  "refreshToken": "new-refresh-token",
  "tokenType": "Bearer",
  "expiresInSeconds": 3600,
  "user": {
    "id": "guid",
    "displayName": "Farhad Salehi",
    "userName": "farhad"
  }
}
```

---

# Conversations

## Get User Conversations

```http
GET /conversations
Authorization: Bearer {token}
```

Response:

```json
[
  {
    "id": "conversation-guid",
    "title": "Dev Team",
    "type": "Group",
    "lastMessageAtUtc": "2026-05-24T12:00:00Z",
    "unreadCount": 5
  }
]
```

---

## Get Conversation Members

```http
GET /conversations/{conversationId}/members
Authorization: Bearer {token}
```

Response:

```json
[
  {
    "id": "user-guid",
    "displayName": "Farhad Salehi",
    "userName": "farhad",
    "joinedAtUtc": "2026-05-20T10:00:00Z",
    "isMuted": false,
    "isPinned": true,
    "isArchived": false,
    "role": "Admin"
  }
]
```

---

# Files

## Upload File

```http
POST /files/upload
Authorization: Bearer {token}
Content-Type: multipart/form-data
```

Response:

```json
{
  "fileId": "file-guid",
  "fileName": "image.png",
  "contentType": "image/png",
  "size": 120000,
  "storagePath": "attachments/2026/05/image.png"
}
```
---

# Realtime / SignalR

Hub URL:

```text
http://localhost:8080/hubs/chat
```

Authentication:

```text
JWT access token via accessTokenFactory
```

## Client invokes

### Join Conversation

```js
connection.invoke("JoinConversation", conversationId)
```

### Start Typing

```js
connection.invoke("StartTyping", conversationId)
```

### Stop Typing

```js
connection.invoke("StopTyping", conversationId)
```

### Mark As Seen

```js
connection.invoke("MarkAsSeen", conversationId, sequenceNumber)
```

### Mark As Delivered

```js
connection.invoke("MarkAsDelivered", conversationId, sequenceNumber)
```

## Server events

### message.created

Payload: `SendMessageResult`

### conversation.updated

Payload:

```json
{
  "conversationId": "guid",
  "lastMessageId": "guid",
  "body": "Hello",
  "senderUserId": "guid",
  "sequenceNumber": 12,
  "createdAtUtc": "2026-05-24T12:00:00Z"
}
```

### typing.started / typing.stopped

```json
{
  "conversationId": "guid",
  "userId": "guid"
}
```

### message.seen / message.delivered

```json
{
  "conversationId": "guid",
  "userId": "guid",
  "sequenceNumber": 12
}
```

### presence.online / presence.offline

```json
"user-guid"
```


---

# Search

## Search Messages

```http
GET /search/messages?query=hello&conversationId={conversationId}&page=1&pageSize=20
Authorization: Bearer {token}
```

Notes:

```text
query is required.
conversationId is optional.
Only messages from conversations where current user is active member are returned.
Deleted messages are excluded.
Supports fuzzy matching and Persian/Arabic normalization.
```

Response:

```json
{
  "page": 1,
  "pageSize": 20,
  "totalCount": 1,
  "items": [
    {
      "id": "message-guid",
      "conversationId": "conversation-guid",
      "senderUserId": "user-guid",
      "body": "Hello world",
      "highlight": "<em>Hello</em> world",
      "sequenceNumber": 12,
      "createdAtUtc": "2026-05-27T08:00:00Z",
      "editedAtUtc": null
    }
  ]
}
```

---

## Search Conversations

```http
GET /search/conversations?query=dev&page=1&pageSize=20
Authorization: Bearer {token}
```

Notes:

```text
query is required.
Searches group title and private conversation participant displayName/userName.
Only conversations where current user is active member are returned.
```

Response:

```json
{
  "page": 1,
  "pageSize": 20,
  "totalCount": 1,
  "items": [
    {
      "id": "conversation-guid",
      "title": "Dev Team",
      "type": "Group",
      "createdAtUtc": "2026-05-27T08:00:00Z",
      "lastMessageAtUtc": "2026-05-27T08:30:00Z"
    }
  ]
}
```

---

## Rebuild Message Search Index

```http
POST /search/messages/rebuild-index
Authorization: Bearer {admin-token}
```

Response:

```json
{
  "message": "Messages search index rebuilt."
}
```