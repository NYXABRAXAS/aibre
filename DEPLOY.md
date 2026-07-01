# OPTIM AI BRE Engine — Deployment Guide

Deploy to any server in 4 steps. No Node.js, .NET, PostgreSQL, or Redis installation required.
Everything runs inside Docker containers.

---

## STEP 1 — Install Docker

### Windows Server / Windows 11
```
Download: https://www.docker.com/products/docker-desktop/
Run the installer → Restart when prompted → Docker Desktop starts automatically
```

Verify:
```powershell
docker --version
docker compose version
```

### Ubuntu / Debian Linux
```bash
curl -fsSL https://get.docker.com | sh
sudo usermod -aG docker $USER
newgrp docker
```

Verify:
```bash
docker --version
docker compose version
```

### Amazon Linux 2 (AWS EC2)
```bash
sudo yum update -y
sudo amazon-linux-extras install docker -y
sudo service docker start
sudo usermod -aG docker ec2-user
sudo curl -L "https://github.com/docker/compose/releases/latest/download/docker-compose-$(uname -s)-$(uname -m)" -o /usr/local/bin/docker-compose
sudo chmod +x /usr/local/bin/docker-compose
```

### Azure VM (Ubuntu)
Same as Ubuntu above.

### DigitalOcean / Render / Any VPS (Ubuntu)
Same as Ubuntu above.

---

## STEP 2 — Copy Project Files

### Option A — Copy from your machine to the server
```bash
# From your local machine (replace SERVER_IP):
scp -r optim-ai-bre/ user@SERVER_IP:~/optim-ai-bre/

# On the server:
cd ~/optim-ai-bre
```

### Option B — Clone from Git
```bash
git clone https://your-repo-url/optim-ai-bre.git
cd optim-ai-bre
```

### Option C — Copy files directly (Windows to Server)
Use WinSCP, FileZilla, or any SFTP client to copy the `optim-ai-bre` folder to the server.

---

### Configure Environment Variables

```bash
# Copy the example env file:
cp docker/.env.example docker/.env
```

Edit `docker/.env` — change all `CHANGE_ME` values:
```bash
nano docker/.env
```

**Required changes:**
```env
POSTGRES_PASSWORD=YourStrongDatabasePassword123!
REDIS_PASSWORD=YourStrongRedisPassword456!
JWT_SECRET_KEY=YourRandomStringMinimum32CharsLong789!
APP_DOMAIN=http://YOUR_SERVER_IP
```

**To generate a strong JWT secret key:**
```bash
# Linux/Mac:
openssl rand -base64 48

# Windows PowerShell:
-join ((65..90)+(97..122)+(48..57) | Get-Random -Count 64 | % {[char]$_})
```

---

## STEP 3 — Run

```bash
docker compose up -d
```

**That's it.** This command:
- Builds the .NET 8 backend from source (~3 minutes)
- Builds the Next.js frontend from source (~2 minutes)
- Starts PostgreSQL and creates the database schema automatically
- Starts Redis with authentication
- Starts Nginx as the reverse proxy
- Starts the daily database backup service

**Watch the startup progress:**
```bash
docker compose logs -f
```

**Check all services are running:**
```bash
docker compose ps
```

Expected output:
```
NAME             STATUS                   PORTS
bre-nginx        running (healthy)        0.0.0.0:80->80/tcp
bre-backend      running (healthy)
bre-frontend     running (healthy)
bre-postgres     running (healthy)
bre-redis        running (healthy)
bre-db-backup    running
```

---

## STEP 4 — Open the Application

```
http://YOUR_SERVER_IP
```

| What | URL |
|------|-----|
| Application | `http://YOUR_SERVER_IP` |
| API Documentation (Swagger) | `http://YOUR_SERVER_IP/swagger` |
| Health Check | `http://YOUR_SERVER_IP/health` |

**Default Login Credentials:**
```
Admin Account:
  Email:    admin@optimai.in
  Password: Admin@1234

Demo Bank Account:
  Email:    demo@demobank.in
  Password: Demo@1234
```

**Change the admin password** after first login from Settings → Profile → Change Password.

---

## SSL / HTTPS Setup (Optional)

After you have a domain name pointing to your server:

### Option A — Let's Encrypt (Free SSL)

```bash
# Install certbot:
sudo apt install certbot -y

# Get certificate (replace with your domain):
sudo certbot certonly --standalone -d your-domain.com

# Copy certificates to project:
sudo cp /etc/letsencrypt/live/your-domain.com/fullchain.pem docker/ssl/
sudo cp /etc/letsencrypt/live/your-domain.com/privkey.pem docker/ssl/
```

