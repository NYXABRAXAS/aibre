-- ============================================================
-- OPTIM AI BRE ENGINE - COMPLETE DATABASE SCHEMA
-- PostgreSQL 15+
-- Multi-Tenant, Production-Grade
-- ============================================================

-- Enable extensions
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";
CREATE EXTENSION IF NOT EXISTS "pg_trgm";

-- ============================================================
-- SCHEMA: TENANTS & MULTI-TENANCY
-- ============================================================

CREATE TABLE tenants (
    id                  UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_code         VARCHAR(50) UNIQUE NOT NULL,
    tenant_name         VARCHAR(200) NOT NULL,
    display_name        VARCHAR(200),
    logo_url            TEXT,
    primary_color       VARCHAR(10) DEFAULT '#1E40AF',
    secondary_color     VARCHAR(10) DEFAULT '#3B82F6',
    plan_type           VARCHAR(50) NOT NULL DEFAULT 'ENTERPRISE',  -- STARTER, PROFESSIONAL, ENTERPRISE
    max_rules           INTEGER NOT NULL DEFAULT 1000,
    max_executions_per_day BIGINT NOT NULL DEFAULT 1000000,
    is_active           BOOLEAN NOT NULL DEFAULT TRUE,
    trial_end_date      TIMESTAMPTZ,
    subscription_end_date TIMESTAMPTZ,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by          UUID,
    settings            JSONB NOT NULL DEFAULT '{}'::JSONB
);

CREATE TABLE tenant_configurations (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id       UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    config_key      VARCHAR(200) NOT NULL,
    config_value    TEXT,
    config_type     VARCHAR(50) NOT NULL DEFAULT 'STRING', -- STRING, JSON, BOOLEAN, NUMBER
    category        VARCHAR(100),
    is_encrypted    BOOLEAN DEFAULT FALSE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(tenant_id, config_key)
);

-- ============================================================
-- SCHEMA: IDENTITY & ACCESS MANAGEMENT
-- ============================================================

CREATE TABLE users (
    id                  UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id           UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    email               VARCHAR(320) NOT NULL,
    username            VARCHAR(100) NOT NULL,
    password_hash       TEXT NOT NULL,
    full_name           VARCHAR(200) NOT NULL,
    employee_id         VARCHAR(100),
    designation         VARCHAR(200),
    department          VARCHAR(200),
    mobile              VARCHAR(20),
    profile_image_url   TEXT,
    is_active           BOOLEAN NOT NULL DEFAULT TRUE,
    is_email_verified   BOOLEAN NOT NULL DEFAULT FALSE,
    is_mfa_enabled      BOOLEAN NOT NULL DEFAULT FALSE,
    mfa_secret          TEXT,
    last_login_at       TIMESTAMPTZ,
    last_login_ip       INET,
    failed_login_count  INTEGER NOT NULL DEFAULT 0,
    locked_until        TIMESTAMPTZ,
    password_changed_at TIMESTAMPTZ,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(tenant_id, email),
    UNIQUE(tenant_id, username)
);

CREATE TABLE roles (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id       UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    role_code       VARCHAR(100) NOT NULL,
    role_name       VARCHAR(200) NOT NULL,
    description     TEXT,
    is_system_role  BOOLEAN NOT NULL DEFAULT FALSE,
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(tenant_id, role_code)
);

