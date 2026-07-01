using Microsoft.EntityFrameworkCore;
using OptimAI.BRE.RuleEngine.Infrastructure;
using OptimAI.BRE.Shared.Domain;

namespace OptimAI.BRE.Gateway;

/// <summary>
/// Seeds the database with initial tenants, users, roles, and permissions
/// on first startup. All operations are idempotent (safe to call on every startup).
/// </summary>
public static class DatabaseSeeder
{
    // Fixed UUIDs so seed data is deterministic and consistent across restarts
    private static readonly Guid SystemTenantId = new("a0000000-0000-0000-0000-000000000001");
    private static readonly Guid DemoBankTenantId = new("a0000000-0000-0000-0000-000000000002");
    private static readonly Guid SuperAdminRoleId = new("b0000000-0000-0000-0000-000000000001");
    private static readonly Guid CreditManagerRoleId = new("b0000000-0000-0000-0000-000000000002");
    private static readonly Guid AdminUserId = new("c0000000-0000-0000-0000-000000000001");
    private static readonly Guid DemoUserId = new("c0000000-0000-0000-0000-000000000002");

    // BCrypt hashes — work factor 12
    // Admin@1234  → $2a$12$LQv3c1yqBWVHxkd0LHAkCOYz6TtxMQJqhN8/Lewjoc.MiG6L6bHQW
    // Demo@1234   → $2a$12$9k.GJjJDiZqSVhWNi3W4C.F.5XknZ3Nuo8hHE1t3L3XuoUgJAOxFW
    private const string AdminPasswordHash = "$2a$12$LQv3c1yqBWVHxkd0LHAkCOYz6TtxMQJqhN8/Lewjoc.MiG6L6bHQW";
    private const string DemoPasswordHash  = "$2a$12$9k.GJjJDiZqSVhWNi3W4C.F.5XknZ3Nuo8hHE1t3L3XuoUgJAOxFW";

    public static async Task SeedAsync(BREDbContext db)
    {
        // Skip if already seeded (idempotency check)
        if (await db.Tenants.IgnoreQueryFilters().AnyAsync())
            return;

        // Pass 1: create permissions, tenants, users, roles
        await SeedPermissionsAsync(db);
        await SeedSystemTenantAsync(db);
        await SeedDemoBankTenantAsync(db);
        await db.SaveChangesAsync();

        // Pass 2: wire role→permission links (needs permission IDs from pass 1)
        await SeedRolePermissionsAsync(db);
    }

    // ============================================================
    // PERMISSIONS — 21 system-wide permissions
    // ============================================================
    private static async Task SeedPermissionsAsync(BREDbContext db)
    {
        if (await db.Permissions.AnyAsync()) return;

        var permissions = new[]
        {
            // Rule management
            NewPermission("RULE.VIEW",          "View Rules",              "RULES"),
            NewPermission("RULE.CREATE",         "Create Rules",            "RULES"),
            NewPermission("RULE.EDIT",           "Edit Rules",              "RULES"),
            NewPermission("RULE.DELETE",         "Delete Rules",            "RULES"),
            NewPermission("RULE.CLONE",          "Clone Rules",             "RULES"),
            NewPermission("RULE.APPROVE",        "Approve Rules",           "RULES"),
            NewPermission("RULE.PUBLISH",        "Publish Rules",           "RULES"),
            // Execution
            NewPermission("EXECUTION.VIEW",      "View Executions",         "EXECUTION"),
            NewPermission("EXECUTION.EXECUTE",   "Execute BRE",             "EXECUTION"),
            NewPermission("EXECUTION.SANDBOX",   "Use Sandbox",             "EXECUTION"),
            // Deviations
            NewPermission("DEVIATION.VIEW",      "View Deviations",         "DEVIATION"),
            NewPermission("DEVIATION.MANAGE",    "Manage Deviations",       "DEVIATION"),
            NewPermission("DEVIATION.OVERRIDE",  "Override Deviations",     "DEVIATION"),
            // AI
            NewPermission("AI.ANALYSIS",         "Run AI Analysis",         "AI"),
            NewPermission("AI.GENERATE",         "Generate AI Rules",       "AI"),
            // Reports
            NewPermission("REPORT.VIEW",         "View Reports",            "REPORTS"),
            NewPermission("REPORT.GENERATE",     "Generate Reports",        "REPORTS"),
            NewPermission("REPORT.EXPORT",       "Export Reports",          "REPORTS"),
            // Audit
            NewPermission("AUDIT.VIEW",          "View Audit Trail",        "AUDIT"),
            // Client management
            NewPermission("CLIENT.VIEW",         "View Clients",            "CLIENTS"),
            NewPermission("CLIENT.MANAGE",       "Manage Clients",          "CLIENTS"),
            // Admin
            NewPermission("ADMIN.FULL",          "Full Admin Access",       "ADMIN"),
        };

        db.Permissions.AddRange(permissions);
    }

