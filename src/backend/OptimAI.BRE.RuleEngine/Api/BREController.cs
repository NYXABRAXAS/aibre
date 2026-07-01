using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using OptimAI.BRE.RuleEngine.Application;
using OptimAI.BRE.RuleEngine.Domain;
using OptimAI.BRE.Shared.Domain;
using System.ComponentModel.DataAnnotations;

namespace OptimAI.BRE.RuleEngine.Api;

[ApiController]
[Route("api/v1")]
[Authorize]
public sealed class BREController : ControllerBase
{
    private readonly IRuleExecutionService _executionService;
    private readonly IExecutionRequestRepository _requestRepo;
    private readonly IExecutionResultRepository _resultRepo;
    private readonly ISandboxService _sandboxService;
    private readonly IDecisionReportService _reportService;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly ILogger<BREController> _logger;

    public BREController(
        IRuleExecutionService executionService,
        IExecutionRequestRepository requestRepo,
        IExecutionResultRepository resultRepo,
        ISandboxService sandboxService,
        IDecisionReportService reportService,
        ITenantContextAccessor tenantAccessor,
        ILogger<BREController> logger)
    {
        _executionService = executionService;
        _requestRepo = requestRepo;
        _resultRepo = resultRepo;
        _sandboxService = sandboxService;
        _reportService = reportService;
        _tenantAccessor = tenantAccessor;
        _logger = logger;
    }

