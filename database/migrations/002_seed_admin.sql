-- ============================================================
-- OPTIM AI BRE ENGINE - SEED: ADMIN TENANT + USER
-- Run AFTER 001_initial_schema.sql
-- Password: Admin@1234  (BCrypt hash below)
-- ============================================================

-- Step 1: Create the default SYSTEM tenant
INSERT INTO tenants (
    id, tenant_code, tenant_name, display_name,
    plan_type, max_rules, max_executions_per_day,
    is_active, settings
) VALUES (
    'a0000000-0000-0000-0000-000000000001',
    'SYSTEM',
    'OPTIM AI - System Tenant',
    'System Admin',
    'ENTERPRISE',
    99999,
    99999999,
    TRUE,
    '{"isSystem": true}'::jsonb
) ON CONFLICT (tenant_code) DO NOTHING;

-- Step 2: Create SUPER_ADMIN role for the system tenant
INSERT INTO roles (
    id, tenant_id, role_code, role_name, description,
    is_system_role, is_active
) VALUES (
    'b0000000-0000-0000-0000-000000000001',
    'a0000000-0000-0000-0000-000000000001',
    'SUPER_ADMIN',
    'Super Administrator',
    'Full system access - all permissions',
    TRUE, TRUE
) ON CONFLICT DO NOTHING;

-- Step 3: Grant ALL permissions to SUPER_ADMIN
INSERT INTO role_permissions (role_id, permission_id)
SELECT
    'b0000000-0000-0000-0000-000000000001',
    p.id
FROM permissions p
ON CONFLICT DO NOTHING;

-- Step 4: Create the admin user
-- Email: admin@optimai.in
-- Password: Admin@1234
-- BCrypt hash generated with work factor 12
INSERT INTO users (
    id, tenant_id, email, username, password_hash,
    full_name, designation, department,
    is_active, is_email_verified
) VALUES (
    'c0000000-0000-0000-0000-000000000001',
    'a0000000-0000-0000-0000-000000000001',
    'admin@optimai.in',
    'admin',
    '$2a$12$LQv3c1yqBWVHxkd0LHAkCOYz6TtxMQJqhN8/Lewjoc.MiG6L6bHQW',
    'System Administrator',
    'Platform Administrator',
    'Technology',
    TRUE,
    TRUE
) ON CONFLICT (tenant_id, email) DO NOTHING;

-- Step 5: Assign SUPER_ADMIN role to admin user
INSERT INTO user_roles (user_id, role_id)
VALUES (
    'c0000000-0000-0000-0000-000000000001',
    'b0000000-0000-0000-0000-000000000001'
) ON CONFLICT DO NOTHING;

-- Step 6: Create a DEMO tenant for testing multi-tenancy
INSERT INTO tenants (
    id, tenant_code, tenant_name, display_name,
    plan_type, max_rules, max_executions_per_day, is_active, settings
) VALUES (
    'a0000000-0000-0000-0000-000000000002',
    'DEMO_BANK',
    'Demo Bank Ltd',
    'Demo Bank',
    'ENTERPRISE', 500, 100000, TRUE,
    '{"productTypes": ["VEHICLE_LOAN","TRACTOR_LOAN","MSME"]}'::jsonb
) ON CONFLICT (tenant_code) DO NOTHING;

-- Step 7: Create CREDIT_MANAGER role for Demo Bank
INSERT INTO roles (id, tenant_id, role_code, role_name, is_system_role, is_active)
VALUES (
    'b0000000-0000-0000-0000-000000000002',
    'a0000000-0000-0000-0000-000000000002',
    'CREDIT_MANAGER', 'Credit Manager', FALSE, TRUE
) ON CONFLICT DO NOTHING;

-- Step 8: Grant rule permissions to CREDIT_MANAGER
INSERT INTO role_permissions (role_id, permission_id)
SELECT 'b0000000-0000-0000-0000-000000000002', p.id
FROM permissions p
WHERE p.permission_code IN (
    'RULE.VIEW','RULE.CREATE','RULE.EDIT','RULE.CLONE',
    'EXECUTION.VIEW','EXECUTION.EXECUTE','EXECUTION.SANDBOX',
    'REPORT.VIEW','REPORT.EXPORT','AI.GENERATE','AI.ANALYSIS',
    'AUDIT.VIEW'
) ON CONFLICT DO NOTHING;

-- Step 9: Create demo user for Demo Bank
-- Email: demo@demobank.in
-- Password: Demo@1234
INSERT INTO users (
    id, tenant_id, email, username, password_hash,
    full_name, designation, department, is_active, is_email_verified
) VALUES (
    'c0000000-0000-0000-0000-000000000002',
    'a0000000-0000-0000-0000-000000000002',
    'demo@demobank.in',
    'demo_credit',
    '$2a$12$9k.GJjJDiZqSVhWNi3W4C.F.5XknZ3Nuo8hHE1t3L3XuoUgJAOxFW',
    'Demo Credit Manager',
    'Credit Manager',
    'Credit Department',
    TRUE, TRUE
) ON CONFLICT (tenant_id, email) DO NOTHING;

