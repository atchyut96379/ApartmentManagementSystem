# Custom domain for Marvel Rocks portal (simple login URL)

Use a short URL like **`https://portal.marvelrocks.in`** instead of `https://marvelrocks-ams.azurewebsites.net`.

## What you need

| Item | Example |
|------|---------|
| Domain | `marvelrocks.in` (from GoDaddy, Namecheap, etc.) |
| Subdomain for app | `portal` → full URL `https://portal.marvelrocks.in` |
| Azure App Service | `marvelrocks-ams` (already created) |
| Plan | **B1** or higher (custom domain + free SSL) |

---

## Step 1 — Buy domain (if you do not have one)

1. Buy **marvelrocks.in** (or similar) at your registrar.
2. Open **DNS management** for that domain (we will add records in Step 3).

---

## Step 2 — Add domain in Azure

1. [Azure Portal](https://portal.azure.com) → **App Service** → **marvelrocks-ams**.
2. **Custom domains** → **Add custom domain**.
3. Enter: `portal.marvelrocks.in` (or your chosen subdomain).
4. Azure shows DNS records to create — keep this page open.

---

## Step 3 — DNS at your registrar (GoDaddy / Namecheap)

Add what Azure asks for. Usually:

| Type | Name / Host | Value |
|------|-------------|--------|
| **CNAME** | `portal` | `marvelrocks-ams.azurewebsites.net` |
| **TXT** (verify) | `asuid.portal` | (verification string from Azure) |

Save DNS. Verification can take **5 minutes to 48 hours** (often under 1 hour).

Back in Azure → **Validate** → **Add**.

---

## Step 4 — HTTPS (free certificate)

1. App Service → **Custom domains** → your domain → **Add binding**.
2. Choose **App Service Managed Certificate** (free).
3. Hostname: `portal.marvelrocks.in`, **SNI SSL**, **HTTPS Only** = On.

---

## Step 5 — Tell the application your public URL

**Configuration → Application settings** → add or update:

```text
Application__AppUrl = https://portal.marvelrocks.in
```

Optional (restrict host names after domain works):

```text
AllowedHosts = portal.marvelrocks.in;marvelrocks-ams.azurewebsites.net
```

**Save** → **Restart** the app.

Also set the same URL in **Integrations → Public app URL** if you use that field.

---

## Step 6 — Razorpay & links

Update anywhere the old `azurewebsites.net` URL was used:

| Place | New URL |
|-------|---------|
| Razorpay → Website | `https://portal.marvelrocks.in/Home/Public` |
| Razorpay → Webhook | `https://portal.marvelrocks.in/api/razorpay/webhook` |
| Committee bookmark | `https://portal.marvelrocks.in/Account/Login` |

---

## Step 7 — Test

- [ ] `https://portal.marvelrocks.in/health` → healthy  
- [ ] `https://portal.marvelrocks.in/Account/Login` → login page  
- [ ] Login as Admin / resident  
- [ ] Pay link in WhatsApp reminder uses `portal.marvelrocks.in` (not azurewebsites.net)

---

## Suggested subdomain names

| URL | Use |
|-----|-----|
| `portal.marvelrocks.in` | Main app + login (recommended) |
| `pay.marvelrocks.in` | Payments only (optional) |
| `ams.marvelrocks.in` | Short for “apartment management” |

One subdomain is enough for login and the full app.

---

## Troubleshooting

| Problem | Fix |
|---------|-----|
| Domain not validating | Wait for DNS; check CNAME points to `*.azurewebsites.net` |
| Certificate pending | Domain must be validated first; use B1+ plan |
| Login works on azure URL but not custom domain | Set `Application__AppUrl` and restart |
| Redirect loop | Set `AllowedHosts` to include both custom domain and azure hostname |

---

## Cost

- Domain: ~₹800–1200 / year  
- Azure B1 + SQL: unchanged  
- SSL on App Service: **free** (managed certificate)
