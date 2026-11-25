# EDUTWIN DEPLOYMENT SINGLE SOURCE OF TRUTH (SSOT)

> **Document Version:** 1.4.0  
> **Last Verified Date:** 2026-11-26  
> **Target Environment:** Cloud Staging (0 VNĐ — Free Tiers & Aiven Trial)  
> **Baseline Commit:** `f8b7bb9401dbeb8f825a1cf93905ea20607ce62f`  
> **Working Branch:** `ui_update_v3`  
> **Status:** TASK 2.1 COMPLETED LIVE ON AIVEN MYSQL 8.4.11 — READY FOR COMMIT APPROVAL & TASK 2.2–2.7

---

## A. PROJECT IDENTITY

- **Project Name:** EduTwin — AI-Driven Adaptive Learning & Competency Assessment Platform
- **Repository URL:** `https://github.com/nuyntai19/AI-Driven-Adaptive-Learning-and-Competency-Assessment-Platform`
- **Primary Working Branch:** `ui_update_v3` (Direct merge/push to `main` prohibited until TASK 4)
- **Baseline Git SHA:** `f8b7bb9401dbeb8f825a1cf93905ea20607ce62f`
- **Deployment Tier:** Cloud Staging (0 VNĐ budget, no billing enabled)

---

## B. ARCHITECTURE OVERVIEW (APPROVED FOR TASK 2)

| Layer | Cloud Staging Platform | Key Verified Specs |
|---|---|---|
| **Frontend** | **Vercel Hobby** | Static SPA (`web/edutwin-web`, Vite) + `/api/*` Proxy Rewrite |
| **Backend API** | **Render Free Web Service** | Custom Docker (`./src/EduTwin.API/Dockerfile`), Port `8080`, 512 MiB RAM |
| **Relational Database** | **Aiven MySQL Developer Free Trial** | **MySQL 8.4.11**, Host `mysql-e9782e9-thanhhangdepgai2503-c073.b.aivencloud.com:22943`, DB `edutwin_staging`, User `avnadmin`, TLS Required, `log_bin=ON`, `log_bin_trust_function_creators=ON`, `CREATE TRIGGER` verified. (\$50 trial credit, ~30 days, no payment method attached.) |
| **Persistent Object Storage** | **Supabase Storage Free** | Project `edutwin-storage`, Private Bucket `edutwin-attachments` (1 GiB Free) |
| **Data Protection Keys** | **Aiven MySQL (`data_protection_keys`)** | `MySqlDataProtectionKeyRepository` with AES-256-GCM at rest + env-scoped `ApplicationName` (`EduTwin-Staging`) |
| **AI Assessment Engine** | **Google AI Studio (Gemini Free Tier)** | Existing configured model (`gemini-3.5-flash-lite`), 0 billing |
| **Source Control** | **GitHub (`ui_update_v3`)** | Deployment-only commit isolated from business-logic/test changes |

---

## C. MIGRATION & TRIGGER BASELINE (VERIFIED)

- **EF Core Migration Classes (`: Migration`):** **62** files in `src/EduTwin.DAL/Persistence/Migrations/`.
- **EF Core Applied Migrations (`__EFMigrationsHistory`):** **61** migrations (`TOTAL_MIGRATION_ATTRS=61`, `UNIQUE_MIGRATION_IDS=61`).
  - *Exact 62 vs 61 Discrepancy:* `20261016190000_AddAdaptivePracticeSessions.cs` inherits `: Migration` but lacks both a `.Designer.cs` file and a `[Migration("...")]` attribute, so EF Core does not register or execute it (adaptive practice tables are already covered in the active migration chain; historical migration files are preserved untouched).
- **Schema & Invariants (LIVE VERIFIED ON AIVEN MYSQL 8.4.11 — `edutwin_staging`):**
  - **61 applied migrations** in `__EFMigrationsHistory`.
  - **74 schema tables** (73 domain entities + `__EFMigrationsHistory`) + **`data_protection_keys`** persisted in MySQL = **75 tables total**, **161 Foreign Keys**.
  - **41 `CREATE TRIGGER`** + **19 `DROP TRIGGER`** statements in source → **49 active triggers** verified in `INFORMATION_SCHEMA.TRIGGERS`.
  - Negative Raw SQL Trigger Test on Aiven MySQL 8.4.11: `DELETE FROM knowledge_graph_versions LIMIT 1;` rejected with `ERROR 1644 (45000): EDUTWIN_ACADEMIC_DEPENDENCY: graph version history cannot be deleted`.
  - Probe table `deploy_probe_trigger` & trigger `deploy_probe_before_insert` tested (negative `ERROR 1644: Invalid amount`) and cleanly dropped prior to migration.
