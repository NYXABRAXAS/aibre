// ============================================================
// REPOSITORY STUB IMPLEMENTATIONS
// These are functional local-dev implementations.
// Replace with full implementations per module.
// ============================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using OptimAI.BRE.AIEngine.Api;
using OptimAI.BRE.RuleDesigner.Api;
using OptimAI.BRE.RuleEngine.Api;
using OptimAI.BRE.RuleEngine.Application;
using OptimAI.BRE.RuleEngine.Domain;
using OptimAI.BRE.Shared.Domain;
using System.Text.Json;

namespace OptimAI.BRE.RuleEngine.Infrastructure;

// ---- DynamicFieldResolver ----
public class DynamicFieldResolver : IDynamicFieldResolver
{
    public object? Resolve(string fieldPath, DynamicDataContext context) => context.GetValue(fieldPath);

    public bool TryResolve(string fieldPath, DynamicDataContext context, out object? value)
    {
        value = context.GetValue(fieldPath);
        return true;
    }
}

// ---- DeviationTypeRepository ----
public class DeviationTypeRepository : IDeviationTypeRepository
{
    private readonly BREDbContext _db;
    public DeviationTypeRepository(BREDbContext db) => _db = db;

    public async Task<DeviationType?> FindByCodeAsync(Guid tenantId, string code, CancellationToken ct = default)
        => await _db.DeviationTypes.FirstOrDefaultAsync(d =>
            (d.TenantId == tenantId || d.TenantId == null) && d.DeviationCode == code, ct);

    public async Task<IReadOnlyList<DeviationType>> GetAllActiveAsync(Guid tenantId, CancellationToken ct = default)
        => await _db.DeviationTypes
            .Where(d => (d.TenantId == tenantId || d.TenantId == null) && d.IsActive)
            .ToListAsync(ct);
}

// ---- RiskWeightRepository ----
public class RiskWeightRepository : IRiskWeightRepository
{
    public Task<Dictionary<string, decimal>> GetWeightsAsync(Guid tenantId, string? productCode = null)
        => Task.FromResult(new Dictionary<string, decimal>
        {
            ["bureau"] = 30, ["income"] = 25, ["fi"] = 15,
            ["fraud"] = 15, ["vehicle"] = 10, ["employment"] = 10, ["rule_penalty"] = 5
        });
}

// ---- ExecutionRequestRepository ----
public class ExecutionRequestRepository : IExecutionRequestRepository
{
    private readonly BREDbContext _db;
    public ExecutionRequestRepository(BREDbContext db) => _db = db;

    public async Task<ExecutionRequest> CreateAsync(ExecutionRequest request, CancellationToken ct = default)
    {
        _db.ExecutionRequests.Add(request);
        await _db.SaveChangesAsync(ct);
        return request;
    }

    public async Task MarkCompletedAsync(Guid id, CancellationToken ct = default)
    {
        await _db.ExecutionRequests.Where(r => r.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, ExecutionStatus.Completed)
                .SetProperty(r => r.CompletedAt, DateTime.UtcNow), ct);
    }
}

// ---- ExecutionResultRepository ----
public class ExecutionResultRepository : IExecutionResultRepository
{
    private readonly BREDbContext _db;
    public ExecutionResultRepository(BREDbContext db) => _db = db;

