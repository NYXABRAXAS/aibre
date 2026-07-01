#!/bin/bash
# ==============================================================
# OPTIM AI BRE Engine — PostgreSQL First-Run Init Script
# Runs automatically when the postgres volume is empty.
# Applies schema + seed data in order.
# ==============================================================
set -e

echo "================================================"
echo "OPTIM AI BRE — Initializing PostgreSQL schema..."
echo "================================================"

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL

    -- Enable required extensions
    CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
    CREATE EXTENSION IF NOT EXISTS "pgcrypto";
    CREATE EXTENSION IF NOT EXISTS "pg_trgm";

EOSQL

echo "Extensions created. Running migration 001..."
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" \
    -f /docker-entrypoint-initdb.d/migrations/001_initial_schema.sql

echo "Running migration 002 (seed data)..."
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" \
    -f /docker-entrypoint-initdb.d/migrations/002_seed_admin.sql

echo "================================================"
echo "Database initialization complete!"
echo "Admin login: admin@optimai.in / Admin@1234"
echo "Demo login:  demo@demobank.in / Demo@1234"
echo "================================================"