Uncomment the HTTPS server block in `docker/nginx.conf`:
- Replace `your-domain.com` with your actual domain
- Uncomment the SSL volume mount in `docker-compose.yml`
- Uncomment `return 301 https://$host$request_uri;` in the HTTP server block

```bash
# Update APP_DOMAIN in docker/.env:
APP_DOMAIN=https://your-domain.com

# Restart nginx:
docker compose restart bre-nginx
```

### Option B — Cloudflare (Free SSL without cert management)
Point your domain to Cloudflare, set SSL mode to "Flexible".
Cloudflare handles HTTPS termination. Your server only needs port 80 open.

---

## Useful Commands

```bash
# View all container status:
docker compose ps

# View logs for a specific service:
docker compose logs bre-backend -f --tail=100
docker compose logs bre-frontend -f --tail=100
docker compose logs bre-nginx -f --tail=100
docker compose logs bre-postgres -f --tail=50

# Restart a specific service:
docker compose restart bre-backend
docker compose restart bre-nginx

# Restart all services:
docker compose restart

# Stop all services:
docker compose stop

# Stop and remove containers (data is preserved in volumes):
docker compose down

# Stop and remove containers AND all data (complete reset):
docker compose down -v

# Connect to database:
docker exec -it bre-postgres psql -U optimai -d optimai_bre

# Connect to Redis:
docker exec -it bre-redis redis-cli -a YOUR_REDIS_PASSWORD

# Clear Redis cache (forces rules to reload from DB):
docker exec bre-redis redis-cli -a YOUR_REDIS_PASSWORD FLUSHDB
```

---

## Updating the Application

```bash
# Pull latest code:
git pull

# Rebuild and restart:
docker compose up -d --build

# Or rebuild a specific service:
docker compose up -d --build bre-backend
docker compose up -d --build bre-frontend
```

---

## Database Backup & Restore

**Backups are automatic** — the `bre-db-backup` container runs daily at startup and keeps 7 days of backups.

**Backup location (inside container):** `/backups/`

**Access backup files:**
```bash
# List backups:
docker exec bre-db-backup ls -la /backups/

# Copy a backup to the host:
docker cp bre-db-backup:/backups/bre_backup_20240101_020000.sql.gz ./

# Manual backup now:
docker exec bre-db-backup sh -c '
  pg_dump -h bre-postgres -U $POSTGRES_USER -d $POSTGRES_DB | gzip > /backups/manual_$(date +%Y%m%d_%H%M%S).sql.gz
'

# Restore from backup:
gunzip < bre_backup_20240101_020000.sql.gz | \
  docker exec -i bre-postgres psql -U optimai -d optimai_bre
```

---

## Server Requirements

| Component | Minimum | Recommended |
|-----------|---------|-------------|
| CPU | 2 cores | 4+ cores |
| RAM | 4 GB | 8 GB |
| Disk | 20 GB | 50 GB SSD |
| OS | Ubuntu 20.04+ / Windows Server 2019+ | Ubuntu 22.04 LTS |
| Ports | 80, 443 open in firewall | 80, 443 |

**Open firewall ports:**
```bash
# Ubuntu UFW:
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
sudo ufw enable

# AWS EC2: Add inbound rules in Security Group for port 80 and 443
# Azure VM: Add inbound port rules in Network Security Group
# DigitalOcean: Add firewall rules in the control panel
```

---

## Troubleshooting

**Container not starting:**
```bash
docker compose logs bre-backend --tail=50
```

**Database connection error:**
```bash
# Check postgres is healthy:
docker compose ps bre-postgres

# Check the POSTGRES_PASSWORD in docker/.env matches what backend expects
```

**502 Bad Gateway from Nginx:**
```bash
# Backend not ready yet — wait 60 seconds after start
docker compose logs bre-backend --tail=20
```

**Port 80 already in use:**
```bash
# Find what's using port 80:
sudo lsof -i :80      # Linux
netstat -ano | findstr :80   # Windows

# Stop it, or change nginx port in docker-compose.yml:
# ports: - "8080:80"
# Then access via: http://SERVER_IP:8080
```

**Out of memory:**
```bash
# Check memory usage:
docker stats

# Add more RAM to the server, or reduce postgres shared_buffers:
# Edit docker-compose.yml → bre-postgres command → -c shared_buffers=128MB
```