    public async Task SaveAsync(ExecutionResult result, CancellationToken ct = default)
    {
        _db.ExecutionResults.Add(result);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<ExecutionResult?> GetByRequestIdAsync(Guid requestId, Guid tenantId, CancellationToken ct = default)
        => await _db.ExecutionResults.FirstOrDefaultAsync(r => r.RequestId == requestId && r.TenantId == tenantId, ct);

    public async Task<PagedResponse<DecisionSummaryDto>> GetHistoryAsync(DecisionHistoryQuery query, CancellationToken ct = default)
    {
        var q = _db.ExecutionResults
            .Join(_db.ExecutionRequests, r => r.RequestId, req => req.Id, (r, req) => new { r, req })
            .Where(x => x.r.TenantId == query.TenantId);

        if (query.Decision != null) q = q.Where(x => x.r.FinalDecision.ToString() == query.Decision);
        if (query.FromDate.HasValue) q = q.Where(x => x.req.CreatedAt >= query.FromDate);
        if (query.ToDate.HasValue) q = q.Where(x => x.req.CreatedAt <= query.ToDate);

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(x => x.req.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(x => new DecisionSummaryDto
            {
                RequestId = x.r.RequestId,
                ApplicationId = x.req.ApplicationId,
                Decision = x.r.FinalDecision.ToString(),
                TrafficLight = x.r.TrafficLight.ToString() ?? "AMBER",
                RiskScore = x.r.RiskScore ?? 0,
                DeviationsCount = x.r.DeviationsCount,
                CreatedAt = x.req.CreatedAt
            })
            .ToListAsync(ct);

        return new PagedResponse<DecisionSummaryDto>
        {
            Items = items, TotalCount = total,
            Page = query.Page, PageSize = query.PageSize
        };
    }
}

// ---- DecisionReportService ----
public class DecisionReportService : IDecisionReportService
{
    private readonly BREDbContext _db;
    public DecisionReportService(BREDbContext db) => _db = db;

    public async Task<DecisionReport?> GenerateAsync(Guid requestId, ExecutionResult result, CancellationToken ct = default)
    {
        var report = new DecisionReportEntity
        {
            RequestId = requestId,
            TenantId = result.TenantId,
            FinalDecision = result.FinalDecision.ToString(),
            RiskScore = result.RiskScore,
            RiskCategory = result.RiskCategory?.ToString(),
            TrafficLight = result.TrafficLight?.ToString(),
            ReportJson = JsonSerializer.Serialize(result),
            GeneratedAt = DateTime.UtcNow
        };
        _db.DecisionReports.Add(report);
        await _db.SaveChangesAsync(ct);
        return new DecisionReport { Id = report.Id };
    }
}

// ---- SandboxService ----
public class SandboxService : ISandboxService
{
    private readonly IRuleExecutionService _exec;
    private readonly IRuleLoader _loader;
    public SandboxService(IRuleExecutionService exec, IRuleLoader loader)
    {
        _exec = exec;
        _loader = loader;
    }

    public async Task<object> SimulateAsync(SandboxRequest request, CancellationToken ct = default)
    {
        var context = new ExecutionContext
        {
            TenantId = request.TenantId,
            CorrelationId = $"sandbox-{Guid.NewGuid()}",
            RuleSetId = request.RuleSetId,
            Data = new DynamicDataContext(request.TestData),
            Options = new ExecutionOptions { EnableAiAnalysis = false }
        };
        return await _exec.ExecuteAsync(context, ct);
    }
}

// ---- AiPromptRepository ----
public class AiPromptRepository : IAiPromptRepository
{
    private readonly BREDbContext _db;
    public AiPromptRepository(BREDbContext db) => _db = db;

    public async Task<string?> GetPromptAsync(string promptCode, CancellationToken ct = default)
    {
        var p = await _db.Set<AiPromptEntity>()
            .FirstOrDefaultAsync(x => x.PromptCode == promptCode && x.IsActive, ct);
        return p?.PromptTemplate;
    }
}

// ---- AiRuleGeneratorRepository ----
public class AiRuleGeneratorRepository : IAiRuleGeneratorRepository
{
    private readonly BREDbContext _db;
    public AiRuleGeneratorRepository(BREDbContext db) => _db = db;

    public async Task<Guid> SaveGeneratedRuleAsync(AiGeneratedRuleRecord record, CancellationToken ct = default)
    {
        var entity = new AiGeneratedRuleEntity
        {
            TenantId = record.TenantId,
            UserPrompt = record.UserPrompt,
            GeneratedRule = JsonSerializer.Serialize(record.GeneratedRule),
            CreatedBy = record.CreatedBy
        };
        _db.AiGeneratedRules.Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity.Id;
    }

    public async Task<bool> AcceptRuleAsync(Guid generationId, Guid tenantId, Guid? categoryId, Guid userId, CancellationToken ct = default)
    {
        var entity = await _db.AiGeneratedRules
            .FirstOrDefaultAsync(e => e.Id == generationId && e.TenantId == tenantId, ct);
        if (entity == null) return false;
        entity.IsAccepted = true;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<List<AiGeneratedRuleRecord>> GetHistoryAsync(Guid tenantId, int page, int pageSize, CancellationToken ct = default)
    {
        return await _db.AiGeneratedRules
            .Where(e => e.TenantId == tenantId)
            .OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(e => new AiGeneratedRuleRecord
            {
                Id = e.Id, TenantId = e.TenantId, UserPrompt = e.UserPrompt,
                IsAccepted = e.IsAccepted, CreatedBy = e.CreatedBy, CreatedAt = e.CreatedAt
            })
            .ToListAsync(ct);
    }
}

// ---- FieldCatalogRepository ----
public class FieldCatalogRepository : IFieldCatalogRepository
{
    private readonly BREDbContext _db;
    public FieldCatalogRepository(BREDbContext db) => _db = db;

    public async Task<List<string>> GetFieldPathsAsync(Guid tenantId, CancellationToken ct = default)
        => await _db.FieldCatalog
            .Where(f => f.IsActive && (f.TenantId == null || f.TenantId == tenantId))
            .Select(f => f.FieldPath).ToListAsync(ct);

    public async Task<List<FieldCatalogDto>> GetCatalogAsync(Guid tenantId, string? category = null, CancellationToken ct = default)
    {
        var q = _db.FieldCatalog.Where(f => f.IsActive && (f.TenantId == null || f.TenantId == tenantId));
        if (category != null) q = q.Where(f => f.Category == category);
        return await q.Select(f => new FieldCatalogDto
        {
            FieldPath = f.FieldPath, DisplayName = f.DisplayName,
            DataType = f.DataType, Category = f.Category, Description = f.Description
        }).ToListAsync(ct);
    }
}

// ---- RuleRepository ----
public class RuleRepository : IRuleRepository
{
    private readonly BREDbContext _db;
    public RuleRepository(BREDbContext db) => _db = db;

    public async Task<PagedResponse<RuleSummaryDto>> GetPagedAsync(RuleQuery query, CancellationToken ct = default)
    {
        var q = _db.Rules.Where(r => r.TenantId == query.TenantId).IgnoreQueryFilters();
        if (query.Status != null && Enum.TryParse<RuleStatus>(query.Status, true, out var s))
            q = q.Where(r => r.Status == s);
        if (query.RuleType != null && Enum.TryParse<RuleType>(query.RuleType, true, out var rt))
            q = q.Where(r => r.RuleType == rt);
        if (!string.IsNullOrWhiteSpace(query.Search))
            q = q.Where(r => r.RuleName.Contains(query.Search) || r.RuleCode.Contains(query.Search));

        var total = await q.CountAsync(ct);
        var items = await q.OrderBy(r => r.Priority).ThenBy(r => r.RuleName)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(r => new RuleSummaryDto
            {
                Id = r.Id, RuleCode = r.RuleCode, RuleName = r.RuleName,
                RuleType = r.RuleType.ToString(), Status = r.Status.ToString(),
                IsActive = r.IsActive, IsPublished = r.IsPublished,
                Priority = r.Priority, Tags = r.Tags,
                CreatedAt = r.CreatedAt, UpdatedAt = r.UpdatedAt
            }).ToListAsync(ct);

        return new PagedResponse<RuleSummaryDto>
        { Items = items, TotalCount = total, Page = query.Page, PageSize = query.PageSize };
    }

    public async Task<Rule?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken ct = default)
        => await _db.Rules.IgnoreQueryFilters()
            .Include(r => r.CurrentVersion)
            .Include(r => r.Scopes)
            .Include(r => r.Category)
            .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId, ct);

    public async Task<Rule> CreateWithVersionAsync(Rule rule, RuleVersion version, CancellationToken ct = default)
    {
        _db.Rules.Add(rule);
        await _db.SaveChangesAsync(ct);
        version.RuleId = rule.Id;
        version.TenantId = rule.TenantId;
        _db.RuleVersions.Add(version);
        await _db.SaveChangesAsync(ct);
        rule.CurrentVersionId = version.Id;
        await _db.SaveChangesAsync(ct);
        return rule;
    }

    public async Task UpdateWithNewVersionAsync(Rule rule, RuleVersion newVersion, CancellationToken ct = default)
    {
        _db.Rules.Update(rule);
        _db.RuleVersions.Add(newVersion);
        await _db.SaveChangesAsync(ct);
    }

    public async Task PublishAsync(Guid id, Guid tenantId, Guid publishedBy, CancellationToken ct = default)
    {
        var rule = await _db.Rules.IgnoreQueryFilters().Include(r => r.CurrentVersion)
            .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId, ct);
        if (rule == null) return;
        rule.IsPublished = true;
        rule.IsDraft = false;
        rule.Status = RuleStatus.Published;
        if (rule.CurrentVersion != null)
        {
            rule.CurrentVersion.Status = RuleStatus.Published;
            rule.CurrentVersion.PublishedBy = publishedBy;
            rule.CurrentVersion.PublishedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task<Rule> CloneAsync(Guid id, Guid tenantId, Guid clonedBy, string? newCode, string? newName, CancellationToken ct = default)
    {
        var original = await GetByIdAsync(id, tenantId, ct);
        var clone = new Rule
        {
            TenantId = tenantId,
            RuleCode = newCode ?? $"{original!.RuleCode}_copy_{DateTime.UtcNow.Ticks}",
            RuleName = newName ?? $"{original.RuleName} (Copy)",
            Description = original.Description,
            CategoryId = original.CategoryId,
            RuleType = original.RuleType,
            Priority = original.Priority,
            Tags = new List<string>(original.Tags),
            Status = RuleStatus.Draft,
            CreatedBy = clonedBy
        };
        var version = new RuleVersion
        {
            TenantId = tenantId,
            VersionNumber = 1,
            VersionLabel = "v1.0",
            RuleDefinition = original!.CurrentVersion!.RuleDefinition,
            ChangeSummary = $"Cloned from {original.RuleCode}",
            IsCurrent = true,
            Status = RuleStatus.Draft,
            CreatedBy = clonedBy
        };
        return await CreateWithVersionAsync(clone, version, ct);
    }

    public async Task ToggleActiveAsync(Guid id, Guid tenantId, CancellationToken ct = default)
    {
        var rule = await _db.Rules.IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId, ct);
        if (rule == null) return;
        rule.IsActive = !rule.IsActive;
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateScopesAsync(Guid id, Guid tenantId, List<RuleScopeRequest> scopes, CancellationToken ct = default)
    {
        var existing = _db.RuleScopes.Where(s => s.RuleId == id);
        _db.RuleScopes.RemoveRange(existing);
        foreach (var s in scopes)
        {
            _db.RuleScopes.Add(new RuleScope
            {
                RuleId = id, TenantId = tenantId,
                ScopeType = Enum.Parse<ScopeType>(s.ScopeType, true),
                ScopeValue = s.ScopeValue, IsExcluded = s.IsExcluded
            });
        }
        await _db.SaveChangesAsync(ct);
    }
}

// ---- RuleVersionRepository ----
public class RuleVersionRepository : IRuleVersionRepository
{
    private readonly BREDbContext _db;
    public RuleVersionRepository(BREDbContext db) => _db = db;

    public async Task<RuleVersion?> GetLatestAsync(Guid ruleId, CancellationToken ct = default)
        => await _db.RuleVersions.Where(v => v.RuleId == ruleId)
            .OrderByDescending(v => v.VersionNumber).FirstOrDefaultAsync(ct);

    public async Task<List<RuleVersion>> GetAllAsync(Guid ruleId, Guid tenantId, CancellationToken ct = default)
        => await _db.RuleVersions
            .Where(v => v.RuleId == ruleId && v.TenantId == tenantId)
            .OrderByDescending(v => v.VersionNumber).ToListAsync(ct);
}

// ---- RuleApprovalService ----
public class RuleApprovalService : IRuleApprovalService
{
    private readonly BREDbContext _db;
    public RuleApprovalService(BREDbContext db) => _db = db;

    public async Task SubmitForApprovalAsync(Guid ruleId, Guid versionId, Guid tenantId, Guid requestedBy, string? comments, CancellationToken ct = default)
    {
        await _db.Rules.IgnoreQueryFilters().Where(r => r.Id == ruleId)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, RuleStatus.PendingApproval), ct);
        _db.RuleApprovals.Add(new RuleApproval
        {
            RuleId = ruleId, VersionId = versionId, TenantId = tenantId,
            RequestedBy = requestedBy, Comments = comments, Status = "PENDING"
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task ApproveAsync(Guid ruleId, Guid tenantId, Guid approvedBy, string? comments, CancellationToken ct = default)
    {
        await _db.Rules.IgnoreQueryFilters().Where(r => r.Id == ruleId)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, RuleStatus.Approved), ct);
        var approval = await _db.RuleApprovals.OrderByDescending(a => a.RequestedAt)
            .FirstOrDefaultAsync(a => a.RuleId == ruleId && a.Status == "PENDING", ct);
        if (approval != null)
        {
            approval.Status = "APPROVED";
            approval.ReviewedBy = approvedBy;
            approval.ReviewedAt = DateTime.UtcNow;
            approval.Comments = comments;
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task RejectAsync(Guid ruleId, Guid tenantId, Guid rejectedBy, string comments, CancellationToken ct = default)
    {
        await _db.Rules.IgnoreQueryFilters().Where(r => r.Id == ruleId)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, RuleStatus.Draft), ct);
        var approval = await _db.RuleApprovals.OrderByDescending(a => a.RequestedAt)
            .FirstOrDefaultAsync(a => a.RuleId == ruleId && a.Status == "PENDING", ct);
        if (approval != null)
        {
            approval.Status = "REJECTED";
            approval.ReviewedBy = rejectedBy;
            approval.ReviewedAt = DateTime.UtcNow;
            approval.Comments = comments;
            await _db.SaveChangesAsync(ct);
        }
    }
}

// ---- AuditService ----
public class AuditService : IAuditService
{
    private readonly BREDbContext _db;
    public AuditService(BREDbContext db) => _db = db;

    public async Task LogAsync(AuditLog log, CancellationToken ct = default)
    {
        _db.AuditLogs.Add(log);
        await _db.SaveChangesAsync(ct);
    }
}

// Placeholder entity for AI prompts (extend BREDbContext if needed)
public class AiPromptEntity
{
    public Guid Id { get; set; }
    public string PromptCode { get; set; } = default!;
    public string PromptTemplate { get; set; } = default!;
    public bool IsActive { get; set; } = true;
}