    // ============================================================
    // SYSTEM TENANT — admin@optimai.in / Admin@1234
    // ============================================================
    private static async Task SeedSystemTenantAsync(BREDbContext db)
    {
        if (await db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.TenantCode == "SYSTEM"))
            return;

        // Tenant
        db.Tenants.Add(new Tenant
        {
            Id                   = SystemTenantId,
            TenantCode           = "SYSTEM",
            TenantName           = "OPTIM AI - System Tenant",
            DisplayName          = "System Admin",
            PlanType             = PlanType.Enterprise,
            MaxRules             = 99999,
            MaxExecutionsPerDay  = 99999999,
            IsActive             = true,
            Settings             = new Dictionary<string, object> { ["isSystem"] = true }
        });

        // Super Admin Role
        db.Roles.Add(new Role
        {
            Id           = SuperAdminRoleId,
            TenantId     = SystemTenantId,
            RoleCode     = "SUPER_ADMIN",
            RoleName     = "Super Administrator",
            Description  = "Full system access — all permissions",
            IsSystemRole = true,
            IsActive     = true
        });

        // Admin User
        db.Users.Add(new User
        {
            Id               = AdminUserId,
            TenantId         = SystemTenantId,
            Email            = "admin@optimai.in",
            Username         = "admin",
            PasswordHash     = AdminPasswordHash,
            FullName         = "System Administrator",
            Designation      = "Platform Administrator",
            Department       = "Technology",
            IsActive         = true,
            IsEmailVerified  = true
        });

        // User → Role assignment
        db.UserRoles.Add(new UserRole
        {
            UserId     = AdminUserId,
            RoleId     = SuperAdminRoleId,
            AssignedAt = DateTime.UtcNow
        });

