# Organization sign-in (Keycloak + LDAP / Active Directory)

EnterpriseMessenger signs users in through [Keycloak](https://www.keycloak.org/). Keycloak checks the
password against the organization's LDAP / Active Directory, and the messenger only trusts the tokens
Keycloak issues. The messenger never sees or stores passwords.

```text
Browser ──(1) redirect──▶ Keycloak /sso ──(2) bind──▶ LDAP / AD
   ▲                          │
   └──(3) code + PKCE─────────┘
   │
   └──(4) Bearer access token──▶ Messenger API ──(5) map "sub" to local user
```

- The web app uses the Authorization Code flow with PKCE (public client `messenger-web`).
- Access tokens live 15 minutes and are refreshed in the background.
- On the first sign-in the API links the Keycloak account to the local user with the **same user name**
  (so existing conversations are kept), or creates a new local user.
- Users with the realm role `messenger-admin` (or local `IsAdmin`) are admins in the messenger.
- A deactivated messenger user is rejected within one minute, even with a valid token.

## Sign-in modes

| Environment            | Keycloak            | Username-only login               |
| ---------------------- | ------------------- | --------------------------------- |
| Development / Testing  | optional            | enabled                           |
| Production             | required            | disabled (`Auth__AllowLocalLogin=true` re-enables it — not secure) |

The web app asks `GET /api/v1/auth/config` which buttons to show, so one build works everywhere.

## Production setup

1. Fill in `.env.production` (see `.env.production.example`):
   - `KEYCLOAK_PUBLIC_URL` — public URL of Keycloak behind the bundled nginx, e.g. `https://chat.example.com/sso`
   - `KEYCLOAK_DB_PASSWORD`, `KEYCLOAK_ADMIN_USERNAME`, `KEYCLOAK_ADMIN_PASSWORD`
2. `docker compose -f docker-compose.production.yml --env-file .env.production up -d`
3. Apply the database migrations (`dotnet ef database update`), as for every release.
4. Open `https://chat.example.com/sso/admin`, sign in with the bootstrap admin, switch to realm **messenger**.
5. **Clients → messenger-web → Valid redirect URIs**: make sure `https://chat.example.com/*` is allowed
   (the imported realm allows `/*` on the Keycloak host and `http://localhost:4200/*`).
6. Connect Active Directory (next section).
7. **Realm roles → messenger-admin → Assign**: give the role to the messenger administrators.

The realm file `keycloak/realm-messenger.json` is imported only when the realm does not exist yet.
Later changes are made in the Keycloak admin console.

## Connecting LDAP / Active Directory

In the **messenger** realm: **User federation → Add LDAP provider**.

| Setting                   | Active Directory value                              |
| ------------------------- | --------------------------------------------------- |
| Vendor                    | Active Directory                                    |
| Connection URL            | `ldaps://dc01.company.local:636`                    |
| Bind type / Bind DN       | simple / a read-only service account                |
| Edit mode                 | `READ_ONLY`                                          |
| Users DN                  | e.g. `OU=Staff,DC=company,DC=local`                  |
| Username LDAP attribute   | `sAMAccountName`                                     |
| RDN / UUID attribute      | `cn` / `objectGUID`                                  |
| User object classes       | `person, organizationalPerson, user`                 |
| Search scope              | Subtree                                              |
| Import users              | On                                                   |
| Periodic sync             | On (e.g. every 24 h) so disabled AD accounts stop working |

Click **Test connection** and **Test authentication**, save, then **Synchronize all users**.

The messenger user name is Keycloak's `preferred_username`, which is `sAMAccountName` with the settings
above. Existing messenger users whose user name matches are linked on their first sign-in; check that
`SEED_ADMIN_USERNAME` matches the AD account of the administrator.

To show Persian full names, keep the default `full name` mapper (or map `displayName` to the `firstName`
attribute).

## Local development

`docker compose up -d` also starts Keycloak in dev mode on <http://localhost:8081/sso>
(admin console: `admin` / `admin`). The API's `appsettings.Development.json` already points to it, and
the login page shows both "Sign in with your organization account" and the development username login.
Create a test user in the **messenger** realm (Users → Add user → Credentials) to try the Keycloak flow.