- **MySQL 8.4.11 + `MySql.EntityFrameworkCore` 10.0.7 Compatibility Notes (VERIFIED LIVE):**
  - `caching_sha2_password` + TLS `VerifyCA` (`SslMode=VerifyCA;SslCa=/etc/secrets/aiven-ca.pem;`) verified live against Aiven Project CA (`CN=a95af98e-fddc-4008-ad33-896a6b0ef697 Project CA`).
  - Aiven enables `sql_require_primary_key=ON` by default; `SeedExtensions.ApplyMigrationsAndSeedAsync` scopes `SET SESSION sql_require_primary_key = 0` strictly around `MigrateAsync()` and restores `DEFAULT` in `finally` without altering any historical migration.

---

## D. DEPLOYMENT CONFIGURATION (RENDER & VERCEL)

### 1. Render Free Web Service (Backend)
- **Repository:** `nuyntai19/AI-Driven-Adaptive-Learning-and-Competency-Assessment-Platform`
- **Branch:** `ui_update_v3`
- **Plan:** `Free`
- **Runtime:** `Docker`
- **Dockerfile Path:** `./src/EduTwin.API/Dockerfile`
- **Docker Build Context:** `.`
- **Port:** `8080` (`ASPNETCORE_URLS=http://+:8080`)
- **Health Check Path:** `/api/v1/health/live`
- **Secret File (Recommended for `VerifyCA`):** `/etc/secrets/aiven-ca.pem` (paste Aiven CA certificate)

#### Render Environment Variables (Names Only — Secrets Excluded)
```text
ASPNETCORE_ENVIRONMENT=Staging
ASPNETCORE_URLS=http://+:8080
ConnectionStrings__Default=Server=mysql-e9782e9-thanhhangdepgai2503-c073.b.aivencloud.com;Port=22943;Database=edutwin_staging;User=avnadmin;Password=<AIVEN_PASSWORD>;SslMode=VerifyCA;SslCa=/etc/secrets/aiven-ca.pem;
HealthChecks__MySqlHost=mysql-e9782e9-thanhhangdepgai2503-c073.b.aivencloud.com
HealthChecks__MySqlPort=22943
Jwt__SigningKey=<SECURE_RANDOM_MIN_32_BYTES_KEY>
Jwt__Issuer=EduTwinAuth
Jwt__Audience=EduTwinUsers
GEMINI_LIST_KEY=["<GEMINI_API_KEY_1>","<GEMINI_API_KEY_2>"]
Gemini__Model=gemini-3.5-flash-lite
Gemini__Timeout=00:00:30
Gemini__MaxConcurrentRequests=1
AIAnalysisWorker__MaxConcurrentJobs=2
AIGrading__MicroBatchEnabled=true
AIGrading__BatchSize=3
AIGrading__BatchWindow=00:00:00.150
AIGrading__Provider=Gemini
# Staging Initialization (set true ONLY for first bootstrap, then immediately set false)
StagingInit__AllowSeed=false
StagingInit__ExpectedDatabase=edutwin_staging
Seed__Enabled=false
Seed__CenterManagerPassword=<UNIQUE_STAGING_MANAGER_PASSWORD_MIN_12_CHARS>
Seed__TeacherPassword=<UNIQUE_STAGING_TEACHER_PASSWORD_MIN_12_CHARS>
Seed__StudentPassword=<UNIQUE_STAGING_STUDENT_PASSWORD_MIN_12_CHARS>
PlatformBootstrap__AdminUsername=platform.admin
PlatformBootstrap__AdminDisplayName=Platform Administrator
PlatformBootstrap__AdminPassword=<UNIQUE_STAGING_ADMIN_PASSWORD_MIN_12_CHARS>
AttachmentStorage__Provider=Supabase
AttachmentStorage__CloudBaseUrl=https://<SUPABASE_PROJECT_REF>.supabase.co/storage/v1
AttachmentStorage__CloudApiKey=<SUPABASE_SERVICE_ROLE_KEY>
AttachmentStorage__BucketName=edutwin-attachments
AttachmentStorage__RootPath=/app/storage
AttachmentStorage__DataProtectionKeyStorage=Database
AttachmentStorage__DataProtectionKeyEncryptionSecret=<SECURE_RANDOM_32_BYTES_DP_SECRET>
```

