using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OptimAI.BRE.RuleEngine.Api;
using OptimAI.BRE.Shared.Domain;

namespace OptimAI.BRE.RuleDesigner.Api;

[ApiController]
[Route("api/v1/rules")]
[Authorize]
public sealed class RuleDesignerController : ControllerBase
{
    private readonly IRuleRepository _ruleRepo;
    private readonly IRuleVersionRepository _versionRepo;
    private readonly IRuleApprovalService _approvalService;
    private readonly IAuditService _auditService;
    private readonly ITenantContextAccessor _tenantAccessor;

    public RuleDesignerController(
        IRuleRepository ruleRepo,
        IRuleVersionRepository versionRepo,
        IRuleApprovalService approvalService,
        IAuditService auditService,
        ITenantContextAccessor tenantAccessor)
    {
        _ruleRepo = ruleRepo;
        _versionRepo = versionRepo;
        _approvalService = approvalService;
        _auditService = auditService;
        _tenantAccessor = tenantAccessor;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<RuleSummaryDto>), 200)]
    public async Task<IActionResult> GetRules(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? category = null,
        [FromQuery] string? ruleType = null,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var result = await _ruleRepo.GetPagedAsync(new RuleQuery
        {
            TenantId = _tenantAccessor.TenantId,
            Page = page,
            PageSize = Math.Min(pageSize, 100),
            Category = category,
            RuleType = ruleType,
            Status = status,
            Search = search
        }, ct);

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(RuleDetailDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetRule(Guid id, CancellationToken ct)
    {
        var rule = await _ruleRepo.GetByIdAsync(id, _tenantAccessor.TenantId, ct);
        if (rule == null) return NotFound();
        return Ok(MapToDetailDto(rule));
    }

    [HttpPost]
    [Authorize(Policy = "RuleWrite")]
    [ProducesResponseType(typeof(RuleDetailDto), 201)]
    public async Task<IActionResult> CreateRule([FromBody] CreateRuleRequest request, CancellationToken ct)
    {
        var rule = new Rule
        {
            TenantId = _tenantAccessor.TenantId,
            RuleCode = request.RuleCode ?? GenerateRuleCode(request.RuleName),
            RuleName = request.RuleName,
            Description = request.Description,
            CategoryId = request.CategoryId,
            RuleType = Enum.Parse<RuleType>(request.RuleType, true),
            Priority = request.Priority,
            Tags = request.Tags ?? new(),
            Status = RuleStatus.Draft,
            CreatedBy = _tenantAccessor.UserId
        };

        var version = new RuleVersion
        {
            TenantId = _tenantAccessor.TenantId,
            VersionNumber = 1,
            VersionLabel = "v1.0",
            RuleDefinition = request.RuleDefinition,
            Status = RuleStatus.Draft,
            IsCurrent = true,
            CreatedBy = _tenantAccessor.UserId
        };

        var created = await _ruleRepo.CreateWithVersionAsync(rule, version, ct);

        await _auditService.LogAsync(new AuditLog
        {
            TenantId = _tenantAccessor.TenantId,
            UserId = _tenantAccessor.UserId,
            Action = "RULE_CREATED",
            EntityType = "RULE",
            EntityId = created.Id.ToString(),
            NewValues = new Dictionary<string, object> { ["ruleCode"] = created.RuleCode, ["ruleName"] = created.RuleName }
        }, ct);

        return CreatedAtAction(nameof(GetRule), new { id = created.Id }, MapToDetailDto(created));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "RuleWrite")]
    [ProducesResponseType(typeof(RuleDetailDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateRule(Guid id, [FromBody] UpdateRuleRequest request, CancellationToken ct)
    {
        var rule = await _ruleRepo.GetByIdAsync(id, _tenantAccessor.TenantId, ct);
        if (rule == null) return NotFound();

        var oldValues = new Dictionary<string, object> { ["ruleName"] = rule.RuleName, ["status"] = rule.Status.ToString() };

        // Create new version
        var latestVersion = await _versionRepo.GetLatestAsync(id, ct);
        var newVersionNumber = (latestVersion?.VersionNumber ?? 0) + 1;

        var newVersion = new RuleVersion
        {
            TenantId = _tenantAccessor.TenantId,
            RuleId = id,
            VersionNumber = newVersionNumber,
            VersionLabel = $"v{newVersionNumber}.0",
            RuleDefinition = request.RuleDefinition,
            ChangeSummary = request.ChangeSummary,
            Status = RuleStatus.Draft,
            IsCurrent = false,
            CreatedBy = _tenantAccessor.UserId
        };

        rule.RuleName = request.RuleName ?? rule.RuleName;
        rule.Description = request.Description ?? rule.Description;
        rule.Priority = request.Priority ?? rule.Priority;
        rule.Tags = request.Tags ?? rule.Tags;
        rule.Status = RuleStatus.Draft;
        rule.IsPublished = false;

        await _ruleRepo.UpdateWithNewVersionAsync(rule, newVersion, ct);

        await _auditService.LogAsync(new AuditLog
        {
            TenantId = _tenantAccessor.TenantId,
            UserId = _tenantAccessor.UserId,
            Action = "RULE_UPDATED",
            EntityType = "RULE",
            EntityId = id.ToString(),
            OldValues = oldValues,
            NewValues = new Dictionary<string, object>
            {
                ["ruleName"] = rule.RuleName,
                ["versionNumber"] = newVersionNumber,
                ["changeSummary"] = request.ChangeSummary ?? ""
            }
        }, ct);

        var updated = await _ruleRepo.GetByIdAsync(id, _tenantAccessor.TenantId, ct);
        return Ok(MapToDetailDto(updated!));
    }

    [HttpPost("{id:guid}/submit-for-approval")]
    [Authorize(Policy = "RuleWrite")]
    public async Task<IActionResult> SubmitForApproval(Guid id, [FromBody] SubmitApprovalRequest request, CancellationToken ct)
    {
        var rule = await _ruleRepo.GetByIdAsync(id, _tenantAccessor.TenantId, ct);
        if (rule == null) return NotFound();

        await _approvalService.SubmitForApprovalAsync(id, rule.CurrentVersionId!.Value, _tenantAccessor.TenantId, _tenantAccessor.UserId, request.Comments, ct);

        return Ok(new { message = "Rule submitted for approval", status = "PENDING_APPROVAL" });
    }

    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = "RuleApprove")]
    public async Task<IActionResult> ApproveRule(Guid id, [FromBody] ApproveRuleRequest request, CancellationToken ct)
    {
        await _approvalService.ApproveAsync(id, _tenantAccessor.TenantId, _tenantAccessor.UserId, request.Comments, ct);

        await _auditService.LogAsync(new AuditLog
        {
            TenantId = _tenantAccessor.TenantId,
            UserId = _tenantAccessor.UserId,
            Action = "RULE_APPROVED",
            EntityType = "RULE",
            EntityId = id.ToString()
        }, ct);

        return Ok(new { message = "Rule approved successfully" });
    }

    [HttpPost("{id:guid}/reject")]
    [Authorize(Policy = "RuleApprove")]
    public async Task<IActionResult> RejectRule(Guid id, [FromBody] RejectRuleRequest request, CancellationToken ct)
    {
        await _approvalService.RejectAsync(id, _tenantAccessor.TenantId, _tenantAccessor.UserId, request.Comments, ct);
        return Ok(new { message = "Rule rejected" });
    }

    [HttpPost("{id:guid}/publish")]
    [Authorize(Policy = "RulePublish")]
    public async Task<IActionResult> PublishRule(Guid id, CancellationToken ct)
    {
        var rule = await _ruleRepo.GetByIdAsync(id, _tenantAccessor.TenantId, ct);
        if (rule == null) return NotFound();
        if (rule.Status != RuleStatus.Approved) return BadRequest(new { error = "Rule must be approved before publishing" });

        await _ruleRepo.PublishAsync(id, _tenantAccessor.TenantId, _tenantAccessor.UserId, ct);

        await _auditService.LogAsync(new AuditLog
        {
            TenantId = _tenantAccessor.TenantId,
            UserId = _tenantAccessor.UserId,
            Action = "RULE_PUBLISHED",
            EntityType = "RULE",
            EntityId = id.ToString()
        }, ct);

        return Ok(new { message = "Rule published to production" });
    }

    [HttpPost("{id:guid}/clone")]
    [Authorize(Policy = "RuleWrite")]
    public async Task<IActionResult> CloneRule(Guid id, [FromBody] CloneRuleRequest request, CancellationToken ct)
    {
        var original = await _ruleRepo.GetByIdAsync(id, _tenantAccessor.TenantId, ct);
        if (original == null) return NotFound();

        var cloned = await _ruleRepo.CloneAsync(id, _tenantAccessor.TenantId, _tenantAccessor.UserId, request.NewRuleCode, request.NewRuleName, ct);

        return CreatedAtAction(nameof(GetRule), new { id = cloned.Id }, MapToDetailDto(cloned));
    }

    [HttpPatch("{id:guid}/toggle")]
    [Authorize(Policy = "RuleWrite")]
    public async Task<IActionResult> ToggleRule(Guid id, CancellationToken ct)
    {
        var rule = await _ruleRepo.GetByIdAsync(id, _tenantAccessor.TenantId, ct);
        if (rule == null) return NotFound();

        await _ruleRepo.ToggleActiveAsync(id, _tenantAccessor.TenantId, ct);
        return Ok(new { message = $"Rule {(rule.IsActive ? "disabled" : "enabled")}", isActive = !rule.IsActive });
    }

    [HttpGet("{id:guid}/versions")]
    [ProducesResponseType(typeof(List<RuleVersionSummaryDto>), 200)]
    public async Task<IActionResult> GetVersions(Guid id, CancellationToken ct)
    {
        var versions = await _versionRepo.GetAllAsync(id, _tenantAccessor.TenantId, ct);
        return Ok(versions.Select(v => new RuleVersionSummaryDto
        {
            Id = v.Id,
            VersionNumber = v.VersionNumber,
            VersionLabel = v.VersionLabel ?? $"v{v.VersionNumber}.0",
            Status = v.Status.ToString(),
            ChangeSummary = v.ChangeSummary,
            IsCurrent = v.IsCurrent,
            CreatedAt = v.CreatedAt,
            ApprovedAt = v.ApprovedAt,
            PublishedAt = v.PublishedAt
        }).ToList());
    }

    [HttpPut("{id:guid}/scopes")]
    [Authorize(Policy = "RuleWrite")]
    public async Task<IActionResult> UpdateScopes(Guid id, [FromBody] UpdateScopesRequest request, CancellationToken ct)
    {
        await _ruleRepo.UpdateScopesAsync(id, _tenantAccessor.TenantId, request.Scopes, ct);
        return Ok(new { message = "Rule scopes updated" });
    }

    private static string GenerateRuleCode(string ruleName)
    {
        return ruleName.ToLower()
            .Replace(" ", "_")
            .Replace("-", "_")
            .Replace("/", "_")
            [..Math.Min(ruleName.Length, 80)];
    }

    private static RuleDetailDto MapToDetailDto(Rule rule) => new()
    {
        Id = rule.Id,
        RuleCode = rule.RuleCode,
        RuleName = rule.RuleName,
        Description = rule.Description,
        CategoryId = rule.CategoryId,
        RuleType = rule.RuleType.ToString(),
        Priority = rule.Priority,
        IsActive = rule.IsActive,
        IsPublished = rule.IsPublished,
        Status = rule.Status.ToString(),
        Tags = rule.Tags,
        CurrentVersion = rule.CurrentVersion != null ? new RuleVersionDto
        {
            Id = rule.CurrentVersion.Id,
            VersionNumber = rule.CurrentVersion.VersionNumber,
            VersionLabel = rule.CurrentVersion.VersionLabel ?? $"v{rule.CurrentVersion.VersionNumber}.0",
            RuleDefinition = rule.CurrentVersion.RuleDefinition,
            Status = rule.CurrentVersion.Status.ToString(),
            CreatedAt = rule.CurrentVersion.CreatedAt
        } : null,
        Scopes = rule.Scopes.Select(s => new RuleScopeDto
        {
            ScopeType = s.ScopeType.ToString(),
            ScopeValue = s.ScopeValue,
            IsExcluded = s.IsExcluded
        }).ToList(),
        CreatedAt = rule.CreatedAt,
        UpdatedAt = rule.UpdatedAt
    };
}

// ============================================================
// DTOs
// ============================================================

public record RuleSummaryDto
{
    public Guid Id { get; init; }
    public string RuleCode { get; init; } = default!;
    public string RuleName { get; init; } = default!;
    public string RuleType { get; init; } = default!;
    public string Status { get; init; } = default!;
    public bool IsActive { get; init; }
    public bool IsPublished { get; init; }
    public int Priority { get; init; }
    public List<string> Tags { get; init; } = new();
    public int VersionCount { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public record RuleDetailDto : RuleSummaryDto
{
    public string? Description { get; init; }
    public Guid? CategoryId { get; init; }
    public RuleVersionDto? CurrentVersion { get; init; }
    public List<RuleScopeDto> Scopes { get; init; } = new();
}

public record RuleVersionDto
{
    public Guid Id { get; init; }
    public int VersionNumber { get; init; }
    public string VersionLabel { get; init; } = default!;
    public RuleDefinition RuleDefinition { get; init; } = default!;
    public string Status { get; init; } = default!;
    public DateTime CreatedAt { get; init; }
}

public record RuleVersionSummaryDto
{
    public Guid Id { get; init; }
    public int VersionNumber { get; init; }
    public string VersionLabel { get; init; } = default!;
    public string Status { get; init; } = default!;
    public string? ChangeSummary { get; init; }
    public bool IsCurrent { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ApprovedAt { get; init; }
    public DateTime? PublishedAt { get; init; }
}

public record RuleScopeDto
{
    public string ScopeType { get; init; } = default!;
    public string ScopeValue { get; init; } = default!;
    public bool IsExcluded { get; init; }
}

public record CreateRuleRequest
{
    public string RuleName { get; init; } = default!;
    public string? RuleCode { get; init; }
    public string? Description { get; init; }
    public Guid? CategoryId { get; init; }
    public string RuleType { get; init; } = default!;
    public int Priority { get; init; } = 100;
    public List<string>? Tags { get; init; }
    public RuleDefinition RuleDefinition { get; init; } = default!;
}

public record UpdateRuleRequest
{
    public string? RuleName { get; init; }
    public string? Description { get; init; }
    public int? Priority { get; init; }
    public List<string>? Tags { get; init; }
    public string? ChangeSummary { get; init; }
    public RuleDefinition RuleDefinition { get; init; } = default!;
}

public record SubmitApprovalRequest { public string? Comments { get; init; } }
public record ApproveRuleRequest { public string? Comments { get; init; } }
public record RejectRuleRequest { public string Comments { get; init; } = default!; }
public record CloneRuleRequest { public string? NewRuleCode { get; init; } public string? NewRuleName { get; init; } }

public record UpdateScopesRequest
{
    public List<RuleScopeRequest> Scopes { get; init; } = new();
}

public record RuleScopeRequest
{
    public string ScopeType { get; init; } = default!;
    public string ScopeValue { get; init; } = default!;
    public bool IsExcluded { get; init; }
}

public record RuleQuery
{
    public Guid TenantId { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Category { get; init; }
    public string? RuleType { get; init; }
    public string? Status { get; init; }
    public string? Search { get; init; }
}

public interface IRuleRepository
{
    Task<PagedResponse<RuleSummaryDto>> GetPagedAsync(RuleQuery query, CancellationToken ct = default);
    Task<Rule?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken ct = default);
    Task<Rule> CreateWithVersionAsync(Rule rule, RuleVersion version, CancellationToken ct = default);
    Task UpdateWithNewVersionAsync(Rule rule, RuleVersion newVersion, CancellationToken ct = default);
    Task PublishAsync(Guid id, Guid tenantId, Guid publishedBy, CancellationToken ct = default);
    Task<Rule> CloneAsync(Guid id, Guid tenantId, Guid clonedBy, string? newCode, string? newName, CancellationToken ct = default);
    Task ToggleActiveAsync(Guid id, Guid tenantId, CancellationToken ct = default);
    Task UpdateScopesAsync(Guid id, Guid tenantId, List<RuleScopeRequest> scopes, CancellationToken ct = default);
}

public interface IRuleVersionRepository
{
    Task<RuleVersion?> GetLatestAsync(Guid ruleId, CancellationToken ct = default);
    Task<List<RuleVersion>> GetAllAsync(Guid ruleId, Guid tenantId, CancellationToken ct = default);
}

public interface IRuleApprovalService
{
    Task SubmitForApprovalAsync(Guid ruleId, Guid versionId, Guid tenantId, Guid requestedBy, string? comments, CancellationToken ct = default);
    Task ApproveAsync(Guid ruleId, Guid tenantId, Guid approvedBy, string? comments, CancellationToken ct = default);
    Task RejectAsync(Guid ruleId, Guid tenantId, Guid rejectedBy, string comments, CancellationToken ct = default);
}

public interface IAuditService
{
    Task LogAsync(AuditLog log, CancellationToken ct = default);
}