        // Grant ALL permissions to SUPER_ADMIN (after save, in a second pass)
        // Done below via GrantAllPermissionsAsync
    }

    // ============================================================
    // DEMO BANK TENANT — demo@demobank.in / Demo@1234
    // ============================================================
    private static async Task SeedDemoBankTenantAsync(BREDbContext db)
    {
        if (await db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.TenantCode == "DEMO_BANK"))
            return;

        // Tenant
        db.Tenants.Add(new Tenant
        {
            Id                  = DemoBankTenantId,
            TenantCode          = "DEMO_BANK",
            TenantName          = "Demo Bank Ltd",
            DisplayName         = "Demo Bank",
            PlanType            = PlanType.Enterprise,
            MaxRules            = 500,
            MaxExecutionsPerDay = 100000,
            IsActive            = true,
            Settings            = new Dictionary<string, object>
            {
                ["productTypes"] = new[] { "VEHICLE_LOAN", "TRACTOR_LOAN", "MSME" }
            }
        });

        // Credit Manager Role
        db.Roles.Add(new Role
        {
            Id           = CreditManagerRoleId,
            TenantId     = DemoBankTenantId,
            RoleCode     = "CREDIT_MANAGER",
            RoleName     = "Credit Manager",
            IsSystemRole = false,
            IsActive     = true
        });

        // Demo User
        db.Users.Add(new User
        {
            Id              = DemoUserId,
            TenantId        = DemoBankTenantId,
            Email           = "demo@demobank.in",
            Username        = "demo_credit",
            PasswordHash    = DemoPasswordHash,
            FullName        = "Demo Credit Manager",
            Designation     = "Credit Manager",
            Department      = "Credit Department",
            IsActive        = true,
            IsEmailVerified = true
        });

        // User → Role
        db.UserRoles.Add(new UserRole
        {
            UserId     = DemoUserId,
            RoleId     = CreditManagerRoleId,
            AssignedAt = DateTime.UtcNow
        });

        // Rule Categories
        var categories = new[]
        {
            ("ELIGIBILITY", "Eligibility Rules", 1),
            ("BUREAU",      "Bureau Rules",       2),
            ("INCOME",      "Income Rules",       3),
            ("VEHICLE",     "Vehicle Rules",      4),
            ("FI",          "FI Rules",           5),
            ("FRAUD",       "Fraud Rules",        6),
            ("COMPLIANCE",  "Compliance Rules",   7),
        };
        foreach (var (code, name, order) in categories)
        {
            db.RuleCategories.Add(new RuleCategory
            {
                TenantId     = DemoBankTenantId,
                CategoryCode = code,
                CategoryName = name,
                SortOrder    = order,
                IsActive     = true
            });
        }

        // Loan Stages
        var stages = new[]
        {
            ("LOGIN",        "Login Stage",           1),
            ("DEDUPE",       "De-Dupe Check",         2),
            ("BUREAU_PULL",  "Bureau Pull",            3),
            ("CREDIT_EVAL",  "Credit Evaluation",     4),
            ("FI",           "Field Investigation",   5),
            ("VALUATION",    "Vehicle Valuation",     6),
            ("FINAL_CREDIT", "Final Credit Decision", 7),
            ("SANCTION",     "Sanction",              8),
        };
        foreach (var (code, name, order) in stages)
        {
            db.LoanStages.Add(new LoanStage
            {
                TenantId   = DemoBankTenantId,
                StageCode  = code,
                StageName  = name,
                StageOrder = order,
                IsActive   = true
            });
        }

        // Products
        var products = new[]
        {
            ("VL",   "Vehicle Loan",           "VEHICLE_LOAN"),
            ("TL",   "Tractor Loan",           "TRACTOR_LOAN"),
            ("AL",   "Auto Loan",              "AUTO_LOAN"),
            ("CVL",  "Commercial Vehicle Loan","CV_LOAN"),
            ("MSME", "MSME Loan",              "MSME"),
            ("PL",   "Personal Loan",          "PERSONAL_LOAN"),
        };
        foreach (var (code, name, type) in products)
        {
            db.Products.Add(new Product
            {
                TenantId    = DemoBankTenantId,
                ProductCode = code,
                ProductName = name,
                ProductType = type,
                IsActive    = true,
                Config      = new Dictionary<string, object>()
            });
        }
    }

    // ============================================================
    // Called after SaveChanges to wire up role → permission links
    // ============================================================
    public static async Task SeedRolePermissionsAsync(BREDbContext db)
    {
        var allPermissions = await db.Permissions.ToListAsync();
        if (!allPermissions.Any()) return;

        // SUPER_ADMIN gets ALL permissions
        var superAdminRoleExists = await db.Roles.AnyAsync(r => r.Id == SuperAdminRoleId);
        if (superAdminRoleExists)
        {
            var existingGranted = await db.RolePermissions
                .Where(rp => rp.RoleId == SuperAdminRoleId)
                .Select(rp => rp.PermissionId)
                .ToListAsync();

            foreach (var perm in allPermissions.Where(p => !existingGranted.Contains(p.Id)))
            {
                db.RolePermissions.Add(new RolePermission
                {
                    RoleId       = SuperAdminRoleId,
                    PermissionId = perm.Id,
                    GrantedAt    = DateTime.UtcNow
                });
            }
        }

        // CREDIT_MANAGER gets a subset of permissions
        var creditManagerCodes = new HashSet<string>
        {
            "RULE.VIEW", "RULE.CREATE", "RULE.EDIT", "RULE.CLONE",
            "EXECUTION.VIEW", "EXECUTION.EXECUTE", "EXECUTION.SANDBOX",
            "DEVIATION.VIEW",
            "AI.ANALYSIS", "AI.GENERATE",
            "REPORT.VIEW", "REPORT.EXPORT",
            "AUDIT.VIEW"
        };

        var creditManagerExists = await db.Roles.AnyAsync(r => r.Id == CreditManagerRoleId);
        if (creditManagerExists)
        {
            var cmGranted = await db.RolePermissions
                .Where(rp => rp.RoleId == CreditManagerRoleId)
                .Select(rp => rp.PermissionId)
                .ToListAsync();

            foreach (var perm in allPermissions
                .Where(p => creditManagerCodes.Contains(p.PermissionCode) && !cmGranted.Contains(p.Id)))
            {
                db.RolePermissions.Add(new RolePermission
                {
                    RoleId       = CreditManagerRoleId,
                    PermissionId = perm.Id,
                    GrantedAt    = DateTime.UtcNow
                });
            }
        }

        await db.SaveChangesAsync();
    }

    private static Permission NewPermission(string code, string name, string module) => new()
    {
        PermissionCode = code,
        PermissionName = name,
        Module         = module
    };
}
