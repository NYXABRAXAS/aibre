# OPTIM AI BRE Engine — Troubleshooting Guide

> All issues are specific to this project's actual configuration. Check each section systematically.

---

## TABLE OF CONTENTS

1. [Backend Startup Errors](#1-backend-startup-errors)
2. [Database Connection Errors](#2-database-connection-errors)
3. [Authentication & Login Errors](#3-authentication--login-errors)
4. [Redis Connection Errors](#4-redis-connection-errors)
5. [Frontend Build Errors](#5-frontend-build-errors)
6. [API Call Errors](#6-api-call-errors)
7. [Rule Execution Errors](#7-rule-execution-errors)
8. [Docker Errors](#8-docker-errors)
9. [Port Conflict Errors](#9-port-conflict-errors)
10. [AI Service Errors](#10-ai-service-errors)
11. [EF Core / Migration Errors](#11-ef-core--migration-errors)
12. [Performance Issues](#12-performance-issues)
13. [Diagnostic Commands Reference](#13-diagnostic-commands-reference)

---

## 1. BACKEND STARTUP ERRORS

### Error: "No connection string named 'PostgreSQL' was found"

**Cause:** `appsettings.Development.json` is missing or ASPNETCORE_ENVIRONMENT is not set.

**Fix:**
```powershell
# Set environment before running:
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run

# Verify the file exists:
Test-Path "src\backend\OptimAI.BRE.Gateway\appsettings.Development.json"
```

---

### Error: "Could not load type 'OptimAI.BRE.RuleEngine.Infrastructure.BREDbContext'"

**Cause:** Build incomplete — one or more class library projects failed to build.

**Fix:**
```powershell
cd src\backend

# Clean and rebuild:
dotnet clean OptimAI.BRE.sln
dotnet restore OptimAI.BRE.sln
dotnet build OptimAI.BRE.sln --configuration Debug

# If errors remain, check which project has the error:
dotnet build OptimAI.BRE.RuleEngine\OptimAI.BRE.RuleEngine.csproj
```

---

### Error: "Address already in use: localhost:5000"

**Cause:** Another process is using port 5000.

**Fix:**
```powershell
# Find the process using port 5000:
netstat -ano | findstr :5000

# Kill the process (replace PID with actual PID number):
taskkill /PID <PID> /F

# Or run on a different port:
dotnet run --urls "http://localhost:5001"
```

If you change the port, also update the frontend `.env.local`:
```env
NEXT_PUBLIC_API_URL=http://localhost:5001/api/v1
```

---

### Error: "BCrypt.Net.BCrypt type not found"

**Cause:** NuGet packages not restored.

**Fix:**
```powershell
cd src\backend
dotnet restore OptimAI.BRE.sln --force
dotnet build OptimAI.BRE.sln
```

---

### Error: "Unable to load the service index for source https://api.nuget.org/v3/index.json"

**Cause:** No internet connection or NuGet is blocked by corporate firewall.

**Fix:**
```powershell
# Check internet connectivity:
Test-NetConnection -ComputerName api.nuget.org -Port 443

# If behind proxy, set NuGet proxy:
dotnet nuget add source "https://api.nuget.org/v3/index.json" --name nuget.org
```

---

### Error: "System.InvalidOperationException: Cannot resolve 'IRuleExecutionService'"

**Cause:** DI registration is missing. The service was not registered in Program.cs.

**Fix:** Check `Program.cs` in `OptimAI.BRE.Gateway` — all services must be registered. The key registrations required:

```csharp
// These must all be in Program.cs AddScoped calls:
builder.Services.AddScoped<IRuleExecutionService, RuleExecutionService>();
builder.Services.AddScoped<IConditionEvaluator, ConditionEvaluator>();
builder.Services.AddScoped<IActionExecutor, ActionExecutor>();
builder.Services.AddScoped<IRiskScoringEngine, RiskScoringEngine>();
builder.Services.AddScoped<IRuleLoader, CachedRuleLoader>();
```

---

## 2. DATABASE CONNECTION ERRORS

### Error: "28P01: password authentication failed for user 'optimai'"

**Cause:** Wrong password or user doesn't exist.

**Fix:**
```powershell
# Connect as postgres superuser:
psql -U postgres -h localhost

# Inside psql:
ALTER USER optimai WITH PASSWORD 'dev_password';
# OR recreate:
DROP USER IF EXISTS optimai;
CREATE USER optimai WITH PASSWORD 'dev_password';
GRANT ALL PRIVILEGES ON DATABASE optimai_bre TO optimai;
\q
```

---

### Error: "3D000: database 'optimai_bre' does not exist"

**Cause:** Database was never created.

**Fix:**
```powershell
psql -U postgres -h localhost -c "CREATE DATABASE optimai_bre OWNER optimai;"
psql -U optimai -h localhost -d optimai_bre -f "database\migrations\001_initial_schema.sql"
psql -U optimai -h localhost -d optimai_bre -f "database\migrations\002_seed_admin.sql"
```

---

### Error: "connection refused: localhost:5432"

**Cause:** PostgreSQL service is not running.

**Fix:**
```powershell
# Check if PostgreSQL is running (Windows service):
Get-Service -Name "postgresql*"

# Start it:
Start-Service -Name "postgresql-x64-15"   # adjust version number

# Or using pg_ctl (if psql is in PATH):
pg_ctl start -D "C:\Program Files\PostgreSQL\15\data"
```

---

### Error: "relation 'tenants' does not exist" (during startup)

**Cause:** SQL migration files were not run against the database.

**Fix:**
```powershell
$dbPath = "C:\Users\Lokesh\Desktop\zerocodebreengine\optim-ai-bre\database\migrations"

psql -U optimai -h localhost -d optimai_bre -f "$dbPath\001_initial_schema.sql"
psql -U optimai -h localhost -d optimai_bre -f "$dbPath\002_seed_admin.sql"
```

---

### Error: EF Core migration conflicts with existing tables

**Symptom:** "Table 'tenants' already exists" during `dotnet ef database update`.

**Cause:** You ran the SQL migration files first, so EF doesn't need to create tables.

**Fix — Mark migration as applied without running it:**
```powershell
# This tells EF the migration was already applied (don't re-run it):
dotnet ef database update InitialCreate --project src\backend\OptimAI.BRE.RuleEngine `
    --startup-project src\backend\OptimAI.BRE.Gateway `
    --no-build
```

If that doesn't work, manually insert the migration record:
```sql
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20240101000000_InitialCreate', '8.0.0');
```

---

## 3. AUTHENTICATION & LOGIN ERRORS

### Error: "401 Unauthorized" on login

**Cause 1:** Wrong credentials.
```
Admin:  admin@optimai.in  / Admin@1234
Demo:   demo@demobank.in  / Demo@1234
```

**Cause 2:** Seed data not loaded.
```powershell
# Check if admin user exists:
psql -U optimai -h localhost -d optimai_bre `
    -c "SELECT email, is_active, failed_login_attempts FROM users;"
```

**Cause 3:** Account locked (5 failed attempts locks it).
```powershell
# Unlock admin account:
psql -U optimai -h localhost -d optimai_bre `
    -c "UPDATE users SET failed_login_attempts=0, locked_until=NULL WHERE email='admin@optimai.in';"
```

---

### Error: "401 Unauthorized" on API calls after successful login

**Cause 1:** Not sending the Bearer token correctly.

**Correct format:**
```
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```
Note: `Bearer ` followed by the token (space between "Bearer" and the token).

**Cause 2:** Token expired (default expiry = 480 minutes / 8 hours).

**Fix:** Re-login to get a fresh token.

**Cause 3:** JWT secret key mismatch between what generated the token and current config.

**Fix:** Ensure `appsettings.Development.json` has a consistent SecretKey and restart the backend.

---

### Error: "403 Forbidden" on specific endpoints

**Cause:** Your user role doesn't have the required permission.

**Check user permissions:**
```powershell
psql -U optimai -h localhost -d optimai_bre -c "
SELECT u.email, r.role_name, p.permission_code
FROM users u
JOIN user_roles ur ON u.id = ur.user_id
JOIN roles r ON ur.role_id = r.id
JOIN role_permissions rp ON r.id = rp.role_id
JOIN permissions p ON rp.permission_id = p.id
WHERE u.email = 'admin@optimai.in'
ORDER BY p.permission_code;"
```

Admin should have 21 permissions. If missing, re-run seed:
```powershell
psql -U optimai -h localhost -d optimai_bre -f "database\migrations\002_seed_admin.sql"
```

---

### Error: "Invalid tenant" or "X-Tenant-ID header missing"

**Cause:** The API needs to know which tenant the request belongs to.

**Fix — Add tenant header in Postman:**
```
X-Tenant-ID: a0000000-0000-0000-0000-000000000001
```

Get tenant IDs:
```powershell
psql -U optimai -h localhost -d optimai_bre -c "SELECT id, tenant_name, tenant_code FROM tenants;"
```

The JWT token from login automatically includes the tenant claim — this error usually means you're calling the API without logging in first.

---

## 4. REDIS CONNECTION ERRORS

### Error: "StackExchange.Redis.RedisConnectionException: It was not possible to connect to the redis server(s)"

**Cause:** Redis is not running.

**Fix (Docker):**
```powershell
# Check if redis container is running:
docker ps | findstr redis

# If not running:
docker start redis-local

# Or create fresh:
docker run -d --name redis-local -p 6379:6379 redis:7-alpine

# Verify:
docker exec redis-local redis-cli ping
# Expected: PONG
```

**Fix (Memurai Windows service):**
```powershell
Get-Service -Name "Memurai"
Start-Service -Name "Memurai"
```

---

### Error: "Connection timeout to localhost:6379"

**Cause:** Firewall blocking port 6379.

**Fix:**
```powershell
# Check if port is reachable:
Test-NetConnection -ComputerName localhost -Port 6379

# If blocked, add firewall rule:
New-NetFirewallRule -DisplayName "Redis" -Direction Inbound -Protocol TCP -LocalPort 6379 -Action Allow
```

---

### Redis Not Critical in Development

The app works without Redis — rules are loaded from the database each time (slower but functional). Redis only affects caching.

To disable Redis requirement temporarily, in `appsettings.Development.json`:
```json
"Redis": {
  "Enabled": false
}
```

Or, configure a longer timeout to fail gracefully:
```json
"ConnectionStrings": {
  "Redis": "localhost:6379,connectTimeout=1000,syncTimeout=1000,abortConnect=false"
}
```

The key is `abortConnect=false` — this prevents the app from crashing if Redis is unavailable.

---

## 5. FRONTEND BUILD ERRORS

### Error: "Cannot find module 'next'"

**Cause:** `npm install` was not run.

**Fix:**
```powershell
cd src\frontend\optim-ai-bre-ui
npm install
npm run dev
```

---

### Error: "Module not found: Can't resolve '@/components/...'"

**Cause:** TypeScript path alias `@` not configured correctly.

**Verify `tsconfig.json` has:**
```json
{
  "compilerOptions": {
    "paths": {
      "@/*": ["./src/*"]
    }
  }
}
```

**Fix:**
```powershell
# Check the file:
Get-Content "src\frontend\optim-ai-bre-ui\tsconfig.json"

# If missing, the tsconfig.json should already exist — verify it was generated.
```

---

### Error: "Parsing error: Cannot find module 'tailwindcss'"

**Cause:** Tailwind not installed.

**Fix:**
```powershell
cd src\frontend\optim-ai-bre-ui
npm install tailwindcss postcss autoprefixer --save-dev
```

---

### Error: "Error: Cannot read properties of undefined (reading 'map')"

**Cause:** API returned unexpected response. The frontend is calling the backend but got an error response.

**Fix:**
1. Check backend is running: `http://localhost:5000/health`
2. Check CORS is configured — backend allows `http://localhost:3000`
3. Check `.env.local` has correct API URL:
   ```env
   NEXT_PUBLIC_API_URL=http://localhost:5000/api/v1
   ```

---

### Error: "CORS policy: No 'Access-Control-Allow-Origin' header"

**Cause:** Frontend at `localhost:3000` calling backend at `localhost:5000` is being blocked.

**Verify in `appsettings.json`:**
```json
"AllowedOrigins": ["http://localhost:3000", "https://localhost:3000"]
```

And in `Program.cs`:
```csharp
app.UseCors(policy => policy
    .WithOrigins(builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()!)
    .AllowAnyMethod()
    .AllowAnyHeader()
    .AllowCredentials());
```

If CORS is still failing, temporarily allow all origins in dev:
```csharp
// Temporary debug only - not for production!
app.UseCors(policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
```

---

### Error: "Error: localStorage is not defined"

**Cause:** Next.js server-side rendering trying to access browser-only APIs.

**This is a known issue with the api-client.ts.** Fix by checking if running in browser:
```typescript
// In api-client.ts, change:
const token = localStorage.getItem('bre_access_token')

// To:
const token = typeof window !== 'undefined' 
  ? localStorage.getItem('bre_access_token') 
  : null
```

---

### Error: "'use client' missing" or "React hooks in Server Component"

**Cause:** Some components use hooks but don't have `'use client'` directive.

**Fix:** Add `'use client'` as the first line in any component that uses hooks, event handlers, or browser APIs.

---

## 6. API CALL ERRORS

### Error: "429 Too Many Requests"

**Cause:** Rate limiter triggered. BRE execution is limited to 1000/minute; standard APIs to 500/minute.

**Fix (dev only):** In `appsettings.Development.json`, increase rate limits:
```json
"RateLimiting": {
  "BREExecutionPerMinute": 10000,
  "StandardApiPerMinute": 5000
}
```

---

### Error: "400 Bad Request: Invalid rule definition"

**Cause:** Rule JSON structure is invalid.

**Required structure:**
```json
{
  "conditions": {
    "id": "unique-string",
    "operator": "AND",
    "rules": [
      {
        "id": "unique-string",
        "isGroup": false,
        "field": "bureau.cibil_score",
        "operator": "LESS_THAN",
        "value": 650,
        "valueType": "LITERAL"
      }
    ]
  },
  "actions": [
    {
      "id": "unique-string",
      "type": "SET_DECISION",
      "value": "REJECT"
    }
  ],
  "metadata": {
    "executionOrder": 1,
    "stopOnMatch": false,
    "errorHandling": "SKIP"
  }
}
```

**Valid operators:** `EQUALS`, `NOT_EQUALS`, `GREATER_THAN`, `LESS_THAN`, `GREATER_THAN_OR_EQUALS`, `LESS_THAN_OR_EQUALS`, `CONTAINS`, `NOT_CONTAINS`, `STARTS_WITH`, `ENDS_WITH`, `IN`, `NOT_IN`, `IS_NULL`, `IS_NOT_NULL`, `BETWEEN`, `REGEX_MATCH`

**Valid action types:** `SET_DECISION`, `SET_RISK`, `SET_TRAFFIC_LIGHT`, `ADD_DEVIATION`, `SET_FIELD_VALUE`, `ADD_TAG`, `SET_SCORE`

**Valid decisions:** `APPROVE`, `REJECT`, `DEVIATION`, `REFER`, `PENDING`

---

### Error: "404 Not Found" on /api/v1/rules/{id}

**Cause 1:** Rule ID doesn't exist.  
**Cause 2:** Rule belongs to a different tenant (tenant isolation).

**Fix:**
```powershell
# List all rules in the database (as optimai superuser):
psql -U optimai -h localhost -d optimai_bre `
    -c "SELECT id, rule_name, tenant_id, is_active FROM rules LIMIT 10;"
```

---

## 7. RULE EXECUTION ERRORS

### Error: "No published rules found for the given context"

**Cause:** Either:
1. No rules exist in the database
2. Rules exist but are in `Draft` or `Under Review` status (not `Published`)
3. Rules exist but are scoped to a different product/branch/stage

**Fix — Check rule status:**
```powershell
psql -U optimai -h localhost -d optimai_bre `
    -c "SELECT rule_name, rule_status, is_active FROM rules;"
```

**Publish a rule:**
```powershell
$token = (Invoke-RestMethod -Method POST `
    -Uri "http://localhost:5000/api/v1/auth/login" `
    -ContentType "application/json" `
    -Body '{"email":"admin@optimai.in","password":"Admin@1234"}').accessToken

# Get the rule ID:
$rules = Invoke-RestMethod -Uri "http://localhost:5000/api/v1/rules" `
    -Headers @{ "Authorization" = "Bearer $token" }

$ruleId = $rules.items[0].id

# Submit for approval (if Draft):
Invoke-RestMethod -Method POST `
    -Uri "http://localhost:5000/api/v1/rules/$ruleId/submit-for-approval" `
    -Headers @{ "Authorization" = "Bearer $token" }

# Approve (admin only):
Invoke-RestMethod -Method POST `
    -Uri "http://localhost:5000/api/v1/rules/$ruleId/approve" `
    -Headers @{ "Authorization" = "Bearer $token" }

# Publish:
Invoke-RestMethod -Method POST `
    -Uri "http://localhost:5000/api/v1/rules/$ruleId/publish" `
    -Headers @{ "Authorization" = "Bearer $token" }
```

---

### Error: Rule evaluates but gives wrong decision

**Debug — Check condition evaluation:**

Use the validate-rule endpoint to see what fields the rule accesses:
```powershell
$ruleDefinition = @{ ... } | ConvertTo-Json -Depth 10

Invoke-RestMethod -Method POST `
    -Uri "http://localhost:5000/api/v1/validate-rule" `
    -Headers @{ "Authorization" = "Bearer $token" } `
    -ContentType "application/json" `
    -Body $ruleDefinition
```

Use the simulate endpoint with `enableDetailedLogging: true`:
```json
{
  "enableDetailedLogging": true,
  "data": { ... }
}
```

Check the response for `conditionResults` array showing each condition's evaluation.

---

### Error: "Field 'bureau.cibil_score' not found in data"

**Cause:** The input JSON data structure doesn't match the field path in the rule.

**Field paths use dot notation.** If your rule uses `bureau.cibil_score`, your input data must have:
```json
{
  "data": {
    "bureau": {
      "cibil_score": 712
    }
  }
}
```

**Fix:** Check the field catalog to see expected paths:
```powershell
psql -U optimai -h localhost -d optimai_bre `
    -c "SELECT field_path, field_name, data_type FROM field_catalog ORDER BY field_path;"
```

---

## 8. DOCKER ERRORS

### Error: "Cannot connect to the Docker daemon"

**Cause:** Docker Desktop is not running.

**Fix:**
```powershell
# Start Docker Desktop application
Start-Process "C:\Program Files\Docker\Docker\Docker Desktop.exe"

# Wait 30 seconds, then verify:
docker info
```

---

### Error: "Port is already allocated" during docker compose up

**Cause:** A local service is already using the same port.

**Fix:**
```powershell
# Find what's using port 5000:
netstat -ano | findstr :5000

# Either stop the local service, or change the port in docker-compose.yml:
# Change "5000:8080" to "5001:8080" for the bre-gateway service
```

---

### Error: "bre-gateway" container keeps restarting

**Cause:** Backend failing to start (DB connection, missing env vars).

**Debug:**
```powershell
cd docker
docker compose logs bre-gateway --tail=50
```

Common fixes:
1. Database not ready — add `depends_on` with health check in docker-compose.yml
2. Wrong env vars — check `.env` file values
3. Migration failed — check logs for SQL errors

---

### Error: "image not found" for bre-gateway or bre-frontend

**Cause:** Docker images not built yet.

**Fix:**
```powershell
cd docker
docker compose build
docker compose up -d
```

---

### Error: Postgres container starts but migrations fail

**Fix — Wait for Postgres to be ready before running migrations:**
```powershell
# Wait for Postgres to accept connections:
do {
    Start-Sleep 2
    $result = docker exec $(docker compose ps -q postgres) pg_isready -U optimai 2>&1
    Write-Host "Waiting for PostgreSQL... $result"
} while ($result -notlike "*accepting connections*")

Write-Host "PostgreSQL is ready!"

# Now run migrations:
Get-Content "..\database\migrations\001_initial_schema.sql" | `
    docker exec -i $(docker compose ps -q postgres) psql -U optimai -d optimai_bre
```

---

## 9. PORT CONFLICT ERRORS

| Service | Default Port | Change In |
|---------|-------------|-----------|
| Backend API | 5000 | `launchSettings.json` or `--urls` flag |
| Frontend | 3000 | `next.config.ts` or `npm run dev -- -p 3001` |
| PostgreSQL | 5432 | `appsettings.Development.json` connection string |
| Redis | 6379 | `appsettings.Development.json` connection string |

**Check what's using a port:**
```powershell
netstat -ano | findstr :<PORT>
# Then:
taskkill /PID <PID> /F
```

**Run backend on different port:**
```powershell
dotnet run --urls "http://localhost:5001"
# Update frontend .env.local: NEXT_PUBLIC_API_URL=http://localhost:5001/api/v1
```

**Run frontend on different port:**
```powershell
npm run dev -- -p 3001
```

---

## 10. AI SERVICE ERRORS

### Error: "Azure OpenAI API key not configured"

**This is expected in local dev.** The app runs fully without AI.

**Fix — Disable AI:**

In `appsettings.Development.json`:
```json
"AiOptions": {
  "UseAzureOpenAI": false
}
```

In BRE execution request:
```json
{
  "enableAiAnalysis": false,
  "data": { ... }
}
```

---

### Error: "429 Too Many Requests" from OpenAI

**Cause:** OpenAI rate limit exceeded (happens with free tier keys).

**Fix:** The service has automatic retry with exponential backoff. If it persists, upgrade your OpenAI plan or reduce AI analysis calls.

---

### Error: "AI analysis returned null"

**Cause:** GPT-4o response was not valid JSON.

**The service returns a fallback analysis on failure** — this is expected behavior and does not break the BRE execution. The `aiAnalysis` field in the response will contain a fallback object with `isAiGenerated: false`.

---

## 11. EF CORE / MIGRATION ERRORS

### Error: "The 'dotnet-ef' tool is not installed"

**Fix:**
```powershell
dotnet tool install --global dotnet-ef
# Verify:
dotnet ef --version
```

---

### Error: "No DbContext was found in assembly"

**Fix:** Specify the DbContext project explicitly:
```powershell
dotnet ef migrations add InitialCreate `
    --project src\backend\OptimAI.BRE.RuleEngine\OptimAI.BRE.RuleEngine.csproj `
    --startup-project src\backend\OptimAI.BRE.Gateway\OptimAI.BRE.Gateway.csproj `
    --context BREDbContext
```

---

### Error: "42P07: relation already exists" in migration

**Cause:** Running `dotnet ef database update` after already running the SQL scripts.

**Fix:**
```sql
-- Mark the migration as applied without re-running:
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20240101000000_InitialCreate', '8.0.0')
ON CONFLICT DO NOTHING;
```

---

## 12. PERFORMANCE ISSUES

### Backend Slow (>2s response)

1. **Check Redis** — if Redis is down, every rule execution hits the DB:
   ```powershell
   docker exec redis-local redis-cli ping
   ```

2. **Check DB indexes** — run ANALYZE:
   ```powershell
   psql -U optimai -h localhost -d optimai_bre -c "ANALYZE;"
   ```

3. **Check slow queries:**
   ```powershell
   psql -U optimai -h localhost -d optimai_bre -c "
   SELECT query, calls, total_exec_time, mean_exec_time
   FROM pg_stat_statements
   ORDER BY mean_exec_time DESC
   LIMIT 10;"
   ```

---

### Frontend Slow to Load

1. Run `npm run build && npm run start` instead of `npm run dev` (dev mode is slower)
2. Check API response times in browser DevTools Network tab
3. TanStack Query caches data — responses after the first load should be fast

---

## 13. DIAGNOSTIC COMMANDS REFERENCE

### Full System Status Check
```powershell
Write-Host "=== OPTIM AI BRE DIAGNOSTICS ===" -ForegroundColor Cyan

Write-Host "`n[.NET]" -ForegroundColor Yellow
dotnet --version

Write-Host "`n[Node.js]" -ForegroundColor Yellow
node --version

Write-Host "`n[PostgreSQL]" -ForegroundColor Yellow
psql --version

Write-Host "`n[Backend Health]" -ForegroundColor Yellow
try {
    $health = Invoke-RestMethod -Uri "http://localhost:5000/health" -TimeoutSec 3
    Write-Host "Backend: $($health.status)" -ForegroundColor Green
} catch {
    Write-Host "Backend: NOT RUNNING" -ForegroundColor Red
}

Write-Host "`n[Frontend]" -ForegroundColor Yellow
try {
    $null = Invoke-WebRequest -Uri "http://localhost:3000" -TimeoutSec 3
    Write-Host "Frontend: RUNNING" -ForegroundColor Green
} catch {
    Write-Host "Frontend: NOT RUNNING" -ForegroundColor Red
}

Write-Host "`n[Redis]" -ForegroundColor Yellow
try {
    $pong = docker exec redis-local redis-cli ping 2>&1
    Write-Host "Redis: $pong" -ForegroundColor Green
} catch {
    Write-Host "Redis: NOT RUNNING" -ForegroundColor Red
}

Write-Host "`n[Database]" -ForegroundColor Yellow
try {
    $dbResult = psql -U optimai -h localhost -d optimai_bre -c "SELECT COUNT(*) FROM users;" 2>&1
    Write-Host "PostgreSQL: CONNECTED - $dbResult" -ForegroundColor Green
} catch {
    Write-Host "PostgreSQL: NOT CONNECTED" -ForegroundColor Red
}

Write-Host "`n=== END DIAGNOSTICS ===" -ForegroundColor Cyan
```

### Database Row Counts
```powershell
psql -U optimai -h localhost -d optimai_bre -c "
SELECT 
    'tenants' as table_name, COUNT(*) as rows FROM tenants
UNION ALL SELECT 'users', COUNT(*) FROM users
UNION ALL SELECT 'rules', COUNT(*) FROM rules
UNION ALL SELECT 'permissions', COUNT(*) FROM permissions
UNION ALL SELECT 'execution_requests', COUNT(*) FROM execution_requests
UNION ALL SELECT 'field_catalog', COUNT(*) FROM field_catalog
ORDER BY table_name;"
```

### View Recent Execution Results
```powershell
psql -U optimai -h localhost -d optimai_bre -c "
SELECT application_id, final_decision, risk_score, traffic_light, executed_at
FROM execution_results
ORDER BY executed_at DESC
LIMIT 5;"
```

### Clear Redis Cache (force rules reload from DB)
```powershell
docker exec redis-local redis-cli FLUSHDB
```

### View Backend Logs
```powershell
# If running dotnet run, logs are in console
# If running via Docker:
docker compose -f docker/docker-compose.yml logs bre-gateway -f --tail=100
```

### Test Login from Command Line
```powershell
$body = '{"email":"admin@optimai.in","password":"Admin@1234"}'
$result = Invoke-RestMethod -Method POST `
    -Uri "http://localhost:5000/api/v1/auth/login" `
    -ContentType "application/json" `
    -Body $body

if ($result.accessToken) {
    Write-Host "LOGIN SUCCESS" -ForegroundColor Green
    Write-Host "Token (first 50 chars): $($result.accessToken.Substring(0,50))..."
    Write-Host "User: $($result.user.fullName)"
    Write-Host "Permissions: $($result.user.permissions.Count)"
} else {
    Write-Host "LOGIN FAILED" -ForegroundColor Red
    $result | ConvertTo-Json
}
```

### Reset Everything (Nuclear Option)
```powershell
# WARNING: This deletes all data and starts fresh

# Drop and recreate database:
psql -U postgres -h localhost -c "DROP DATABASE IF EXISTS optimai_bre;"
psql -U postgres -h localhost -c "CREATE DATABASE optimai_bre OWNER optimai;"

# Re-run migrations:
$base = "C:\Users\Lokesh\Desktop\zerocodebreengine\optim-ai-bre\database\migrations"
psql -U optimai -h localhost -d optimai_bre -f "$base\001_initial_schema.sql"
psql -U optimai -h localhost -d optimai_bre -f "$base\002_seed_admin.sql"

# Clear Redis:
docker exec redis-local redis-cli FLUSHALL

Write-Host "Reset complete. Login with admin@optimai.in / Admin@1234"
```

---

## STILL STUCK?

If none of the above fixes work, collect this information:

```powershell
# Collect diagnostic bundle:
$out = @"
=== OPTIM AI BRE DIAGNOSTICS BUNDLE ===
Date: $(Get-Date)
.NET: $(dotnet --version)
Node: $(node --version)

=== BACKEND LOGS (last 50 lines) ===
(Copy from the terminal where dotnet run is running)

=== ERROR MESSAGE ===
(Copy the exact error message here)

=== LAST API CALL ===
(Copy the request URL, headers, and body)
(Copy the response status code and body)
"@

$out | Out-File -FilePath "C:\Users\Lokesh\Desktop\bre_diagnostics.txt"
Write-Host "Diagnostics saved to Desktop\bre_diagnostics.txt"
```

Check the GitHub Issues or contact the team with the diagnostics bundle.
