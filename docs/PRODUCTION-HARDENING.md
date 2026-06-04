# Production hardening (high priority)

Implemented in the app plus manual Azure steps.

## In the application (after deploy)

| Feature | What it does |
|---------|----------------|
| **Password policy** | Production: min 8 chars + digit. Development: relaxed for local testing. |
| **Login lockout** | 5 failed attempts → 15 minute lockout |
| **Session timeout** | 60 minutes idle logout (sliding) |
| **Admin first login** | New `Admin` user must change password |
| **Health check** | `GET /health` includes database status |
| **Razorpay webhook** | Ignores duplicate `payment.captured` for same transaction |
| **Duplicate flats** | Blocks two residents on the same flat number |
| **Login page** | Shows public portal URL from `Application__AppUrl` |

## Azure — you must configure

### 1. Custom domain (simple login URL)

Follow **[CUSTOM-DOMAIN.md](./CUSTOM-DOMAIN.md)** — e.g. `https://portal.marvelrocks.in`.

Set:

```text
Application__AppUrl = https://portal.marvelrocks.in
```

### 2. SQL Database backups

1. Azure Portal → **SQL database** → **ApartmentManagementDB**.
2. **Backup and restore** → verify **Point-in-time restore** is enabled.
3. Retention: **7–35 days** (Basic tier allows limited retention).

### 3. Application Insights (optional, recommended)

1. App Service → **Application Insights** → **Turn on**.
2. Copy **Connection string**.
3. App Service → **Configuration** → add:

```text
APPLICATIONINSIGHTS_CONNECTION_STRING = InstrumentationKey=...;IngestionEndpoint=...
```

4. Restart app. View failures under **Failures** and **Logs**.

### 4. Security settings

| Setting | Value |
|---------|--------|
| `Identity__ResetAndSeedAdminOnStartup` | `false` |
| HTTPS Only | On |
| Rotate default `Admin` password | After first login |

### 5. MSG91 key

If the auth key appeared in screenshots or chat, **regenerate** in MSG91 and update Azure.

## Verify after deploy

```text
https://portal.marvelrocks.in/health
```

Expect JSON like: `{"status":"Healthy","checks":{"database":"Healthy"}}`.