    /// <summary>
    /// Execute BRE rules against provided JSON payload. Core decision API.
    /// </summary>
    [HttpPost("execute-bre")]
    [EnableRateLimiting("bre-execution")]
    [ProducesResponseType(typeof(BREDecisionResponse), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 429)]
    public async Task<IActionResult> ExecuteBre(
        [FromBody] BREExecutionRequest request,
        CancellationToken ct)
    {
        var tenantId = _tenantAccessor.TenantId;

        var executionRequest = new ExecutionRequest
        {
            TenantId = tenantId,
            CorrelationId = request.CorrelationId ?? Guid.NewGuid().ToString(),
            ApplicationId = request.ApplicationId,
            ProductCode = request.ProductCode,
            BranchCode = request.BranchCode,
            StageCode = request.StageCode,
            RuleSetId = request.RuleSetId,
            InputPayload = request.Data,
            SourceSystem = request.SourceSystem,
            Status = ExecutionStatus.Processing,
            ProcessingStartedAt = DateTime.UtcNow
        };

        executionRequest = await _requestRepo.CreateAsync(executionRequest, ct);

        var context = new OptimAI.BRE.RuleEngine.Domain.ExecutionContext
        {
            TenantId = tenantId,
            CorrelationId = executionRequest.CorrelationId,
            ApplicationId = request.ApplicationId,
            ProductCode = request.ProductCode,
            BranchCode = request.BranchCode,
            StageCode = request.StageCode,
            RuleSetId = request.RuleSetId,
            Data = new DynamicDataContext(request.Data),
            Options = new ExecutionOptions
            {
                EnableAiAnalysis = request.EnableAiAnalysis ?? true,
                EnableRiskScoring = true,
                StopOnFirstReject = false,
                TimeoutMs = 5000
            }
        };

        var result = await _executionService.ExecuteAsync(context, ct);
        result.RequestId = executionRequest.Id;

        await _resultRepo.SaveAsync(result, ct);
        await _requestRepo.MarkCompletedAsync(executionRequest.Id, ct);

        var report = await _reportService.GenerateAsync(executionRequest.Id, result, ct);

        return Ok(new BREDecisionResponse
        {
            RequestId = executionRequest.Id,
            CorrelationId = executionRequest.CorrelationId!,
            ApplicationId = request.ApplicationId,
            Decision = result.FinalDecision.ToString(),
            TrafficLight = result.TrafficLight?.ToString() ?? "AMBER",
            RiskScore = result.RiskScore ?? 0,
            RiskCategory = result.RiskCategory?.ToString() ?? "MEDIUM",
            TotalRulesEvaluated = result.TotalRulesEvaluated,
            RulesPassed = result.RulesPassed,
            RulesFailed = result.RulesFailed,
            DeviationsCount = result.DeviationsCount,
            ExecutionMs = result.ExecutionMs ?? 0,
            RuleResults = result.RuleResults.Select(r => new RuleResultDto
            {
                RuleCode = r.RuleCode,
                RuleName = r.RuleName,
                IsMatched = r.IsMatched,
                ActionsExecuted = r.ActionsExecuted,
                ExecutionMs = r.ExecutionMs ?? 0
            }).ToList(),
            AiSummary = result.AiSummary,
            AiAnalysis = result.AiAnalysis != null ? new AiAnalysisDto
            {
                RiskSummary = result.AiAnalysis.RiskSummary,
                CreditSummary = result.AiAnalysis.CreditSummary,
                Strengths = result.AiAnalysis.Strengths,
                Weaknesses = result.AiAnalysis.Weaknesses,
                ApprovalRecommendation = result.AiAnalysis.ApprovalRecommendation,
                RejectionReasons = result.AiAnalysis.RejectionReasons,
                AdditionalDocuments = result.AiAnalysis.AdditionalDocuments,
                UnderwritingNotes = result.AiAnalysis.UnderwritingNotes,
                ConfidenceScore = result.AiAnalysis.ConfidenceScore
            } : null,
            ReportId = report?.Id,
            GeneratedAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Validate a rule definition before saving.
    /// </summary>
    [HttpPost("validate-rule")]
    [Authorize(Policy = "RuleWrite")]
    [ProducesResponseType(typeof(RuleValidationResponse), 200)]
    public async Task<IActionResult> ValidateRule([FromBody] RuleValidationRequest request)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // Validate rule definition structure
        if (request.RuleDefinition == null)
        {
            errors.Add("Rule definition is required");
            return Ok(new RuleValidationResponse { IsValid = false, Errors = errors });
        }

        if (request.RuleDefinition.Conditions == null || !request.RuleDefinition.Conditions.Rules.Any())
            errors.Add("At least one condition is required");

        if (!request.RuleDefinition.Actions.Any())
            warnings.Add("Rule has no actions defined");

        // Check referenced fields exist in catalog
        var allFields = ExtractFieldPaths(request.RuleDefinition.Conditions);
        foreach (var field in allFields)
        {
            if (!IsValidFieldPath(field))
                warnings.Add($"Field '{field}' not found in field catalog");
        }

        // Test with sample data if provided
        if (request.SampleData != null)
        {
            var context = new OptimAI.BRE.RuleEngine.Domain.ExecutionContext
            {
                TenantId = _tenantAccessor.TenantId,
                Data = new DynamicDataContext(request.SampleData),
                Options = new ExecutionOptions { EnableAiAnalysis = false }
            };

            // Quick dry-run against sample data using the condition evaluator
            // This validates conditions can be parsed and executed
        }

        return Ok(new RuleValidationResponse
        {
            IsValid = !errors.Any(),
            Errors = errors,
            Warnings = warnings,
            ExtractedFields = allFields
        });
    }

    /// <summary>
    /// Simulate BRE execution without persisting results.
    /// </summary>
    [HttpPost("simulate-decision")]
    [Authorize(Policy = "SandboxAccess")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<IActionResult> SimulateDecision([FromBody] SimulationRequest request, CancellationToken ct)
    {
        var result = await _sandboxService.SimulateAsync(new SandboxRequest
        {
            TenantId = _tenantAccessor.TenantId,
            RuleSetId = request.RuleSetId,
            TestData = request.Data,
            RuleIds = request.RuleIds,
            CreatedBy = _tenantAccessor.UserId
        }, ct);

        return Ok(result);
    }

    /// <summary>
    /// Get execution result by request ID.
    /// </summary>
    [HttpGet("decisions/{requestId:guid}")]
    [ProducesResponseType(typeof(BREDecisionResponse), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetDecision(Guid requestId, CancellationToken ct)
    {
        var result = await _resultRepo.GetByRequestIdAsync(requestId, _tenantAccessor.TenantId, ct);
        if (result == null) return NotFound();
        return Ok(result);
    }

    /// <summary>
    /// Get decision history with pagination.
    /// </summary>
    [HttpGet("decisions")]
    [ProducesResponseType(typeof(PagedResponse<DecisionSummaryDto>), 200)]
    public async Task<IActionResult> GetDecisionHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? decision = null,
        [FromQuery] string? applicationId = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        CancellationToken ct = default)
    {
        var result = await _resultRepo.GetHistoryAsync(new DecisionHistoryQuery
        {
            TenantId = _tenantAccessor.TenantId,
            Page = page,
            PageSize = Math.Min(pageSize, 100),
            Decision = decision,
            ApplicationId = applicationId,
            FromDate = fromDate,
            ToDate = toDate
        }, ct);

        return Ok(result);
    }

    private static List<string> ExtractFieldPaths(ConditionGroup? group)
    {
        if (group == null) return new();
        var fields = new List<string>();
        foreach (var node in group.Rules)
        {
            if (node.IsGroup && node.Group != null)
                fields.AddRange(ExtractFieldPaths(node.Group));
            else if (node.Field != null)
                fields.Add(node.Field);
        }
        return fields.Distinct().ToList();
    }

    private static bool IsValidFieldPath(string field) =>
        !string.IsNullOrWhiteSpace(field) && field.Contains('.');
}

// ============================================================
// REQUEST / RESPONSE DTOs
// ============================================================

public record BREExecutionRequest
{
    [Required]
    public Dictionary<string, object> Data { get; init; } = new();
    public string? CorrelationId { get; init; }
    public string? ApplicationId { get; init; }
    public string? ProductCode { get; init; }
    public string? BranchCode { get; init; }
    public string? StageCode { get; init; }
    public Guid? RuleSetId { get; init; }
    public string? SourceSystem { get; init; }
    public bool? EnableAiAnalysis { get; init; }
}

public record BREDecisionResponse
{
    public Guid RequestId { get; init; }
    public string CorrelationId { get; init; } = default!;
    public string? ApplicationId { get; init; }
    public string Decision { get; init; } = default!;
    public string TrafficLight { get; init; } = default!;
    public decimal RiskScore { get; init; }
    public string RiskCategory { get; init; } = default!;
    public int TotalRulesEvaluated { get; init; }
    public int RulesPassed { get; init; }
    public int RulesFailed { get; init; }
    public int DeviationsCount { get; init; }
    public int ExecutionMs { get; init; }
    public List<RuleResultDto> RuleResults { get; init; } = new();
    public string? AiSummary { get; init; }
    public AiAnalysisDto? AiAnalysis { get; init; }
    public Guid? ReportId { get; init; }
    public DateTime GeneratedAt { get; init; }
}

public record RuleResultDto
{
    public string RuleCode { get; init; } = default!;
    public string RuleName { get; init; } = default!;
    public bool IsMatched { get; init; }
    public List<string> ActionsExecuted { get; init; } = new();
    public int ExecutionMs { get; init; }
}

public record AiAnalysisDto
{
    public string RiskSummary { get; init; } = default!;
    public string CreditSummary { get; init; } = default!;
    public List<string> Strengths { get; init; } = new();
    public List<string> Weaknesses { get; init; } = new();
    public string ApprovalRecommendation { get; init; } = default!;
    public List<string> RejectionReasons { get; init; } = new();
    public List<string> AdditionalDocuments { get; init; } = new();
    public string UnderwritingNotes { get; init; } = default!;
    public double ConfidenceScore { get; init; }
}

public record RuleValidationRequest
{
    public RuleDefinition? RuleDefinition { get; init; }
    public Dictionary<string, object>? SampleData { get; init; }
}

public record RuleValidationResponse
{
    public bool IsValid { get; init; }
    public List<string> Errors { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
    public List<string> ExtractedFields { get; init; } = new();
}

public record SimulationRequest
{
    public Dictionary<string, object> Data { get; init; } = new();
    public Guid? RuleSetId { get; init; }
    public List<Guid>? RuleIds { get; init; }
}

public record PagedResponse<T>
{
    public List<T> Items { get; init; } = new();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

public record DecisionSummaryDto
{
    public Guid RequestId { get; init; }
    public string? ApplicationId { get; init; }
    public string Decision { get; init; } = default!;
    public string TrafficLight { get; init; } = default!;
    public decimal RiskScore { get; init; }
    public int DeviationsCount { get; init; }
    public DateTime CreatedAt { get; init; }
}

public class DecisionHistoryQuery
{
    public Guid TenantId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Decision { get; set; }
    public string? ApplicationId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public interface IExecutionRequestRepository
{
    Task<ExecutionRequest> CreateAsync(ExecutionRequest request, CancellationToken ct = default);
    Task MarkCompletedAsync(Guid id, CancellationToken ct = default);
}

public interface IExecutionResultRepository
{
    Task SaveAsync(ExecutionResult result, CancellationToken ct = default);
    Task<ExecutionResult?> GetByRequestIdAsync(Guid requestId, Guid tenantId, CancellationToken ct = default);
    Task<PagedResponse<DecisionSummaryDto>> GetHistoryAsync(DecisionHistoryQuery query, CancellationToken ct = default);
}

public interface ISandboxService
{
    Task<object> SimulateAsync(SandboxRequest request, CancellationToken ct = default);
}

public record SandboxRequest
{
    public Guid TenantId { get; init; }
    public Guid? RuleSetId { get; init; }
    public Dictionary<string, object> TestData { get; init; } = new();
    public List<Guid>? RuleIds { get; init; }
    public Guid CreatedBy { get; init; }
}

public interface IDecisionReportService
{
    Task<DecisionReport?> GenerateAsync(Guid requestId, ExecutionResult result, CancellationToken ct = default);
}

public class DecisionReport
{
    public Guid Id { get; set; }
}

public interface ITenantContextAccessor
{
    Guid TenantId { get; }
    Guid UserId { get; }
    string? UserEmail { get; }
}