### 2. Vercel Hobby (Frontend)
- **Repository:** `nuyntai19/AI-Driven-Adaptive-Learning-and-Competency-Assessment-Platform`
- **Branch:** `ui_update_v3`
- **Plan:** `Hobby`
- **Root Directory:** `web/edutwin-web`
- **Framework Preset:** `Vite`
- **Install Command:** `npm ci`
- **Build Command:** `npm run build` (`tsc -b && vite build`)
- **Output Directory:** `dist`
- **API Proxy/Rewrite:** `web/edutwin-web/vercel.json` rewrites `/api/:path*` → `https://<ACTUAL_RENDER_SERVICE>.onrender.com/api/:path*` and `/(.*)` → `/index.html`. (Update destination hostname if Render assigns a different service URL.)

---

## E. AIVEN 30-DAY TRIAL BACKUP & EXIT PROCEDURE

Before the Aiven Developer Trial expires (~30 days / \$50 credit), run a full logical backup preserving all 49 triggers and routines:

```bash
mysqldump \
  --host=mysql-e9782e9-thanhhangdepgai2503-c073.b.aivencloud.com \
  --port=22943 \
  --user=avnadmin \
  --password \
  --ssl-mode=VERIFY_CA \
  --ssl-ca=./scratch/aiven-ca.pem \
  --single-transaction \
  --set-gtid-purged=OFF \
  --routines \
  --triggers \
  --databases edutwin_staging \
  > ./scratch/edutwin_staging_backup_$(date +%Y%m%d).sql
```

Store encrypted backup off-host (never commit `.sql` dumps to Git).

---

## F. VALIDATION EVIDENCE (CURRENT)

- **Backend Build:** `dotnet build EduTwin.sln` — **PASS** (0 Warnings, 0 Errors); Docker `src/EduTwin.API/Dockerfile` — **PASS**.
- **Frontend Build:** Windows (`Node v26.13.1`, `npm ci && npm run build`) — **PASS**; Linux Docker (`node:22-alpine`) — **PASS**.
- **Deployment Targeted Regression:** `StagingSeedAndSecurityGuardTests`, `CloudAttemptAttachmentStorageTests`, `EduTwinRuntimeSeederTests`, `AttachmentOrphanCleanupWorkerTests` — **PASS** (including live MySQL 8.0.35 Data Protection restart test).
- **Frontend Unit Tests:** **968 PASS / 0 FAIL / 0 SKIP** (26 suites).
- **Measured Container RAM (`--memory=512m`):** `41.83 MiB` startup → `46.6 MiB` steady-state idle.
- **Secret Scan (19 deployment commit files):** **0 leaks**, `git diff --check` clean.
- **Aiven MySQL 8.4.11 Live Staging Verification (`edutwin_staging`):**
  - TLS `VerifyCA` + Project CA verified (`openssl verify`: OK).
  - **61 migrations** applied, **74 base tables + `data_protection_keys`**, **161 Foreign Keys**, **49 active triggers** verified in `INFORMATION_SCHEMA`.
  - Staging seed completed (`1 PlatformAdmin`, `2 CenterManager`, `4 Teacher`, `10 Student` with unique per-role passwords in gitignored `scratch/aiven.env`).
  - Live API health (`/api/v1/health/live` = HTTP 200) and live login verified for `CenterManager`, `Teacher` (`teacher.math`), `Student` (`student01`), and `PlatformAdmin` (`/api/v1/auth/admin/login`).
  - **Data Protection Key Ring Persistence:** `28/28` deployment regression tests PASS including live Aiven MySQL 8.4.11 simulated restart test (`MySqlDataProtectionKeyRepository_PersistsKeysAcrossSimulatedRestart_WhenMySqlAvailable`).
- **Pending Live Cloud Verification:** Supabase `edutwin-attachments` live API CRUD + orphan cleanup (TASK 2.2), Render + Vercel E2E smoke tests (TASK 2.4–2.7).