CREATE TABLE permissions (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    permission_code VARCHAR(200) UNIQUE NOT NULL,
    permission_name VARCHAR(200) NOT NULL,
    module          VARCHAR(100) NOT NULL,
    description     TEXT,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE role_permissions (
    role_id         UUID NOT NULL REFERENCES roles(id) ON DELETE CASCADE,
    permission_id   UUID NOT NULL REFERENCES permissions(id) ON DELETE CASCADE,
    granted_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    granted_by      UUID,
    PRIMARY KEY (role_id, permission_id)
);

CREATE TABLE user_roles (
    user_id     UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    role_id     UUID NOT NULL REFERENCES roles(id) ON DELETE CASCADE,
    assigned_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    assigned_by UUID,
    PRIMARY KEY (user_id, role_id)
);

CREATE TABLE api_keys (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id       UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    key_name        VARCHAR(200) NOT NULL,
    api_key_hash    TEXT NOT NULL UNIQUE,
    api_key_prefix  VARCHAR(20) NOT NULL,
    scopes          TEXT[] NOT NULL DEFAULT '{}',
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    expires_at      TIMESTAMPTZ,
    last_used_at    TIMESTAMPTZ,
    rate_limit_per_minute INTEGER NOT NULL DEFAULT 1000,
    created_by      UUID NOT NULL REFERENCES users(id),
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE refresh_tokens (
    id          UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id     UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    token_hash  TEXT NOT NULL UNIQUE,
    expires_at  TIMESTAMPTZ NOT NULL,
    is_revoked  BOOLEAN NOT NULL DEFAULT FALSE,
    ip_address  INET,
    user_agent  TEXT,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ============================================================
-- SCHEMA: PRODUCT & BRANCH CONFIGURATION
-- ============================================================

CREATE TABLE products (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id       UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    product_code    VARCHAR(100) NOT NULL,
    product_name    VARCHAR(200) NOT NULL,
    product_type    VARCHAR(100) NOT NULL, -- VEHICLE_LOAN, TRACTOR_LOAN, AUTO_LOAN, CV_LOAN, MSME, HOME_LOAN, PERSONAL_LOAN
    description     TEXT,
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    config          JSONB NOT NULL DEFAULT '{}'::JSONB,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(tenant_id, product_code)
);

CREATE TABLE branches (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id       UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    branch_code     VARCHAR(100) NOT NULL,
    branch_name     VARCHAR(200) NOT NULL,
    region          VARCHAR(100),
    state           VARCHAR(100),
    city            VARCHAR(100),
    zone            VARCHAR(100),
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(tenant_id, branch_code)
);

CREATE TABLE loan_stages (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id       UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    stage_code      VARCHAR(100) NOT NULL,
    stage_name      VARCHAR(200) NOT NULL,
    stage_order     INTEGER NOT NULL DEFAULT 0,
    description     TEXT,
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(tenant_id, stage_code)
);

-- ============================================================
-- SCHEMA: RULE ENGINE CORE
-- ============================================================

CREATE TABLE rule_categories (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id       UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    category_code   VARCHAR(100) NOT NULL,
    category_name   VARCHAR(200) NOT NULL,
    description     TEXT,
    icon            VARCHAR(100),
    sort_order      INTEGER NOT NULL DEFAULT 0,
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(tenant_id, category_code)
);

CREATE TABLE rules (
    id                  UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id           UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    rule_code           VARCHAR(200) NOT NULL,
    rule_name           VARCHAR(500) NOT NULL,
    description         TEXT,
    category_id         UUID REFERENCES rule_categories(id),
    rule_type           VARCHAR(100) NOT NULL,  -- ELIGIBILITY, CREDIT, BUREAU, FI, VALUATION, FRAUD, COMPLIANCE, DEVIATION, INCOME, KYC
    priority            INTEGER NOT NULL DEFAULT 100,
    is_active           BOOLEAN NOT NULL DEFAULT TRUE,
    is_published        BOOLEAN NOT NULL DEFAULT FALSE,
    is_draft            BOOLEAN NOT NULL DEFAULT TRUE,
    status              VARCHAR(50) NOT NULL DEFAULT 'DRAFT', -- DRAFT, PENDING_APPROVAL, APPROVED, PUBLISHED, ARCHIVED
    current_version_id  UUID,
    tags                TEXT[] DEFAULT '{}',
    created_by          UUID NOT NULL REFERENCES users(id),
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(tenant_id, rule_code)
);

CREATE TABLE rule_versions (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    rule_id         UUID NOT NULL REFERENCES rules(id) ON DELETE CASCADE,
    tenant_id       UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    version_number  INTEGER NOT NULL,
    version_label   VARCHAR(50),  -- e.g., v1.0, v1.1
    rule_definition JSONB NOT NULL,  -- Complete rule AST
    change_summary  TEXT,
    is_current      BOOLEAN NOT NULL DEFAULT FALSE,
    status          VARCHAR(50) NOT NULL DEFAULT 'DRAFT',
    approved_by     UUID REFERENCES users(id),
    approved_at     TIMESTAMPTZ,
    published_by    UUID REFERENCES users(id),
    published_at    TIMESTAMPTZ,
    created_by      UUID NOT NULL REFERENCES users(id),
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(rule_id, version_number)
);

-- Rule Definition JSON Structure (stored in rule_versions.rule_definition):
-- {
--   "conditions": {
--     "operator": "AND|OR",
--     "rules": [
--       {
--         "id": "uuid",
--         "field": "bureau_score",
--         "operator": "LESS_THAN",
--         "value": 650,
--         "valueType": "NUMBER"
--       },
--       {
--         "operator": "OR",
--         "rules": [...]  -- nested group
--       }
--     ]
--   },
--   "actions": [
--     { "type": "SET_DECISION", "value": "REJECT" },
--     { "type": "SET_RISK", "value": "HIGH" },
--     { "type": "ADD_DEVIATION", "value": "LOW_BUREAU_SCORE" },
--     { "type": "SET_FIELD", "field": "recommendation", "value": "Reject" }
--   ],
--   "metadata": {
--     "executionOrder": 1,
--     "stopOnMatch": true,
--     "errorHandling": "SKIP"
--   }
-- }

CREATE TABLE rule_scopes (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    rule_id         UUID NOT NULL REFERENCES rules(id) ON DELETE CASCADE,
    tenant_id       UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    scope_type      VARCHAR(50) NOT NULL, -- PRODUCT, BRANCH, STAGE, USER_ROLE, GLOBAL
    scope_value     VARCHAR(200) NOT NULL, -- product_code, branch_code, stage_code, role_code, or '*' for all
    is_excluded     BOOLEAN NOT NULL DEFAULT FALSE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE rule_sets (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id       UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    set_code        VARCHAR(200) NOT NULL,
    set_name        VARCHAR(500) NOT NULL,
    description     TEXT,
    execution_mode  VARCHAR(50) NOT NULL DEFAULT 'ALL', -- ALL, FIRST_MATCH, SCORED
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    created_by      UUID NOT NULL REFERENCES users(id),
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(tenant_id, set_code)
);

CREATE TABLE rule_set_members (
    id          UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    set_id      UUID NOT NULL REFERENCES rule_sets(id) ON DELETE CASCADE,
    rule_id     UUID NOT NULL REFERENCES rules(id) ON DELETE CASCADE,
    sort_order  INTEGER NOT NULL DEFAULT 0,
    weight      DECIMAL(5,2) DEFAULT 1.0,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(set_id, rule_id)
);

-- ============================================================
-- SCHEMA: FIELD CATALOG (Dynamic Field Mapping)
-- ============================================================

CREATE TABLE field_catalog (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id       UUID REFERENCES tenants(id) ON DELETE CASCADE, -- NULL = global
    field_path      VARCHAR(500) NOT NULL,  -- e.g., "bureau.cibil_score", "applicant.age"
    display_name    VARCHAR(200) NOT NULL,
    description     TEXT,
    data_type       VARCHAR(50) NOT NULL,   -- NUMBER, STRING, BOOLEAN, DATE, ARRAY, OBJECT
    category        VARCHAR(100),           -- BUREAU, INCOME, PERSONAL, VEHICLE, etc.
    sample_values   JSONB,
    validation_rules JSONB,
    is_system_field BOOLEAN NOT NULL DEFAULT FALSE,
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(tenant_id, field_path)
);

CREATE TABLE custom_functions (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id       UUID REFERENCES tenants(id) ON DELETE CASCADE,
    function_name   VARCHAR(200) NOT NULL,
    display_name    VARCHAR(200) NOT NULL,
    description     TEXT,
    category        VARCHAR(100),
    return_type     VARCHAR(50) NOT NULL,
    parameters      JSONB NOT NULL DEFAULT '[]'::JSONB,
    implementation  TEXT NOT NULL,  -- C# expression or script
    is_system_fn    BOOLEAN NOT NULL DEFAULT FALSE,
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(tenant_id, function_name)
);

-- ============================================================
-- SCHEMA: EXECUTION ENGINE
-- ============================================================

CREATE TABLE execution_requests (
    id                  UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id           UUID NOT NULL REFERENCES tenants(id),
    correlation_id      VARCHAR(200) UNIQUE,
    application_id      VARCHAR(200),
    product_code        VARCHAR(100),
    branch_code         VARCHAR(100),
    stage_code          VARCHAR(100),
    rule_set_id         UUID REFERENCES rule_sets(id),
    input_payload       JSONB NOT NULL,
    input_hash          VARCHAR(64),
    status              VARCHAR(50) NOT NULL DEFAULT 'PENDING', -- PENDING, PROCESSING, COMPLETED, FAILED
    priority            INTEGER NOT NULL DEFAULT 5,
    source_system       VARCHAR(200),
    api_key_id          UUID REFERENCES api_keys(id),
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    processing_started_at TIMESTAMPTZ,
    completed_at        TIMESTAMPTZ,
    processing_ms       INTEGER
);

CREATE TABLE execution_results (
    id                  UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    request_id          UUID NOT NULL REFERENCES execution_requests(id) ON DELETE CASCADE,
    tenant_id           UUID NOT NULL REFERENCES tenants(id),
    final_decision      VARCHAR(50) NOT NULL, -- APPROVE, REJECT, DEVIATION, REFER, PENDING
    risk_score          DECIMAL(5,2),
    risk_category       VARCHAR(50), -- LOW, MEDIUM, HIGH, CRITICAL
    traffic_light       VARCHAR(10), -- GREEN, AMBER, RED
    total_rules_evaluated INTEGER NOT NULL DEFAULT 0,
    rules_passed        INTEGER NOT NULL DEFAULT 0,
    rules_failed        INTEGER NOT NULL DEFAULT 0,
    rules_skipped       INTEGER NOT NULL DEFAULT 0,
    deviations_count    INTEGER NOT NULL DEFAULT 0,
    execution_ms        INTEGER,
    rule_results        JSONB NOT NULL DEFAULT '[]'::JSONB,
    field_values        JSONB NOT NULL DEFAULT '{}'::JSONB,
    ai_summary          TEXT,
    ai_analysis         JSONB,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE rule_execution_details (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    result_id       UUID NOT NULL REFERENCES execution_results(id) ON DELETE CASCADE,
    rule_id         UUID NOT NULL REFERENCES rules(id),
    rule_code       VARCHAR(200) NOT NULL,
    rule_name       VARCHAR(500) NOT NULL,
    version_number  INTEGER NOT NULL,
    execution_order INTEGER NOT NULL,
    is_matched      BOOLEAN NOT NULL,
    conditions_evaluated JSONB NOT NULL DEFAULT '[]'::JSONB,
    actions_executed JSONB NOT NULL DEFAULT '[]'::JSONB,
    execution_ms    INTEGER,
    error_message   TEXT,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ============================================================
-- SCHEMA: DEVIATIONS
-- ============================================================

CREATE TABLE deviation_types (
    id                  UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id           UUID REFERENCES tenants(id) ON DELETE CASCADE,
    deviation_code      VARCHAR(200) NOT NULL,
    deviation_name      VARCHAR(500) NOT NULL,
    category            VARCHAR(100),
    default_severity    VARCHAR(50) NOT NULL DEFAULT 'MEDIUM', -- LOW, MEDIUM, HIGH, CRITICAL
    description         TEXT,
    recommended_action  TEXT,
    requires_approval   BOOLEAN NOT NULL DEFAULT FALSE,
    approver_role       VARCHAR(200),
    is_active           BOOLEAN NOT NULL DEFAULT TRUE,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(tenant_id, deviation_code)
);

CREATE TABLE execution_deviations (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    result_id       UUID NOT NULL REFERENCES execution_results(id) ON DELETE CASCADE,
    rule_id         UUID REFERENCES rules(id),
    deviation_type_id UUID REFERENCES deviation_types(id),
    deviation_code  VARCHAR(200) NOT NULL,
    deviation_name  VARCHAR(500) NOT NULL,
    severity        VARCHAR(50) NOT NULL,
    reason          TEXT NOT NULL,
    field_path      VARCHAR(500),
    actual_value    TEXT,
    expected_value  TEXT,
    recommended_action TEXT,
    is_overridden   BOOLEAN NOT NULL DEFAULT FALSE,
    overridden_by   UUID REFERENCES users(id),
    override_reason TEXT,
    override_at     TIMESTAMPTZ,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ============================================================
-- SCHEMA: DECISION REPORTS
-- ============================================================

CREATE TABLE decision_reports (
    id                  UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    request_id          UUID NOT NULL UNIQUE REFERENCES execution_requests(id),
    tenant_id           UUID NOT NULL REFERENCES tenants(id),
    report_number       VARCHAR(100) UNIQUE,
    application_id      VARCHAR(200),
    product_code        VARCHAR(100),
    final_decision      VARCHAR(50) NOT NULL,
    risk_score          DECIMAL(5,2),
    risk_category       VARCHAR(50),
    traffic_light       VARCHAR(10),
    summary             TEXT,
    strengths           TEXT[],
    weaknesses          TEXT[],
    deviations_summary  TEXT,
    approval_recommendation TEXT,
    rejection_reasons   TEXT[],
    additional_docs     TEXT[],
    underwriting_notes  TEXT,
    report_json         JSONB,
    pdf_storage_path    TEXT,
    excel_storage_path  TEXT,
    generated_at        TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    expires_at          TIMESTAMPTZ
);

-- ============================================================
-- SCHEMA: SANDBOX & TESTING
-- ============================================================

CREATE TABLE sandbox_sessions (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id       UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    session_name    VARCHAR(200) NOT NULL,
    rule_set_id     UUID REFERENCES rule_sets(id),
    test_payload    JSONB NOT NULL,
    result          JSONB,
    status          VARCHAR(50) NOT NULL DEFAULT 'PENDING',
    created_by      UUID NOT NULL REFERENCES users(id),
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    executed_at     TIMESTAMPTZ
);

-- ============================================================
-- SCHEMA: AI ENGINE
-- ============================================================

CREATE TABLE ai_prompts (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id       UUID REFERENCES tenants(id) ON DELETE CASCADE,
    prompt_code     VARCHAR(200) NOT NULL,
    prompt_name     VARCHAR(300) NOT NULL,
    prompt_template TEXT NOT NULL,
    model           VARCHAR(100) DEFAULT 'gpt-4o',
    temperature     DECIMAL(3,2) DEFAULT 0.3,
    max_tokens      INTEGER DEFAULT 2000,
    is_system       BOOLEAN NOT NULL DEFAULT FALSE,
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(tenant_id, prompt_code)
);

CREATE TABLE ai_generated_rules (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id       UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    user_prompt     TEXT NOT NULL,
    generated_rule  JSONB NOT NULL,
    rule_id         UUID REFERENCES rules(id),
    model_used      VARCHAR(100),
    tokens_used     INTEGER,
    is_accepted     BOOLEAN,
    feedback        TEXT,
    created_by      UUID NOT NULL REFERENCES users(id),
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ============================================================
-- SCHEMA: AUDIT & COMPLIANCE
-- ============================================================

CREATE TABLE audit_logs (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id       UUID NOT NULL REFERENCES tenants(id),
    user_id         UUID REFERENCES users(id),
    action          VARCHAR(200) NOT NULL,
    entity_type     VARCHAR(100) NOT NULL,
    entity_id       VARCHAR(200),
    old_values      JSONB,
    new_values      JSONB,
    ip_address      INET,
    user_agent      TEXT,
    request_id      VARCHAR(200),
    is_success      BOOLEAN NOT NULL DEFAULT TRUE,
    error_message   TEXT,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE rule_approvals (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    rule_id         UUID NOT NULL REFERENCES rules(id) ON DELETE CASCADE,
    version_id      UUID NOT NULL REFERENCES rule_versions(id) ON DELETE CASCADE,
    tenant_id       UUID NOT NULL REFERENCES tenants(id),
    requested_by    UUID NOT NULL REFERENCES users(id),
    requested_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    reviewed_by     UUID REFERENCES users(id),
    reviewed_at     TIMESTAMPTZ,
    status          VARCHAR(50) NOT NULL DEFAULT 'PENDING', -- PENDING, APPROVED, REJECTED
    comments        TEXT,
    notification_sent BOOLEAN NOT NULL DEFAULT FALSE
);

-- ============================================================
-- SCHEMA: MARKETPLACE
-- ============================================================

CREATE TABLE marketplace_rules (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    rule_code       VARCHAR(200) UNIQUE NOT NULL,
    rule_name       VARCHAR(500) NOT NULL,
    description     TEXT,
    category        VARCHAR(100),
    product_types   TEXT[],
    rule_definition JSONB NOT NULL,
    author          VARCHAR(200),
    version         VARCHAR(50),
    downloads       INTEGER NOT NULL DEFAULT 0,
    rating          DECIMAL(3,2) DEFAULT 0,
    tags            TEXT[],
    is_featured     BOOLEAN NOT NULL DEFAULT FALSE,
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    published_at    TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE marketplace_imports (
    id                  UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id           UUID NOT NULL REFERENCES tenants(id),
    marketplace_rule_id UUID NOT NULL REFERENCES marketplace_rules(id),
    imported_rule_id    UUID REFERENCES rules(id),
    imported_by         UUID NOT NULL REFERENCES users(id),
    imported_at         TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ============================================================
-- INDEXES FOR PERFORMANCE
-- ============================================================

-- Tenant isolation indexes
CREATE INDEX idx_rules_tenant ON rules(tenant_id) WHERE is_active = TRUE;
CREATE INDEX idx_rules_tenant_category ON rules(tenant_id, category_id) WHERE is_active = TRUE;
CREATE INDEX idx_rules_tenant_type ON rules(tenant_id, rule_type) WHERE is_active = TRUE AND is_published = TRUE;
CREATE INDEX idx_rule_scopes_rule ON rule_scopes(rule_id);
CREATE INDEX idx_rule_scopes_tenant_type ON rule_scopes(tenant_id, scope_type, scope_value);

-- Execution performance indexes
CREATE INDEX idx_exec_requests_tenant_status ON execution_requests(tenant_id, status, created_at DESC);
CREATE INDEX idx_exec_requests_correlation ON execution_requests(correlation_id);
CREATE INDEX idx_exec_results_request ON execution_results(request_id);
CREATE INDEX idx_exec_results_tenant_decision ON execution_results(tenant_id, final_decision, created_at DESC);
CREATE INDEX idx_rule_exec_details_result ON rule_execution_details(result_id);

-- Audit indexes
CREATE INDEX idx_audit_tenant_entity ON audit_logs(tenant_id, entity_type, entity_id, created_at DESC);
CREATE INDEX idx_audit_tenant_user ON audit_logs(tenant_id, user_id, created_at DESC);

-- User lookup
CREATE INDEX idx_users_tenant_email ON users(tenant_id, email) WHERE is_active = TRUE;
CREATE INDEX idx_users_tenant_username ON users(tenant_id, username) WHERE is_active = TRUE;

-- Field catalog full-text
CREATE INDEX idx_field_catalog_path ON field_catalog USING GIN(to_tsvector('english', field_path || ' ' || display_name));

-- Execution date range queries
CREATE INDEX idx_exec_requests_tenant_date ON execution_requests(tenant_id, created_at DESC);
CREATE INDEX idx_decision_reports_tenant ON decision_reports(tenant_id, generated_at DESC);

-- ============================================================
-- SEED DATA: SYSTEM PERMISSIONS
-- ============================================================

INSERT INTO permissions (permission_code, permission_name, module, description) VALUES
-- Rule Management
('RULE.VIEW', 'View Rules', 'RULE_ENGINE', 'View all rules'),
('RULE.CREATE', 'Create Rules', 'RULE_ENGINE', 'Create new rules'),
('RULE.EDIT', 'Edit Rules', 'RULE_ENGINE', 'Edit existing rules'),
('RULE.DELETE', 'Delete Rules', 'RULE_ENGINE', 'Delete rules'),
('RULE.PUBLISH', 'Publish Rules', 'RULE_ENGINE', 'Publish rules to production'),
('RULE.APPROVE', 'Approve Rules', 'RULE_ENGINE', 'Approve rule changes'),
('RULE.CLONE', 'Clone Rules', 'RULE_ENGINE', 'Clone existing rules'),
('RULE.VERSION', 'Manage Rule Versions', 'RULE_ENGINE', 'View and manage rule versions'),
-- Execution
('EXECUTION.VIEW', 'View Executions', 'EXECUTION', 'View execution results'),
('EXECUTION.EXECUTE', 'Execute Rules', 'EXECUTION', 'Execute rule engine via API'),
('EXECUTION.SANDBOX', 'Use Sandbox', 'EXECUTION', 'Use sandbox testing environment'),
-- Client Management
('CLIENT.VIEW', 'View Clients', 'CLIENT_MGMT', 'View client list'),
('CLIENT.CREATE', 'Create Clients', 'CLIENT_MGMT', 'Create new clients'),
('CLIENT.EDIT', 'Edit Clients', 'CLIENT_MGMT', 'Edit client configuration'),
('CLIENT.MANAGE_USERS', 'Manage Client Users', 'CLIENT_MGMT', 'Manage users within tenant'),
-- Reports
('REPORT.VIEW', 'View Reports', 'REPORTS', 'View decision reports'),
('REPORT.EXPORT', 'Export Reports', 'REPORTS', 'Export reports to PDF/Excel'),
-- AI Engine
('AI.GENERATE', 'AI Rule Generation', 'AI_ENGINE', 'Use AI to generate rules'),
('AI.ANALYSIS', 'AI Credit Analysis', 'AI_ENGINE', 'Access AI credit analysis'),
-- Admin
('ADMIN.FULL', 'Full Admin Access', 'ADMIN', 'Complete administrative access'),
('AUDIT.VIEW', 'View Audit Logs', 'AUDIT', 'Access audit trail');

-- ============================================================
-- SEED DATA: GLOBAL FIELD CATALOG
-- ============================================================

INSERT INTO field_catalog (field_path, display_name, data_type, category, is_system_field) VALUES
-- Personal
('applicant.age', 'Applicant Age', 'NUMBER', 'PERSONAL', TRUE),
('applicant.date_of_birth', 'Date of Birth', 'DATE', 'PERSONAL', TRUE),
('applicant.gender', 'Gender', 'STRING', 'PERSONAL', TRUE),
('applicant.pan_number', 'PAN Number', 'STRING', 'KYC', TRUE),
('applicant.aadhaar_number', 'Aadhaar Number', 'STRING', 'KYC', TRUE),
('applicant.mobile', 'Mobile Number', 'STRING', 'PERSONAL', TRUE),
('applicant.email', 'Email Address', 'STRING', 'PERSONAL', TRUE),
('applicant.marital_status', 'Marital Status', 'STRING', 'PERSONAL', TRUE),
('applicant.caste_category', 'Caste Category', 'STRING', 'PERSONAL', TRUE),
-- Employment
('employment.type', 'Employment Type', 'STRING', 'EMPLOYMENT', TRUE),
('employment.employer_name', 'Employer Name', 'STRING', 'EMPLOYMENT', TRUE),
('employment.monthly_income', 'Monthly Income', 'NUMBER', 'INCOME', TRUE),
('employment.annual_income', 'Annual Income', 'NUMBER', 'INCOME', TRUE),
('employment.vintage_months', 'Employment Vintage (Months)', 'NUMBER', 'EMPLOYMENT', TRUE),
-- Bureau
('bureau.cibil_score', 'CIBIL Score', 'NUMBER', 'BUREAU', TRUE),
('bureau.experian_score', 'Experian Score', 'NUMBER', 'BUREAU', TRUE),
('bureau.equifax_score', 'Equifax Score', 'NUMBER', 'BUREAU', TRUE),
('bureau.crif_score', 'CRIF Score', 'NUMBER', 'BUREAU', TRUE),
('bureau.max_dpd_24m', 'Max DPD (24 Months)', 'NUMBER', 'BUREAU', TRUE),
('bureau.max_dpd_12m', 'Max DPD (12 Months)', 'NUMBER', 'BUREAU', TRUE),
('bureau.total_active_loans', 'Total Active Loans', 'NUMBER', 'BUREAU', TRUE),
('bureau.unsecured_outstanding', 'Unsecured Outstanding Amount', 'NUMBER', 'BUREAU', TRUE),
('bureau.secured_outstanding', 'Secured Outstanding Amount', 'NUMBER', 'BUREAU', TRUE),
('bureau.total_emi_obligation', 'Total EMI Obligation', 'NUMBER', 'BUREAU', TRUE),
('bureau.written_off_amount', 'Written Off Amount', 'NUMBER', 'BUREAU', TRUE),
('bureau.suit_filed', 'Suit Filed', 'BOOLEAN', 'BUREAU', TRUE),
('bureau.wilful_defaulter', 'Wilful Defaulter', 'BOOLEAN', 'BUREAU', TRUE),
-- Income Ratios
('ratios.foir', 'FOIR (Fixed Obligation to Income Ratio)', 'NUMBER', 'RATIOS', TRUE),
('ratios.ltv', 'LTV (Loan to Value Ratio)', 'NUMBER', 'RATIOS', TRUE),
('ratios.dscr', 'DSCR (Debt Service Coverage Ratio)', 'NUMBER', 'RATIOS', TRUE),
-- Loan
('loan.amount', 'Loan Amount', 'NUMBER', 'LOAN', TRUE),
('loan.tenure_months', 'Loan Tenure (Months)', 'NUMBER', 'LOAN', TRUE),
('loan.emi', 'EMI Amount', 'NUMBER', 'LOAN', TRUE),
('loan.interest_rate', 'Interest Rate', 'NUMBER', 'LOAN', TRUE),
('loan.purpose', 'Loan Purpose', 'STRING', 'LOAN', TRUE),
-- Vehicle
('vehicle.type', 'Vehicle Type', 'STRING', 'VEHICLE', TRUE),
('vehicle.make', 'Vehicle Make', 'STRING', 'VEHICLE', TRUE),
('vehicle.model', 'Vehicle Model', 'STRING', 'VEHICLE', TRUE),
('vehicle.year', 'Vehicle Year', 'NUMBER', 'VEHICLE', TRUE),
('vehicle.age_years', 'Vehicle Age (Years)', 'NUMBER', 'VEHICLE', TRUE),
('vehicle.valuation', 'Vehicle Valuation', 'NUMBER', 'VEHICLE', TRUE),
('vehicle.registration_number', 'Registration Number', 'STRING', 'VEHICLE', TRUE),
-- FI
('fi.verified', 'FI Verified', 'BOOLEAN', 'FI', TRUE),
('fi.residence_type', 'Residence Type', 'STRING', 'FI', TRUE),
('fi.address_match', 'Address Match', 'BOOLEAN', 'FI', TRUE),
('fi.mobile_match', 'Mobile Match', 'BOOLEAN', 'FI', TRUE),
('fi.negative', 'FI Negative', 'BOOLEAN', 'FI', TRUE),
-- GST & ITR
('gst.turnover', 'GST Turnover', 'NUMBER', 'GST', TRUE),
('gst.filing_months', 'GST Filing Months', 'NUMBER', 'GST', TRUE),
('itr.gross_income', 'ITR Gross Income', 'NUMBER', 'ITR', TRUE),
('itr.net_income', 'ITR Net Income', 'NUMBER', 'ITR', TRUE),
('itr.years_filed', 'ITR Years Filed', 'NUMBER', 'ITR', TRUE),
-- Fraud
('fraud.score', 'Fraud Score', 'NUMBER', 'FRAUD', TRUE),
('fraud.blacklisted', 'Blacklisted', 'BOOLEAN', 'FRAUD', TRUE),
('fraud.device_score', 'Device Risk Score', 'NUMBER', 'FRAUD', TRUE);

-- ============================================================
-- SEED DATA: DEVIATION TYPES
-- ============================================================

INSERT INTO deviation_types (deviation_code, deviation_name, category, default_severity, description, recommended_action) VALUES
('LOW_BUREAU_SCORE', 'Low Bureau Score', 'BUREAU', 'HIGH', 'Bureau/CIBIL score below threshold', 'Obtain credit counseling letter or additional collateral'),
('HIGH_DPD', 'High DPD', 'BUREAU', 'HIGH', 'Days Past Due exceeds acceptable limit', 'Explain previous defaults with supporting docs'),
('HIGH_FOIR', 'High FOIR', 'INCOME', 'MEDIUM', 'Fixed Obligation to Income Ratio exceeds limit', 'Consider reducing loan amount or tenure'),
('INCOME_MISMATCH', 'Income Mismatch', 'INCOME', 'MEDIUM', 'Stated income does not match verified income', 'Provide additional income proof documents'),
('NEGATIVE_FI', 'Negative FI Report', 'FI', 'HIGH', 'Field Investigation report is negative', 'Second FI verification required'),
('ADDRESS_MISMATCH', 'Address Mismatch', 'FI', 'MEDIUM', 'Applicant address does not match records', 'Obtain additional address proof'),
('MOBILE_MISMATCH', 'Mobile Number Mismatch', 'FI', 'LOW', 'Mobile number not matching bureau records', 'Verify mobile ownership'),
('HIGH_LTV', 'High LTV', 'VALUATION', 'MEDIUM', 'Loan to Value ratio exceeds limit', 'Additional down payment required'),
('VEHICLE_AGE', 'Vehicle Age Deviation', 'VEHICLE', 'MEDIUM', 'Vehicle age exceeds permissible limit', 'Obtain extended warranty or reduce tenure'),
('MULTIPLE_LOANS', 'Multiple Active Loans', 'BUREAU', 'MEDIUM', 'High number of active loans', 'Review repayment capacity'),
('WRITTEN_OFF', 'Written Off Account', 'BUREAU', 'CRITICAL', 'Previous written off account found', 'NOC from previous lender required'),
('SUIT_FILED', 'Suit Filed', 'BUREAU', 'CRITICAL', 'Legal suit filed against applicant', 'Legal verification required'),
('AGE_DEVIATION', 'Age Deviation', 'PERSONAL', 'MEDIUM', 'Applicant age outside standard limits', 'Additional life insurance required'),
('LOW_VINTAGE', 'Low Employment Vintage', 'EMPLOYMENT', 'LOW', 'Employment tenure below minimum threshold', 'Previous employment proof required'),
('LOW_GST_FILING', 'Low GST Filing Compliance', 'GST', 'MEDIUM', 'GST filing not consistent', 'Last 12 months GST returns required'),
('FRAUD_RISK', 'Fraud Risk Detected', 'FRAUD', 'CRITICAL', 'High fraud risk score detected', 'Enhanced due diligence required');

-- ============================================================
-- TRIGGERS: AUTO UPDATE TIMESTAMPS
-- ============================================================

CREATE OR REPLACE FUNCTION update_updated_at()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_tenants_updated_at BEFORE UPDATE ON tenants FOR EACH ROW EXECUTE FUNCTION update_updated_at();
CREATE TRIGGER trg_users_updated_at BEFORE UPDATE ON users FOR EACH ROW EXECUTE FUNCTION update_updated_at();
CREATE TRIGGER trg_roles_updated_at BEFORE UPDATE ON roles FOR EACH ROW EXECUTE FUNCTION update_updated_at();
CREATE TRIGGER trg_rules_updated_at BEFORE UPDATE ON rules FOR EACH ROW EXECUTE FUNCTION update_updated_at();
CREATE TRIGGER trg_rule_sets_updated_at BEFORE UPDATE ON rule_sets FOR EACH ROW EXECUTE FUNCTION update_updated_at();

-- Auto-generate report number
CREATE OR REPLACE FUNCTION generate_report_number()
RETURNS TRIGGER AS $$
BEGIN
    NEW.report_number = 'BRE-' || TO_CHAR(NOW(), 'YYYYMMDD') || '-' || LPAD(nextval('report_seq')::TEXT, 6, '0');
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE SEQUENCE IF NOT EXISTS report_seq START 1;
CREATE TRIGGER trg_decision_report_number BEFORE INSERT ON decision_reports FOR EACH ROW EXECUTE FUNCTION generate_report_number();

-- ============================================================
-- VIEWS FOR ANALYTICS
-- ============================================================

CREATE OR REPLACE VIEW v_execution_summary AS
SELECT
    er.tenant_id,
    DATE_TRUNC('day', eq.created_at) AS execution_date,
    COUNT(*) AS total_executions,
    SUM(CASE WHEN er.final_decision = 'APPROVE' THEN 1 ELSE 0 END) AS approved,
    SUM(CASE WHEN er.final_decision = 'REJECT' THEN 1 ELSE 0 END) AS rejected,
    SUM(CASE WHEN er.final_decision = 'DEVIATION' THEN 1 ELSE 0 END) AS deviations,
    ROUND(AVG(er.risk_score), 2) AS avg_risk_score,
    ROUND(AVG(eq.processing_ms), 0) AS avg_processing_ms,
    SUM(CASE WHEN er.final_decision = 'APPROVE' THEN 1 ELSE 0 END) * 100.0 / COUNT(*) AS approval_rate
FROM execution_results er
JOIN execution_requests eq ON eq.id = er.request_id
GROUP BY er.tenant_id, DATE_TRUNC('day', eq.created_at);

CREATE OR REPLACE VIEW v_rule_hit_analysis AS
SELECT
    red.rule_id,
    r.rule_code,
    r.rule_name,
    r.tenant_id,
    COUNT(*) AS total_evaluations,
    SUM(CASE WHEN red.is_matched THEN 1 ELSE 0 END) AS matched_count,
    ROUND(SUM(CASE WHEN red.is_matched THEN 1 ELSE 0 END) * 100.0 / COUNT(*), 2) AS hit_rate,
    ROUND(AVG(red.execution_ms), 0) AS avg_execution_ms
FROM rule_execution_details red
JOIN rules r ON r.id = red.rule_id
GROUP BY red.rule_id, r.rule_code, r.rule_name, r.tenant_id;

CREATE OR REPLACE VIEW v_deviation_analysis AS
SELECT
    ed.tenant_id,
    ed.deviation_code,
    ed.deviation_name,
    ed.severity,
    COUNT(*) AS occurrence_count,
    SUM(CASE WHEN ed.is_overridden THEN 1 ELSE 0 END) AS override_count,
    DATE_TRUNC('month', ed.created_at) AS month
FROM execution_deviations ed
GROUP BY ed.tenant_id, ed.deviation_code, ed.deviation_name, ed.severity, DATE_TRUNC('month', ed.created_at);
