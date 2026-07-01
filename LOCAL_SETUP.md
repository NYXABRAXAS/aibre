# OPTIM AI BRE Engine — Complete Local Setup Guide

> Analyzed from actual project source code. Every command is specific to this project.

---

## TABLE OF CONTENTS

1. [Software Prerequisites](#1-software-prerequisites)
2. [Folder Structure](#2-folder-structure)
3. [Option A — Run With Docker (Easiest)](#3-option-a--run-with-docker-easiest)
4. [Option B — Run Manually (Without Docker)](#4-option-b--run-manually-without-docker)
5. [Database Setup](#5-database-setup)
6. [Environment Variables & Configuration](#6-environment-variables--configuration)
7. [Start Backend](#7-start-backend)
8. [Start Frontend](#8-start-frontend)
9. [Swagger API Documentation](#9-swagger-api-documentation)
10. [Default Credentials](#10-default-credentials)
11. [Create First Admin User](#11-create-first-admin-user)
12. [Postman Testing Guide](#12-postman-testing-guide)
13. [Verify Everything Works](#13-verify-everything-works)
14. [AI Service Configuration](#14-ai-service-configuration)

---

## 1. SOFTWARE PREREQUISITES

Install the following exactly — the project will NOT work with wrong versions.

### 1.1 .NET 8 SDK
```
Version required: .NET 8.0 (any 8.x patch)
Download: https://dotnet.microsoft.com/en-us/download/dotnet/8.0
```

**Install & verify:**
```powershell
# Download and install .NET 8 SDK from Microsoft website
# After install, verify:
dotnet --version
# Must show: 8.0.x
```

### 1.2 Node.js
```
Version required: 20.x LTS (project uses Next.js 14.1)
Download: https://nodejs.org/en/download (select LTS)
```

**Verify:**
```powershell
node --version
# Must show: v20.x.x

npm --version
# Must show: 10.x.x
```

### 1.3 PostgreSQL
```
Version required: 15 or 16
Download: https://www.postgresql.org/download/windows/
Installer: EDB PostgreSQL installer (includes pgAdmin 4)
```

**During installation, remember:**
- Port: `5432` (default)
- Superuser password: set this to something you remember
- Include pgAdmin 4 (check the box)

**Verify:**
```powershell
psql --version
# Must show: psql (PostgreSQL) 15.x or 16.x
```

### 1.4 Redis
Redis does not have a native Windows installer. Two options:

**Option A — Redis via Docker Desktop (recommended for Windows):**
```powershell
# Install Docker Desktop first (see 1.5), then:
docker run -d --name redis-local -p 6379:6379 redis:7-alpine
```

**Option B — Redis for Windows (Memurai or WSL):**
```
Memurai (Redis-compatible for Windows): https://www.memurai.com/
Download free developer version and install.
Default port: 6379, no password in dev mode.
```

**Verify Redis is running:**
```powershell
# If using Docker:
docker exec -it redis-local redis-cli ping
# Should return: PONG

# If using Memurai: it runs as a Windows service automatically
```

### 1.5 Docker Desktop (Optional but recommended)
```
Version: Latest stable
Download: https://www.docker.com/products/docker-desktop/
```

**Verify:**
```powershell
docker --version
# Must show: Docker version 25.x or higher

docker compose version
# Must show: Docker Compose version v2.x
```

### 1.6 Git
```powershell
git --version
# Must show: git version 2.x
```

### 1.7 Summary Checklist
```
[ ] .NET 8 SDK — dotnet --version → 8.0.x
[ ] Node.js 20 LTS — node --version → v20.x.x
[ ] PostgreSQL 15/16 — psql --version → 15.x or 16.x
[ ] Redis 7 — via Docker or Memurai
[ ] Docker Desktop — optional but useful
```

---

## 2. FOLDER STRUCTURE

```
optim-ai-bre/                          ← PROJECT ROOT
│
├── src/
│   ├── backend/                       ← ALL .NET 8 BACKEND CODE
│   │   ├── OptimAI.BRE.sln            ← Visual Studio Solution file
│   │   │
│   │   ├── OptimAI.BRE.Gateway/       ← MAIN ENTRY POINT (runs on port 5000)
│   │   │   ├── Program.cs             ← App startup, DI, middleware
│   │   │   ├── appsettings.json       ← Base configuration
│   │   │   ├── appsettings.Development.json  ← Local dev overrides
│   │   │   └── OptimAI.BRE.Gateway.csproj
│   │   │
│   │   ├── OptimAI.BRE.Shared/        ← Domain entities, base classes
│   │   │   └── Domain/Entities.cs     ← All C# domain models
│   │   │
│   │   ├── OptimAI.BRE.RuleEngine/    ← Core rule execution engine
│   │   │   ├── Domain/                ← Interfaces, execution context
│   │   │   ├── Application/           ← RuleExecutionService, ConditionEvaluator
│   │   │   │                            RiskScoringEngine, ActionExecutor
│   │   │   └── Infrastructure/        ← BREDbContext, CachedRuleLoader
│   │   │                                RepositoryStubs, TenantMiddleware
│   │   │
│   │   ├── OptimAI.BRE.RuleDesigner/  ← Rule CRUD, versioning, approval
│   │   ├── OptimAI.BRE.AIEngine/      ← Azure OpenAI integration
│   │   ├── OptimAI.BRE.IdentityService/ ← JWT auth, login, token refresh
│   │   ├── OptimAI.BRE.ClientMgmt/    ← Tenant/client management
│   │   ├── OptimAI.BRE.AuditService/  ← Audit trail
│   │   └── OptimAI.BRE.ReportService/ ← Decision report generation
│   │
│   └── frontend/
│       └── optim-ai-bre-ui/           ← NEXT.JS 14 FRONTEND (runs on port 3000)
│           ├── package.json           ← Node dependencies
│           ├── tsconfig.json          ← TypeScript config
│           ├── tailwind.config.js     ← Tailwind CSS config
│           ├── next.config.ts         ← Next.js config
│           └── src/
│               ├── app/               ← Next.js App Router pages
│               ├── components/        ← React components
│               │   ├── rule-builder/  ← Visual rule editor
│               │   ├── dashboard/     ← Analytics dashboard
│               │   ├── sandbox/       ← Rule testing sandbox
│               │   ├── ai-analyst/    ← AI credit analysis
│               │   ├── deviation/     ← Deviation management
│               │   └── layout/        ← App shell, navigation
│               ├── lib/               ← API client (axios)
│               └── types/             ← TypeScript types
│
├── database/
│   └── migrations/
│       ├── 001_initial_schema.sql     ← Tables, indexes, triggers, seed data
│       └── 002_seed_admin.sql         ← Admin user, demo tenant, test data
│
├── docker/
│   ├── docker-compose.yml             ← Full stack Docker Compose
│   ├── Dockerfile.backend             ← .NET 8 Docker image
│   ├── Dockerfile.frontend            ← Next.js Docker image
│   └── .env.example                   ← Template for .env file
│
└── k8s/
    └── manifests/deployment.yaml      ← Kubernetes deployment configs
```

---

## 3. OPTION A — RUN WITH DOCKER (EASIEST)

If you have Docker Desktop installed, this starts everything in one command.

### Step 1 — Copy environment file
```powershell
cd C:\Users\Lokesh\Desktop\zerocodebreengine\optim-ai-bre\docker
Copy-Item .env.example .env
```

### Step 2 — Edit .env file (minimum required changes)
```powershell
notepad .env
```

Change these values in .env:
```env
POSTGRES_PASSWORD=YourPassword123
REDIS_PASSWORD=YourRedisPass123
JWT_SECRET_KEY=OptimAIBRESecretKey2024MinimumThirtyTwoChars
AZURE_OPENAI_ENDPOINT=
AZURE_OPENAI_KEY=
USE_AZURE_OPENAI=false
```

### Step 3 — Start all services
```powershell
cd C:\Users\Lokesh\Desktop\zerocodebreengine\optim-ai-bre\docker

docker compose up -d
```

### Step 4 — Check all containers are running
```powershell
docker compose ps
```

Expected output:
```
NAME              STATUS    PORTS
bre-gateway       running   0.0.0.0:5000->8080/tcp
bre-frontend      running   0.0.0.0:3000->3000/tcp
postgres          running   0.0.0.0:5432->5432/tcp
redis             running   0.0.0.0:6379->6379/tcp
elasticsearch     running   0.0.0.0:9200->9200/tcp
```

### Step 5 — Run database migrations
```powershell
# Wait 15 seconds for Postgres to be ready, then:
docker exec -i $(docker compose ps -q postgres) psql -U optimai -d optimai_bre -f /dev/stdin < ..\database\migrations\001_initial_schema.sql

docker exec -i $(docker compose ps -q postgres) psql -U optimai -d optimai_bre -f /dev/stdin < ..\database\migrations\002_seed_admin.sql
```

### Step 6 — Access the application
```
Frontend UI:  http://localhost:3000
Backend API:  http://localhost:5000
Swagger:      http://localhost:5000/swagger
```

**Skip to Section 10 for login credentials.**

---

## 4. OPTION B — RUN MANUALLY (WITHOUT DOCKER)

Use this if you prefer to install PostgreSQL and Redis natively.

### Prerequisites check
```powershell
dotnet --version    # 8.0.x
node --version      # v20.x.x
psql --version      # 15.x or 16.x
```

---

## 5. DATABASE SETUP

### Step 5.1 — Create PostgreSQL user and database

Open **pgAdmin 4** (installed with PostgreSQL) OR use the command line.

**Using Command Line (PowerShell as Administrator):**
```powershell
# Connect as the postgres superuser
psql -U postgres -h localhost

# Inside psql prompt, run these commands:
CREATE USER optimai WITH PASSWORD 'dev_password';
CREATE DATABASE optimai_bre OWNER optimai;
GRANT ALL PRIVILEGES ON DATABASE optimai_bre TO optimai;
\q
```

**Using pgAdmin 4 (GUI):**
1. Open pgAdmin 4
2. Right-click "Login/Group Roles" → Create → Login/Group Role
   - Name: `optimai`
   - Password: `dev_password`
   - Privileges: Can login ✓
3. Right-click "Databases" → Create → Database
   - Name: `optimai_bre`
   - Owner: `optimai`

### Step 5.2 — Run schema migration (001_initial_schema.sql)

This creates all 25 tables, indexes, triggers, and seed data.

```powershell
psql -U optimai -h localhost -d optimai_bre -f "C:\Users\Lokesh\Desktop\zerocodebreengine\optim-ai-bre\database\migrations\001_initial_schema.sql"
```

**When prompted for password:** `dev_password`

**Expected output:**
```
CREATE EXTENSION
CREATE EXTENSION
CREATE EXTENSION
CREATE TABLE   ← (repeated ~25 times)
CREATE INDEX   ← (repeated ~15 times)
INSERT 0 21    ← permissions inserted
INSERT 0 62    ← field catalog entries inserted
INSERT 0 16    ← deviation types inserted
CREATE FUNCTION
CREATE TRIGGER
CREATE VIEW
```

### Step 5.3 — Run seed data (002_seed_admin.sql)

This creates admin user, demo bank tenant, categories, and stages.

```powershell
psql -U optimai -h localhost -d optimai_bre -f "C:\Users\Lokesh\Desktop\zerocodebreengine\optim-ai-bre\database\migrations\002_seed_admin.sql"
```

**Expected output:**
```
INSERT 0 1     ← system tenant created
INSERT 0 1     ← super_admin role created
INSERT 0 21    ← all permissions granted
INSERT 0 1     ← admin user created
INSERT 0 1     ← role assigned to admin
INSERT 0 1     ← demo bank tenant created
...etc
```

### Step 5.4 — Verify database setup

```powershell
psql -U optimai -h localhost -d optimai_bre -c "SELECT tenant_name, tenant_code FROM tenants;"
```

Expected:
```
      tenant_name        | tenant_code
--------------------------+-------------
 OPTIM AI - System Tenant | SYSTEM
 Demo Bank Ltd            | DEMO_BANK
```

```powershell
psql -U optimai -h localhost -d optimai_bre -c "SELECT email, full_name FROM users;"
```

Expected:
```
       email        |       full_name
--------------------+------------------------
 admin@optimai.in   | System Administrator
 demo@demobank.in   | Demo Credit Manager
```

---

## 6. ENVIRONMENT VARIABLES & CONFIGURATION

The backend reads from `appsettings.json` and `appsettings.Development.json`.

**File location:**
```
src\backend\OptimAI.BRE.Gateway\appsettings.Development.json
```

**Current values (these work for local dev without changes):**
```json
{
  "ConnectionStrings": {
    "PostgreSQL": "Host=localhost;Port=5432;Database=optimai_bre;Username=optimai;Password=dev_password",
    "Redis": "localhost:6379"
  },
  "Jwt": {
    "SecretKey": "DEV_SECRET_KEY_CHANGE_IN_PRODUCTION_MIN_32_CHARS",
    "Issuer": "OptimAI.BRE",
    "Audience": "OptimAI.BRE.Clients",
    "AccessTokenExpiryMinutes": 480,
    "RefreshTokenExpiryDays": 30
  },
  "AiOptions": {
    "Endpoint": "",
    "ApiKey": "",
    "ModelName": "gpt-4o",
    "UseAzureOpenAI": false
  }
}
```

**If your PostgreSQL password is different:**
```powershell
# Edit the file and change dev_password to your actual password
notepad "C:\Users\Lokesh\Desktop\zerocodebreengine\optim-ai-bre\src\backend\OptimAI.BRE.Gateway\appsettings.Development.json"
```

**Frontend environment:**

Create a `.env.local` file in the frontend folder:
```powershell
New-Item -ItemType File "C:\Users\Lokesh\Desktop\zerocodebreengine\optim-ai-bre\src\frontend\optim-ai-bre-ui\.env.local"
```

Add this content:
```env
NEXT_PUBLIC_API_URL=http://localhost:5000/api/v1
NEXT_TELEMETRY_DISABLED=1
```

---

## 7. START BACKEND

### Step 7.1 — Start Redis (if not already running)

**Using Docker:**
```powershell
docker run -d --name redis-local -p 6379:6379 redis:7-alpine
```

**Using Memurai (Windows native):**
```powershell
# Memurai runs as Windows service automatically after install
# Verify it's running:
Get-Service -Name Memurai
```

**Verify Redis:**
```powershell
docker exec redis-local redis-cli ping
# Should return: PONG
```

### Step 7.2 — Restore NuGet packages

```powershell
cd C:\Users\Lokesh\Desktop\zerocodebreengine\optim-ai-bre\src\backend

dotnet restore OptimAI.BRE.sln
```

Wait for this to complete. It downloads all NuGet packages (~500MB first time).

### Step 7.3 — Build the solution

```powershell
cd C:\Users\Lokesh\Desktop\zerocodebreengine\optim-ai-bre\src\backend

dotnet build OptimAI.BRE.sln --configuration Debug
```

**Expected:** `Build succeeded. 0 Error(s)`

### Step 7.4 — Run EF Core database migration (first time)

The backend auto-migrates on startup. But to create EF Core migration files first:

```powershell
cd C:\Users\Lokesh\Desktop\zerocodebreengine\optim-ai-bre\src\backend\OptimAI.BRE.Gateway

# Install EF Core tools if not installed:
dotnet tool install --global dotnet-ef

# Create initial migration:
dotnet ef migrations add InitialCreate --project ..\OptimAI.BRE.RuleEngine\OptimAI.BRE.RuleEngine.csproj --startup-project .

# Apply migration to database:
dotnet ef database update --project ..\OptimAI.BRE.RuleEngine\OptimAI.BRE.RuleEngine.csproj --startup-project .
```

> **NOTE:** If you already ran the SQL files in Step 5, the tables are already created.
> The EF migration will detect existing tables and skip or error.
> In that case, mark the migration as applied:
> ```powershell
> dotnet ef database update --project ..\OptimAI.BRE.RuleEngine\OptimAI.BRE.RuleEngine.csproj --startup-project . --connection "Host=localhost;Database=optimai_bre;Username=optimai;Password=dev_password"
> ```

### Step 7.5 — Start the backend

```powershell
cd C:\Users\Lokesh\Desktop\zerocodebreengine\optim-ai-bre\src\backend\OptimAI.BRE.Gateway

set ASPNETCORE_ENVIRONMENT=Development
dotnet run --launch-profile Development
```

**Expected startup output:**
```
[HH:mm:ss INF] Starting OptimAI BRE Engine...
[HH:mm:ss INF] Now listening on: http://localhost:5000
[HH:mm:ss INF] Application started. Press Ctrl+C to shut down.
```

**Backend is now running at:** `http://localhost:5000`

---

## 8. START FRONTEND

Open a **NEW** PowerShell terminal window.

### Step 8.1 — Install Node dependencies
```powershell
cd C:\Users\Lokesh\Desktop\zerocodebreengine\optim-ai-bre\src\frontend\optim-ai-bre-ui

npm install
```

This installs: Next.js 14, React 18, Recharts, dnd-kit, Monaco Editor, TanStack Query, Tailwind CSS, TypeScript.

First install takes 2–3 minutes.

### Step 8.2 — Start the frontend dev server
```powershell
cd C:\Users\Lokesh\Desktop\zerocodebreengine\optim-ai-bre\src\frontend\optim-ai-bre-ui

npm run dev
```

**Expected output:**
```
   ▲ Next.js 14.1.0
   - Local:        http://localhost:3000
   - Environments: .env.local

 ✓ Ready in 2.3s
```

**Frontend is now running at:** `http://localhost:3000`

---

## 9. SWAGGER API DOCUMENTATION

Swagger UI is enabled in `Development` and `Staging` environments.

```
URL:  http://localhost:5000/swagger
```

**What you'll see:**
- Full API documentation for all endpoints
- Try-it-out for each endpoint
- Bearer token authentication
- Request/response schemas

**Swagger endpoints documented:**
```
POST  /api/v1/auth/login              ← Get JWT token
POST  /api/v1/auth/refresh            ← Refresh token
POST  /api/v1/execute-bre             ← Run BRE decision engine
POST  /api/v1/validate-rule           ← Validate rule definition
POST  /api/v1/simulate-decision       ← Sandbox simulation
GET   /api/v1/decisions               ← Get decision history
GET   /api/v1/rules                   ← List all rules
POST  /api/v1/rules                   ← Create new rule
GET   /api/v1/rules/{id}              ← Get rule by ID
PUT   /api/v1/rules/{id}              ← Update rule
POST  /api/v1/rules/{id}/publish      ← Publish rule
POST  /api/v1/rules/{id}/clone        ← Clone rule
POST  /api/v1/ai/generate-rule        ← AI rule generation
POST  /api/v1/ai/analyze-credit       ← AI credit analysis
GET   /health                         ← Health check
GET   /health/ready                   ← Readiness probe
```

---

## 10. DEFAULT CREDENTIALS

### Admin Account (System Tenant)
```
URL:      http://localhost:3000
Email:    admin@optimai.in
Password: Admin@1234
Tenant:   SYSTEM (Super Admin - all permissions)
```

### Demo Account (Demo Bank Tenant)
```
URL:      http://localhost:3000
Email:    demo@demobank.in
Password: Demo@1234
Tenant:   DEMO_BANK (Credit Manager permissions)
```

### PostgreSQL
```
Host:     localhost
Port:     5432
Database: optimai_bre
Username: optimai
Password: dev_password
```

### Redis
```
Host:     localhost
Port:     6379
Password: (none in dev mode)
```

---

## 11. CREATE FIRST ADMIN USER

### Method 1 — SQL (Fastest)

The seed file (002_seed_admin.sql) already created the admin user.
If you need to create a NEW additional user:

```sql
-- Connect to database first:
-- psql -U optimai -h localhost -d optimai_bre

-- Replace YOUR_TENANT_ID with the actual UUID from tenants table:
-- SELECT id FROM tenants WHERE tenant_code = 'DEMO_BANK';

INSERT INTO users (
    tenant_id,
    email,
    username,
    password_hash,
    full_name,
    designation,
    is_active,
    is_email_verified
) VALUES (
    'REPLACE_WITH_TENANT_UUID',
    'newuser@demobank.in',
    'newuser',
    -- BCrypt hash of 'Password@123':
    '$2a$12$92IXUNpkjO0rOQ5byMi.Ye4oKoEa3Ro9llC/.og/at2.uheWG/igi',
    'New Credit Manager',
    'Credit Manager',
    TRUE,
    TRUE
);

-- Get the new user's ID and assign role:
INSERT INTO user_roles (user_id, role_id)
SELECT u.id, r.id
FROM users u, roles r
WHERE u.email = 'newuser@demobank.in'
AND r.role_code = 'CREDIT_MANAGER';
```

### Method 2 — API Endpoint (After login)

```powershell
# First login to get token:
$login = Invoke-RestMethod -Method POST `
    -Uri "http://localhost:5000/api/v1/auth/login" `
    -ContentType "application/json" `
    -Body '{"email":"admin@optimai.in","password":"Admin@1234"}'

$token = $login.accessToken

# Create new user (implement the endpoint in ClientMgmt controller):
Invoke-RestMethod -Method POST `
    -Uri "http://localhost:5000/api/v1/users" `
    -Headers @{ "Authorization" = "Bearer $token" } `
    -ContentType "application/json" `
    -Body '{"email":"manager@bank.in","fullName":"Branch Manager","password":"Password@123","roleCode":"CREDIT_MANAGER"}'
```

### BCrypt Password Hash Generator

To generate a bcrypt hash for a new password, run this one-liner:

```powershell
# Install BCrypt tool:
dotnet tool install --global BCrypt.Net.Tool 2>$null

# Generate hash (replace YourPassword with actual password):
dotnet bcrypt hash "YourPassword@123"
```

Or use this small C# script:
```powershell
$script = @"
using BCrypt.Net;
Console.WriteLine(BCrypt.HashPassword("Admin@1234", 12));
"@

# Save to temp file and run:
$script | Out-File -FilePath "$env:TEMP\hashpwd.csx" -Encoding UTF8
dotnet script "$env:TEMP\hashpwd.csx"
```

---

## 12. POSTMAN TESTING GUIDE

### Import Collection

Create a new Postman Collection named **"OPTIM AI BRE"** with these requests:

### Request 1 — Login
```
Method:  POST
URL:     http://localhost:5000/api/v1/auth/login
Headers: Content-Type: application/json
Body (raw JSON):
{
  "email": "admin@optimai.in",
  "password": "Admin@1234"
}
```

**Save the `accessToken` from the response as a collection variable named `{{token}}`**

In Postman:
- Go to Collection → Variables
- Add variable: `token` = (paste the accessToken value)

### Request 2 — Execute BRE (Core API)
```
Method:  POST
URL:     http://localhost:5000/api/v1/execute-bre
Headers:
  Content-Type: application/json
  Authorization: Bearer {{token}}
Body (raw JSON):
{
  "applicationId": "APP-2024-001",
  "productCode": "VL",
  "branchCode": "MUM-001",
  "stageCode": "CREDIT_EVAL",
  "enableAiAnalysis": false,
  "data": {
    "applicant": {
      "age": 34,
      "gender": "MALE",
      "pan_number": "ABCDE1234F"
    },
    "employment": {
      "type": "SALARIED",
      "employer_name": "Infosys Ltd",
      "monthly_income": 85000,
      "vintage_months": 48
    },
    "bureau": {
      "cibil_score": 712,
      "max_dpd_24m": 0,
      "total_active_loans": 2,
      "total_emi_obligation": 22000,
      "written_off_amount": 0,
      "suit_filed": false,
      "wilful_defaulter": false
    },
    "loan": {
      "amount": 750000,
      "tenure_months": 60
    },
    "vehicle": {
      "age_years": 0,
      "valuation": 85000
    },
    "ratios": {
      "foir": 45.8,
      "ltv": 88.2
    },
    "fi": {
      "verified": true,
      "negative": false,
      "address_match": true,
      "mobile_match": true
    },
    "fraud": {
      "score": 12,
      "blacklisted": false
    }
  }
}
```

### Request 3 — Create a Rule
```
Method:  POST
URL:     http://localhost:5000/api/v1/rules
Headers:
  Content-Type: application/json
  Authorization: Bearer {{token}}
Body (raw JSON):
{
  "ruleName": "Low CIBIL Score Reject",
  "ruleCode": "low_cibil_reject",
  "ruleType": "Bureau",
  "priority": 10,
  "tags": ["bureau", "cibil", "eligibility"],
  "ruleDefinition": {
    "conditions": {
      "id": "root-group",
      "operator": "AND",
      "rules": [
        {
          "id": "cond-001",
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
        "id": "action-001",
        "type": "SET_DECISION",
        "value": "REJECT"
      },
      {
        "id": "action-002",
        "type": "SET_RISK",
        "value": "HIGH"
      },
      {
        "id": "action-003",
        "type": "ADD_DEVIATION",
        "value": "LOW_BUREAU_SCORE",
        "parameters": {
          "severity": "HIGH",
          "reason": "CIBIL score below minimum threshold of 650"
        }
      }
    ],
    "metadata": {
      "executionOrder": 1,
      "stopOnMatch": false,
      "errorHandling": "SKIP"
    }
  }
}
```

### Request 4 — List Rules
```
Method:  GET
URL:     http://localhost:5000/api/v1/rules?page=1&pageSize=20
Headers:
  Authorization: Bearer {{token}}
```

### Request 5 — Simulate Decision (Sandbox)
```
Method:  POST
URL:     http://localhost:5000/api/v1/simulate-decision
Headers:
  Content-Type: application/json
  Authorization: Bearer {{token}}
Body:
{
  "data": {
    "bureau": { "cibil_score": 580, "max_dpd_24m": 60 },
    "employment": { "monthly_income": 45000 },
    "ratios": { "foir": 72 },
    "fraud": { "blacklisted": false, "score": 25 }
  }
}
```

### Request 6 — Health Check (no auth needed)
```
Method:  GET
URL:     http://localhost:5000/health
```

Expected response:
```json
{
  "status": "Healthy",
  "results": {
    "postgresql": { "status": "Healthy" },
    "redis": { "status": "Healthy" }
  }
}
```

---

## 13. VERIFY EVERYTHING WORKS

Run these checks in order.

### ✅ Check 1 — Backend Health
```powershell
Invoke-RestMethod -Uri "http://localhost:5000/health"
# Expected: status = "Healthy"
```

### ✅ Check 2 — Login Works
```powershell
$response = Invoke-RestMethod -Method POST `
    -Uri "http://localhost:5000/api/v1/auth/login" `
    -ContentType "application/json" `
    -Body '{"email":"admin@optimai.in","password":"Admin@1234"}'

Write-Host "Token: $($response.accessToken.Substring(0,50))..."
Write-Host "User: $($response.user.fullName)"
Write-Host "Tenant: $($response.user.tenantId)"
Write-Host "Permissions: $($response.user.permissions.Count)"
# Expected: Token printed, User = System Administrator, 21 permissions
```

### ✅ Check 3 — Rule Execution Works

Save the token first:
```powershell
$token = (Invoke-RestMethod -Method POST `
    -Uri "http://localhost:5000/api/v1/auth/login" `
    -ContentType "application/json" `
    -Body '{"email":"admin@optimai.in","password":"Admin@1234"}').accessToken

$body = @{
    applicationId = "TEST-001"
    data = @{
        bureau = @{ cibil_score = 580; max_dpd_24m = 45 }
        employment = @{ monthly_income = 35000 }
        ratios = @{ foir = 68 }
        fi = @{ verified = $true; negative = $false }
        fraud = @{ blacklisted = $false; score = 15 }
    }
} | ConvertTo-Json -Depth 5

$result = Invoke-RestMethod -Method POST `
    -Uri "http://localhost:5000/api/v1/execute-bre" `
    -Headers @{ "Authorization" = "Bearer $token" } `
    -ContentType "application/json" `
    -Body $body

Write-Host "Decision: $($result.decision)"
Write-Host "Risk Score: $($result.riskScore)"
Write-Host "Traffic Light: $($result.trafficLight)"
Write-Host "Rules Evaluated: $($result.totalRulesEvaluated)"
Write-Host "Execution Time: $($result.executionMs)ms"
```

### ✅ Check 4 — Frontend Loads
```
Open browser: http://localhost:3000
Expected: Dashboard loads with navigation sidebar
```

### ✅ Check 5 — Swagger Loads
```
Open browser: http://localhost:5000/swagger
Expected: Swagger UI with OPTIM AI BRE Engine API docs
```

### ✅ Check 6 — Rule Builder Works
```
1. Open http://localhost:3000/rules
2. Click "Create Rule"
3. Add a condition: bureau.cibil_score < 650
4. Add an action: SET_DECISION = REJECT
5. Click "Preview" tab — should show human-readable rule
6. Click "JSON View" tab — should show rule JSON
```

### ✅ Check 7 — Multi-Tenant Works
```powershell
# Login as demo bank user:
$demo = Invoke-RestMethod -Method POST `
    -Uri "http://localhost:5000/api/v1/auth/login" `
    -ContentType "application/json" `
    -Body '{"email":"demo@demobank.in","password":"Demo@1234"}'

Write-Host "Demo user tenant: $($demo.user.tenantId)"
# Should be different from admin tenant ID

# Demo user should NOT see system tenant rules
$rules = Invoke-RestMethod `
    -Uri "http://localhost:5000/api/v1/rules" `
    -Headers @{ "Authorization" = "Bearer $($demo.accessToken)" }

Write-Host "Demo bank rules: $($rules.totalCount)"
# Rules are tenant-isolated — demo user only sees their own rules
```

### ✅ Check 8 — Sandbox Works
```
1. Open http://localhost:3000/sandbox
2. Edit the JSON in the Monaco editor (left panel)
3. Click "▶ Run Simulation"
4. See Decision, Risk Score, and Rule Results on the right
```

---

## 14. AI SERVICE CONFIGURATION

AI features (Rule Generator, Credit Analysis) require OpenAI or Azure OpenAI.

### Option A — Use OpenAI (api.openai.com)

Edit `appsettings.Development.json`:
```json
"AiOptions": {
  "Endpoint": "",
  "ApiKey": "sk-your-openai-key-here",
  "ModelName": "gpt-4o",
  "UseAzureOpenAI": false
}
```

Get API key from: https://platform.openai.com/api-keys

### Option B — Use Azure OpenAI

```json
"AiOptions": {
  "Endpoint": "https://YOUR-RESOURCE.openai.azure.com/",
  "ApiKey": "your-azure-openai-key",
  "ModelName": "gpt-4o",
  "UseAzureOpenAI": true
}
```

### Option C — Disable AI (for local testing without API key)

Edit `appsettings.Development.json`:
```json
"RuleEngine": {
  "EnableAiByDefault": false
}
```

And in execute-bre request, send:
```json
{ "enableAiAnalysis": false }
```

AI features gracefully degrade when no key is configured — the BRE engine still works fully.

---

## QUICK REFERENCE — ALL URLS

| Service | URL | Notes |
|---------|-----|-------|
| Frontend | http://localhost:3000 | Next.js UI |
| Backend API | http://localhost:5000 | .NET 8 Gateway |
| Swagger | http://localhost:5000/swagger | API Docs |
| Health Check | http://localhost:5000/health | Service status |
| pgAdmin 4 | http://localhost:5050 | DB Admin (Docker only) |
| Redis | localhost:6379 | Cache |
| PostgreSQL | localhost:5432 | Database |

---

## QUICK REFERENCE — ALL COMMANDS

```powershell
# ---- DATABASE ----
psql -U optimai -h localhost -d optimai_bre                     # Connect to DB
psql -U optimai -h localhost -d optimai_bre -f "001_initial_schema.sql"
psql -U optimai -h localhost -d optimai_bre -f "002_seed_admin.sql"

# ---- BACKEND ----
cd src\backend
dotnet restore OptimAI.BRE.sln
dotnet build OptimAI.BRE.sln
cd OptimAI.BRE.Gateway
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run

# ---- FRONTEND ----
cd src\frontend\optim-ai-bre-ui
npm install
npm run dev

# ---- REDIS (Docker) ----
docker run -d --name redis-local -p 6379:6379 redis:7-alpine
docker exec redis-local redis-cli ping

# ---- FULL DOCKER STACK ----
cd docker
docker compose up -d
docker compose ps
docker compose logs bre-gateway -f

# ---- STOP DOCKER ----
docker compose down

# ---- STOP DOCKER + DELETE DATA ----
docker compose down -v
```
