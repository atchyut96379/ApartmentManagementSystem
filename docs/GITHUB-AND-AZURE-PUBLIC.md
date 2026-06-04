# Push to GitHub → Deploy on Azure (free account) → Public URL

Account: **atchyut96403@gmail.com** · Credits: use before expiry.

---

## Part 1 — GitHub (code backup)

Repo: **https://github.com/atchyut96379/ApartmentManagementSystem**

Secrets **never** go to GitHub:

- `integrations.local.json` (Razorpay, SMTP passwords)
- `appsettings.Development.json` (your PC SQL server)

On a new PC: copy `appsettings.Development.json.example` → `appsettings.Development.json` and edit.

---

## Part 2 — Azure resources (one-time, ~20 min)

### 2.1 Resource group

1. Portal → **Resource groups** → **Create**
2. Name: `rg-marvelrocks-ams`
3. Region: **Central India**

### 2.2 SQL Database

1. **Create a resource** → **SQL Database**
2. Database name: `ApartmentManagementDB`
3. Server: create new, e.g. `marvelrocks-sql`, admin login + strong password (save it)
4. **Networking**: Allow Azure services; add your PC IP for SSMS import
5. Tier: **Basic** (or smallest; uses credits)

**Import your data** (from local SQL):

- SSMS → your local DB → **Tasks** → **Export Data-tier Application** (.bacpac)
- SSMS → connect to Azure SQL → **Import Data-tier Application**

### 2.3 Web App

1. **Create a resource** → **Web App**
2. Name: e.g. `marvelrocks-ams` → URL will be `https://marvelrocks-ams.azurewebsites.net`
3. Publish: **Code**
4. Runtime stack: **.NET 10** (or latest .NET available)
5. OS: **Windows** (simplest for SQL client)
6. Region: **Central India**
7. Plan: **Free F1** for trial, or **Basic B1** (uses credits, better for testing)

---

## Part 3 — Deploy from GitHub (recommended)

1. Open your **Web App** → **Deployment Center**
2. Source: **GitHub**
3. Sign in → org/user **atchyut96379** → repo **ApartmentManagementSystem** → branch **main**
4. Build provider: **GitHub Actions** (default) or **App Service build service**
5. Save — first deploy may take 5–10 minutes

If build fails on .NET 10, use ZIP deploy instead:

```powershell
cd D:\Projects\ApartmentManagementSystem-Dev
.\scripts\Publish-Azure.ps1
```

Portal → Web App → **Advanced Tools (Kudu)** → **Zip Push Deploy** → upload `deploy/apartment-app.zip`.

---

## Part 4 — App settings (required)

Web App → **Configuration** → **Application settings** → **New**:

| Name | Value |
|------|--------|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `DATABASE_MIGRATE_ON_STARTUP` | `true` (first deploy only) |
| `ConnectionStrings__DefaultConnection` | Azure SQL connection string from portal |
| `Application__AppUrl` | `https://marvelrocks-ams.azurewebsites.net` |
| `Society__ApartmentName` | `Marvel Rocks` |
| `Payment__Provider` | `Razorpay` |
| `Payment__Razorpay__KeyId` | `rzp_test_...` (test keys for now) |
| `Payment__Razorpay__KeySecret` | your test secret |

Copy full list from `deploy/azure-app-settings.template.json`.

**Save** → **Restart** app.

Test: `https://marvelrocks-ams.azurewebsites.net/health`  
Login: `https://marvelrocks-ams.azurewebsites.net/Account/Login`

---

## Part 5 — Public URL (two stages)

### Stage A — Free testing (no domain purchase)

Use Azure default URL:

**`https://marvelrocks-ams.azurewebsites.net`**

- Share this with committee for testing
- Razorpay **test** keys: set website in Razorpay to `https://marvelrocks-ams.azurewebsites.net/Home/Public`

### Stage B — Custom domain (when going live)

1. Buy domain (e.g. `marvelrocks.in` on GoDaddy / Namecheap)
2. Web App → **Custom domains** → **Add custom domain**
3. Add DNS records at registrar (CNAME or A + TXT as Azure shows)
4. **Managed certificate** → free HTTPS
5. Update `Application__AppUrl` to `https://payments.marvelrocks.in` (or your choice)
6. Razorpay live: same domain + KYC + live keys

---

## Part 6 — Checklist after deploy

- [ ] `/health` returns OK
- [ ] Login as Admin — change default password
- [ ] One test Razorpay payment (test keys)
- [ ] Set `DATABASE_MIGRATE_ON_STARTUP` to `false` after first success
- [ ] Budget alert in **Cost Management** (e.g. ₹1000)

---

## Your credits (~₹19,138)

Enough for **Basic B1 + SQL Basic** for several months of testing. Prefer **B1** over F1 if the app sleeps too often on free F1.

When credits end → switch to pay-as-you-go or delete resource group `rg-marvelrocks-ams` to stop charges.
