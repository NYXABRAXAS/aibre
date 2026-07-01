using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using OptimAI.BRE.Shared.Domain;
using System.Text.Json;

namespace OptimAI.BRE.RuleEngine.Infrastructure;

public class BREDbContext : DbContext
{
    public BREDbContext(DbContextOptions<BREDbContext> options) : base(options) { }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantConfiguration> TenantConfigurations => Set<TenantConfiguration>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<LoanStage> LoanStages => Set<LoanStage>();
    public DbSet<RuleCategory> RuleCategories => Set<RuleCategory>();
    public DbSet<Rule> Rules => Set<Rule>();
    public DbSet<RuleVersion> RuleVersions => Set<RuleVersion>();
    public DbSet<RuleScope> RuleScopes => Set<RuleScope>();
    public DbSet<RuleSet> RuleSets => Set<RuleSet>();
    public DbSet<RuleSetMember> RuleSetMembers => Set<RuleSetMember>();
    public DbSet<FieldCatalogEntry> FieldCatalog => Set<FieldCatalogEntry>();
    public DbSet<ExecutionRequest> ExecutionRequests => Set<ExecutionRequest>();
    public DbSet<ExecutionResult> ExecutionResults => Set<ExecutionResult>();
    public DbSet<RuleExecutionDetail> RuleExecutionDetails => Set<RuleExecutionDetail>();
    public DbSet<DeviationType> DeviationTypes => Set<DeviationType>();
    public DbSet<ExecutionDeviation> ExecutionDeviations => Set<ExecutionDeviation>();
    public DbSet<DecisionReportEntity> DecisionReports => Set<DecisionReportEntity>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<RuleApproval> RuleApprovals => Set<RuleApproval>();
    public DbSet<AiGeneratedRuleEntity> AiGeneratedRules => Set<AiGeneratedRuleEntity>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        base.OnModelCreating(model);

        // ---- TENANT ----
        model.Entity<Tenant>(e =>
        {
            e.HasKey(t => t.Id);
            e.HasIndex(t => t.TenantCode).IsUnique();
            e.Property(t => t.Settings)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null)!)
                .HasColumnType("jsonb");
        });

        // ---- USER ----
        model.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);
            e.HasIndex(u => new { u.TenantId, u.Email }).IsUnique();
            e.HasIndex(u => new { u.TenantId, u.Username }).IsUnique();
            e.HasOne(u => u.Tenant).WithMany(t => t.Users).HasForeignKey(u => u.TenantId);
        });

        // ---- ROLE ----
        model.Entity<Role>(e =>
        {
            e.HasKey(r => r.Id);
            e.HasIndex(r => new { r.TenantId, r.RoleCode }).IsUnique();
        });

        model.Entity<RolePermission>(e =>
        {
            e.HasKey(rp => new { rp.RoleId, rp.PermissionId });
            e.HasOne(rp => rp.Role).WithMany(r => r.RolePermissions).HasForeignKey(rp => rp.RoleId);
            e.HasOne(rp => rp.Permission).WithMany().HasForeignKey(rp => rp.PermissionId);
        });

        model.Entity<UserRole>(e =>
        {
            e.HasKey(ur => new { ur.UserId, ur.RoleId });
            e.HasOne(ur => ur.User).WithMany(u => u.UserRoles).HasForeignKey(ur => ur.UserId);
            e.HasOne(ur => ur.Role).WithMany(r => r.UserRoles).HasForeignKey(ur => ur.RoleId);
        });

        // ---- RULE ----
        model.Entity<Rule>(e =>
        {
            e.HasKey(r => r.Id);
            e.HasIndex(r => new { r.TenantId, r.RuleCode }).IsUnique();
            e.HasIndex(r => new { r.TenantId, r.RuleType }).HasFilter("is_active = true AND is_published = true");
            e.Property(r => r.Tags)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null)!)
                .HasColumnType("jsonb");
            e.HasOne(r => r.Category).WithMany().HasForeignKey(r => r.CategoryId);
            e.HasMany(r => r.Versions).WithOne(v => v.Rule).HasForeignKey(v => v.RuleId);
            e.HasMany(r => r.Scopes).WithOne(s => s.Rule).HasForeignKey(s => s.RuleId);
            e.HasOne(r => r.CurrentVersion).WithMany()
                .HasForeignKey(r => r.CurrentVersionId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ---- RULE VERSION ----
        model.Entity<RuleVersion>(e =>
        {
            e.HasKey(v => v.Id);
            e.HasIndex(v => new { v.RuleId, v.VersionNumber }).IsUnique();
            e.Property(v => v.RuleDefinition)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<RuleDefinition>(v, (JsonSerializerOptions?)null)!)
                .HasColumnType("jsonb");
        });

        // ---- RULE SCOPE ----
        model.Entity<RuleScope>(e =>
        {
            e.HasKey(s => s.Id);
        });

        // ---- RULE SET ----
        model.Entity<RuleSetMember>(e =>
        {
            e.HasKey(m => new { m.SetId, m.RuleId });
            e.HasOne(m => m.RuleSet).WithMany(s => s.Members).HasForeignKey(m => m.SetId);
            e.HasOne(m => m.Rule).WithMany().HasForeignKey(m => m.RuleId);
        });

        // ---- EXECUTION REQUEST ----
        model.Entity<ExecutionRequest>(e =>
        {
            e.HasKey(r => r.Id);
            e.HasIndex(r => r.CorrelationId).IsUnique();
            e.HasIndex(r => new { r.TenantId, r.Status, r.CreatedAt });
            e.Property(r => r.InputPayload)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null)!)
                .HasColumnType("jsonb");
        });

        // ---- EXECUTION RESULT ----
        model.Entity<ExecutionResult>(e =>
        {
            e.HasKey(r => r.Id);
            e.HasIndex(r => r.RequestId);
            e.Property(r => r.RuleResults)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<RuleExecutionSummary>>(v, (JsonSerializerOptions?)null)!)
                .HasColumnType("jsonb");
            e.Property(r => r.FieldValues)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null)!)
                .HasColumnType("jsonb");
            e.Property(r => r.AiAnalysis)
                .HasConversion(
                    v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => v == null ? null : JsonSerializer.Deserialize<AiAnalysis>(v, (JsonSerializerOptions?)null))
                .HasColumnType("jsonb");
        });

        // ---- AUDIT LOG ----
        model.Entity<AuditLog>(e =>
        {
            e.HasKey(a => a.Id);
            e.HasIndex(a => new { a.TenantId, a.EntityType, a.EntityId, a.CreatedAt });
            e.Property(a => a.OldValues)
                .HasConversion(
                    v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => v == null ? null : JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null))
                .HasColumnType("jsonb");
            e.Property(a => a.NewValues)
                .HasConversion(
                    v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => v == null ? null : JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null))
                .HasColumnType("jsonb");
        });

        // ---- DEVIATION ----
        model.Entity<ExecutionDeviation>(e =>
        {
            e.HasKey(d => d.Id);
            e.HasIndex(d => d.ResultId);
        });

        // Global query filters for soft-delete / tenant isolation
        model.Entity<Rule>().HasQueryFilter(r => r.IsActive);
        model.Entity<User>().HasQueryFilter(u => u.IsActive);
    }

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        UpdateTimestamps();
        return base.SaveChangesAsync(ct);
    }

    private void UpdateTimestamps()
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Modified)
                entry.Entity.UpdatedAt = DateTime.UtcNow;
        }
    }
}

