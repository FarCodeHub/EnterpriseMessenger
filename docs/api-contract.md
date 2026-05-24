# EnterpriseMessenger API Contract

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