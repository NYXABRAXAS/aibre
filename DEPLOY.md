# OPTIM AI BRE Engine — Deployment Guide

---

## RENDER DEPLOYMENT (Recommended — Easiest)

Deploy the complete platform to Render in 4 steps.
No server setup. No Docker installation. No manual configuration.

---

### STEP 1 — Push to GitHub

```bash
cd optim-ai-bre

git init
git add .
git commit -m "Render Ready"
git branch -M main
git remote add origin https://github.com/YOUR_USERNAME/YOUR_REPO.git
git push -u origin main
```

---

### STEP 2 — Create a Render Blueprint

1. Go to **https://dashboard.render.com**
2. Click **New** → **Blueprint**
3. Connect your GitHub repository
4. Render reads `render.yaml` and shows you the services to create:
   - `optim-ai-bre-api` — .NET 8 backend
   - `optim-ai-bre-ui` — Next.js frontend
   - `optim-ai-bre-db`  — PostgreSQL 15
   - `optim-ai-bre-redis` — Redis

5. Click **Apply**

---

### STEP 3 — Deploy

Render builds and deploys all services automatically.

**Build time:** ~5–8 minutes (first build compiles .NET + Next.js from source)

Watch progress in the Render dashboard under each service's **Logs** tab.

---

### STEP 4 — Open the Application

| What | URL |
|------|-----|
| Application | `https://optim-ai-bre-ui.onrender.com` |
| API Swagger | `https://optim-ai-bre-api.onrender.com/swagger` |
| Health Check | `https://optim-ai-bre-api.onrender.com/health` |

**Default login credentials:**
```
Admin:  admin@optimai.in  /  Admin@1234
Demo:   demo@demobank.in  /  Demo@1234
```

---

## WHAT RENDER CONFIGURES AUTOMATICALLY

When you deploy via render.yaml, Render automatically:

| Variable | Value |
|----------|-------|
| `DATABASE_URL` | PostgreSQL connection URL (injected from managed DB) |
| `REDIS_URL` | Redis connection URL (injected from managed Redis) |
| `Jwt__SecretKey` | Strong random 32+ character string (auto-generated) |
| `NEXT_PUBLIC_API_URL` | `https://optim-ai-bre-api.onrender.com/api/v1` (baked at build) |
| CORS origins | Frontend URL automatically added to allowed origins |

**You don't need to set any environment variables manually.**

---

## ENABLE AI FEATURES (Optional)

AI features are disabled by default (app works fully without them).

To enable AI credit analysis and rule generation:

1. Go to Render Dashboard → `optim-ai-bre-api` → **Environment**
2. Set these variables:

**Option A — OpenAI:**
```
AiOptions__UseAzureOpenAI = false
AiOptions__ApiKey         = sk-your-openai-key-here
```

**Option B — Azure OpenAI:**
```
AiOptions__UseAzureOpenAI = true
AiOptions__Endpoint       = https://YOUR-RESOURCE.openai.azure.com/
AiOptions__ApiKey         = your-azure-openai-key
```

3. Click **Save Changes** → service redeploys automatically

---

## SCALE UP (For Production)

Render Starter plan (free):
- 512 MB RAM, 0.1 CPU
- Spins down after 15 min of inactivity (cold start = 30–60 sec)

For production, upgrade in Render Dashboard → service → **Settings** → **Plan**:

| Service | Recommended Plan | RAM | CPU |
|---------|-----------------|-----|-----|
| `optim-ai-bre-api` | Standard ($25/mo) | 2GB | 1 CPU |
| `optim-ai-bre-ui` | Starter ($7/mo) | 512MB | 0.5 CPU |
| `optim-ai-bre-db` | Standard ($22/mo) | 1GB | — |
| `optim-ai-bre-redis` | Standard ($10/mo) | 1GB | — |

---

## CUSTOM DOMAIN

1. Render Dashboard → `optim-ai-bre-ui` → **Settings** → **Custom Domains**
2. Add your domain (e.g., `bre.yourcompany.com`)
3. Add a CNAME record at your DNS provider pointing to the Render URL
4. Render provisions a free SSL certificate automatically

After adding the custom domain, update CORS:
1. Render Dashboard → `optim-ai-bre-api` → **Environment**
2. Add: `AllowedOrigins__2 = https://bre.yourcompany.com`
3. Save → redeploys automatically

---

## GIT WORKFLOW (Continuous Deployment)

After initial deploy, any push to `main` triggers automatic redeployment:

```bash
# Make changes to your code
git add .
git commit -m "Update rule engine logic"
git push origin main
# Render automatically rebuilds and deploys
```

---

## VIEW LOGS

```
Backend logs:  Render Dashboard → optim-ai-bre-api  → Logs
Frontend logs: Render Dashboard → optim-ai-bre-ui   → Logs
DB logs:       Render Dashboard → optim-ai-bre-db   → Logs
```

---

## SELF-HOSTED DOCKER DEPLOYMENT

If you prefer to host on your own server (AWS, Azure, DigitalOcean, etc.):

### STEP 1 — Install Docker
```bash
# Ubuntu/Debian
curl -fsSL https://get.docker.com | sh
sudo usermod -aG docker $USER && newgrp docker

# Verify
docker --version && docker compose version
```

### STEP 2 — Configure Environment
```bash
cp docker/.env.example docker/.env
nano docker/.env   # Change all CHANGE_ME values
```

Minimum required changes in `docker/.env`:
```env
POSTGRES_PASSWORD=YourStrongPassword123!
REDIS_PASSWORD=YourRedisPassword456!
JWT_SECRET_KEY=YourRandomStringMinimum32CharactersLong
APP_DOMAIN=http://YOUR_SERVER_IP
```

### STEP 3 — Deploy

```bash
docker compose up -d
```

This single command:
- Builds .NET 8 backend from source
- Builds Next.js frontend from source
- Starts PostgreSQL + Redis
- Initializes database schema + seed data automatically
- Starts Nginx reverse proxy on port 80

### STEP 4 — Open

```
http://YOUR_SERVER_IP
```

---

## ALL URLS AT A GLANCE

| Deployment | Frontend | API | Swagger |
|------------|----------|-----|---------|
| Render | `https://optim-ai-bre-ui.onrender.com` | `https://optim-ai-bre-api.onrender.com` | `/swagger` |
| Docker local | `http://localhost:3000` | `http://localhost:5000` | `/swagger` |
| Docker server | `http://SERVER_IP` | `http://SERVER_IP/api` | `/swagger` |

---

## TROUBLESHOOTING

**Backend shows "Database initialization failed"**
→ The PostgreSQL service may still be starting. Wait 30 seconds and retry.
→ Check Render dashboard: `optim-ai-bre-db` must show **Available** status before API starts.

**Frontend shows blank page**
→ `NEXT_PUBLIC_API_URL` was baked incorrectly at build time.
→ In render.yaml, verify `dockerBuildArgs` has the correct backend URL.
→ Trigger a manual redeploy of `optim-ai-bre-ui`.

**"503 Service Unavailable" on first request**
→ Free tier Render services sleep after 15 min. The first request wakes them up (takes 30–60 seconds).
→ Upgrade to paid plan to eliminate cold starts.

**Login returns 401 with correct credentials**
→ Seed data was not applied. Check backend logs for "Seeding initial data..." message.
→ If missing, trigger a manual redeploy of `optim-ai-bre-api`.

**CORS error in browser**
→ Verify `AllowedOrigins__0` in backend env matches the frontend URL exactly (including https://).

**Redis connection failed**
→ Backend will log a warning but continue running. Rule caching is disabled; rules load from DB each time (slower but functional).