// DB entity aliases needed for EF mapping
public class TenantConfiguration : TenantEntity
{
    public string ConfigKey { get; set; } = default!;
    public string? ConfigValue { get; set; }
    public string ConfigType { get; set; } = "STRING";
    public string? Category { get; set; }
    public bool IsEncrypted { get; set; }
}

public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = default!;
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Product : TenantEntity
{
    public string ProductCode { get; set; } = default!;
    public string ProductName { get; set; } = default!;
    public string ProductType { get; set; } = default!;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public Dictionary<string, object> Config { get; set; } = new();
}

public class Branch : TenantEntity
{
    public string BranchCode { get; set; } = default!;
    public string BranchName { get; set; } = default!;
    public string? Region { get; set; }
    public string? State { get; set; }
    public string? City { get; set; }
    public string? Zone { get; set; }
    public bool IsActive { get; set; } = true;
}

public class LoanStage : TenantEntity
{
    public string StageCode { get; set; } = default!;
    public string StageName { get; set; } = default!;
    public int StageOrder { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

public class FieldCatalogEntry
{
    public Guid Id { get; set; }
    public Guid? TenantId { get; set; }
    public string FieldPath { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public string? Description { get; set; }
    public string DataType { get; set; } = default!;
    public string? Category { get; set; }
    public bool IsSystemField { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class RuleApproval
{
    public Guid Id { get; set; }
    public Guid RuleId { get; set; }
    public Guid VersionId { get; set; }
    public Guid TenantId { get; set; }
    public Guid RequestedBy { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string Status { get; set; } = "PENDING";
    public string? Comments { get; set; }
}

public class RuleExecutionDetail
{
    public Guid Id { get; set; }
    public Guid ResultId { get; set; }
    public Guid RuleId { get; set; }
    public string RuleCode { get; set; } = default!;
    public string RuleName { get; set; } = default!;
    public int VersionNumber { get; set; }
    public int ExecutionOrder { get; set; }
    public bool IsMatched { get; set; }
    public string ConditionsEvaluated { get; set; } = "[]";
    public string ActionsExecuted { get; set; } = "[]";
    public int? ExecutionMs { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class DecisionReportEntity
{
    public Guid Id { get; set; }
    public Guid RequestId { get; set; }
    public Guid TenantId { get; set; }
    public string? ReportNumber { get; set; }
    public string? ApplicationId { get; set; }
    public string? ProductCode { get; set; }
    public string FinalDecision { get; set; } = default!;
    public decimal? RiskScore { get; set; }
    public string? RiskCategory { get; set; }
    public string? TrafficLight { get; set; }
    public string? Summary { get; set; }
    public string? ReportJson { get; set; }
    public string? PdfStoragePath { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

public class AiGeneratedRuleEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string UserPrompt { get; set; } = default!;
    public string GeneratedRule { get; set; } = "{}";
    public Guid? RuleId { get; set; }
    public bool? IsAccepted { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