-- Step 10: Assign CREDIT_MANAGER role to demo user
INSERT INTO user_roles (user_id, role_id)
VALUES (
    'c0000000-0000-0000-0000-000000000002',
    'b0000000-0000-0000-0000-000000000002'
) ON CONFLICT DO NOTHING;

-- Step 11: Seed rule categories for Demo Bank
INSERT INTO rule_categories (id, tenant_id, category_code, category_name, icon, sort_order, is_active)
VALUES
    (uuid_generate_v4(), 'a0000000-0000-0000-0000-000000000002', 'ELIGIBILITY', 'Eligibility Rules', '✅', 1, TRUE),
    (uuid_generate_v4(), 'a0000000-0000-0000-0000-000000000002', 'BUREAU', 'Bureau Rules', '📊', 2, TRUE),
    (uuid_generate_v4(), 'a0000000-0000-0000-0000-000000000002', 'INCOME', 'Income Rules', '💰', 3, TRUE),
    (uuid_generate_v4(), 'a0000000-0000-0000-0000-000000000002', 'VEHICLE', 'Vehicle Rules', '🚗', 4, TRUE),
    (uuid_generate_v4(), 'a0000000-0000-0000-0000-000000000002', 'FI', 'FI Rules', '🏠', 5, TRUE),
    (uuid_generate_v4(), 'a0000000-0000-0000-0000-000000000002', 'FRAUD', 'Fraud Rules', '🚨', 6, TRUE),
    (uuid_generate_v4(), 'a0000000-0000-0000-0000-000000000002', 'COMPLIANCE', 'Compliance Rules', '📋', 7, TRUE)
ON CONFLICT DO NOTHING;

-- Step 12: Seed loan stages for Demo Bank
INSERT INTO loan_stages (id, tenant_id, stage_code, stage_name, stage_order, is_active)
VALUES
    (uuid_generate_v4(), 'a0000000-0000-0000-0000-000000000002', 'LOGIN', 'Login Stage', 1, TRUE),
    (uuid_generate_v4(), 'a0000000-0000-0000-0000-000000000002', 'DEDUPE', 'De-Dupe Check', 2, TRUE),
    (uuid_generate_v4(), 'a0000000-0000-0000-0000-000000000002', 'BUREAU_PULL', 'Bureau Pull', 3, TRUE),
    (uuid_generate_v4(), 'a0000000-0000-0000-0000-000000000002', 'CREDIT_EVAL', 'Credit Evaluation', 4, TRUE),
    (uuid_generate_v4(), 'a0000000-0000-0000-0000-000000000002', 'FI', 'Field Investigation', 5, TRUE),
    (uuid_generate_v4(), 'a0000000-0000-0000-0000-000000000002', 'VALUATION', 'Vehicle Valuation', 6, TRUE),
    (uuid_generate_v4(), 'a0000000-0000-0000-0000-000000000002', 'FINAL_CREDIT', 'Final Credit Decision', 7, TRUE),
    (uuid_generate_v4(), 'a0000000-0000-0000-0000-000000000002', 'SANCTION', 'Sanction', 8, TRUE)
ON CONFLICT DO NOTHING;

-- Step 13: Seed products for Demo Bank
INSERT INTO products (id, tenant_id, product_code, product_name, product_type, is_active, config)
VALUES
    (uuid_generate_v4(), 'a0000000-0000-0000-0000-000000000002', 'VL', 'Vehicle Loan', 'VEHICLE_LOAN', TRUE, '{}'::jsonb),
    (uuid_generate_v4(), 'a0000000-0000-0000-0000-000000000002', 'TL', 'Tractor Loan', 'TRACTOR_LOAN', TRUE, '{}'::jsonb),
    (uuid_generate_v4(), 'a0000000-0000-0000-0000-000000000002', 'AL', 'Auto Loan', 'AUTO_LOAN', TRUE, '{}'::jsonb),
    (uuid_generate_v4(), 'a0000000-0000-0000-0000-000000000002', 'CVL', 'Commercial Vehicle Loan', 'CV_LOAN', TRUE, '{}'::jsonb),
    (uuid_generate_v4(), 'a0000000-0000-0000-0000-000000000002', 'MSME', 'MSME Loan', 'MSME', TRUE, '{}'::jsonb),
    (uuid_generate_v4(), 'a0000000-0000-0000-0000-000000000002', 'PL', 'Personal Loan', 'PERSONAL_LOAN', TRUE, '{}'::jsonb)
ON CONFLICT DO NOTHING;

-- ============================================================
-- VERIFICATION QUERIES (run to confirm seed data)
-- ============================================================
-- SELECT * FROM tenants;
-- SELECT u.email, u.full_name, r.role_name FROM users u
--   JOIN user_roles ur ON ur.user_id = u.id
--   JOIN roles r ON r.id = ur.role_id;
-- SELECT COUNT(*) FROM permissions;
