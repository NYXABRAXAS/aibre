using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OptimAI.BRE.AIEngine.Application;
using OptimAI.BRE.RuleEngine.Api;
using OptimAI.BRE.Shared.Domain;

namespace OptimAI.BRE.AIEngine.Api;

[ApiController]
[Route("api/v1/ai")]
[Authorize]
public sealed class AiController : ControllerBase
{
    private readonly IAiCreditAnalystService _aiService;
    private readonly IAiRuleGeneratorRepository _generatedRuleRepo;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly IFieldCatalogRepository _fieldCatalogRepo;
    private readonly ILogger<AiController> _logger;

    public AiController(
        IAiCreditAnalystService aiService,
        IAiRuleGeneratorRepository generatedRuleRepo,
        ITenantContextAccessor tenantAccessor,
        IFieldCatalogRepository fieldCatalogRepo,
        ILogger<AiController> logger)
    {
        _aiService = aiService;
        _generatedRuleRepo = generatedRuleRepo;
        _tenantAccessor = tenantAccessor;
        _fieldCatalogRepo = fieldCatalogRepo;
        _logger = logger;
    }

    /// <summary>
    /// Generate BRE rule from natural language description using AI.
    /// </summary>
    [HttpPost("generate-rule")]
    [Authorize(Policy = "AiGenerate")]
    [ProducesResponseType(typeof(GeneratedRuleResponse), 200)]
    public async Task<IActionResult> GenerateRule([FromBody] GenerateRuleRequest request, CancellationToken ct)
    {
        var availableFields = await _fieldCatalogRepo.GetFieldPathsAsync(_tenantAccessor.TenantId, ct);

        var ruleDefinition = await _aiService.GenerateRuleFromPromptAsync(new RuleGenerationRequest
        {
            UserPrompt = request.Prompt,
            ProductType = request.ProductType,
            AvailableFields = availableFields,
            TenantId = _tenantAccessor.TenantId,
            CreatedBy = _tenantAccessor.UserId
        }, ct);

        if (ruleDefinition == null)
            return BadRequest(new { error = "AI could not generate a rule from the provided description. Please be more specific." });

        var savedId = await _generatedRuleRepo.SaveGeneratedRuleAsync(new AiGeneratedRuleRecord
        {
            TenantId = _tenantAccessor.TenantId,
            UserPrompt = request.Prompt,
            GeneratedRule = ruleDefinition,
            CreatedBy = _tenantAccessor.UserId
        }, ct);

        return Ok(new GeneratedRuleResponse
        {
            GenerationId = savedId,
            UserPrompt = request.Prompt,
            RuleDefinition = ruleDefinition,
            CanEdit = true,
            Message = "Rule generated successfully. Review and save to rule engine."
        });
    }

    /// <summary>
    /// Run AI credit analysis on execution result.
    /// </summary>
    [HttpPost("analyze-credit")]
    [Authorize(Policy = "AiAnalysis")]
    [ProducesResponseType(typeof(AiAnalysis), 200)]
    public async Task<IActionResult> AnalyzeCredit([FromBody] CreditAnalysisApiRequest request, CancellationToken ct)
    {
        var analysis = await _aiService.AnalyzeCreditAsync(new CreditAnalysisRequest
        {
            Data = request.ApplicationData,
            Decision = request.Decision,
            RiskScore = request.RiskScore,
            RiskCategory = request.RiskCategory,
            ProductCode = request.ProductCode,
            Deviations = request.Deviations.Select(d => new ExecutionDeviation
            {
                DeviationCode = d.Code,
                DeviationName = d.Name,
                Severity = Enum.Parse<Severity>(d.Severity, true),
                Reason = d.Reason
            }).ToList()
        }, ct);

        return Ok(analysis);
    }

    /// <summary>
    /// Analyze deviations and provide mitigation recommendations.
    /// </summary>
    [HttpPost("analyze-deviations")]
    [Authorize(Policy = "AiAnalysis")]
    [ProducesResponseType(typeof(DeviationAnalysis), 200)]
    public async Task<IActionResult> AnalyzeDeviations([FromBody] DeviationAnalysisApiRequest request, CancellationToken ct)
    {
        var analysis = await _aiService.AnalyzeDeviationsAsync(new DeviationAnalysisRequest
        {
            ApplicationData = request.ApplicationData,
            Deviations = request.Deviations.Select(d => new ExecutionDeviation
            {
                DeviationCode = d.Code,
                DeviationName = d.Name,
                Severity = Enum.Parse<Severity>(d.Severity, true),
                Reason = d.Reason,
                ActualValue = d.ActualValue,
                ExpectedValue = d.ExpectedValue,
                TenantId = _tenantAccessor.TenantId
            }).ToList()
        }, ct);

        return Ok(analysis);
    }

    /// <summary>
    /// Accept a generated rule and save to rule engine.
    /// </summary>
    [HttpPost("generated-rules/{generationId:guid}/accept")]
    [Authorize(Policy = "RuleWrite")]
    public async Task<IActionResult> AcceptGeneratedRule(Guid generationId, [FromBody] AcceptRuleRequest request, CancellationToken ct)
    {
        var saved = await _generatedRuleRepo.AcceptRuleAsync(generationId, _tenantAccessor.TenantId, request.CategoryId, _tenantAccessor.UserId, ct);
        if (!saved) return NotFound();

        return Ok(new { message = "Rule saved to rule engine successfully", ruleId = saved });
    }

    /// <summary>
    /// Get AI generation history.
    /// </summary>
    [HttpGet("generated-rules")]
    [ProducesResponseType(typeof(List<AiGeneratedRuleRecord>), 200)]
    public async Task<IActionResult> GetGenerationHistory([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var history = await _generatedRuleRepo.GetHistoryAsync(_tenantAccessor.TenantId, page, pageSize, ct);
        return Ok(history);
    }
}

// DTOs
public record GenerateRuleRequest
{
    public string Prompt { get; init; } = default!;
    public string? ProductType { get; init; }
}

public record GeneratedRuleResponse
{
    public Guid GenerationId { get; init; }
    public string UserPrompt { get; init; } = default!;
    public RuleDefinition RuleDefinition { get; init; } = default!;
    public bool CanEdit { get; init; }
    public string Message { get; init; } = default!;
}

public record CreditAnalysisApiRequest
{
    public Dictionary<string, object> ApplicationData { get; init; } = new();
    public string Decision { get; init; } = default!;
    public decimal RiskScore { get; init; }
    public string RiskCategory { get; init; } = default!;
    public string? ProductCode { get; init; }
    public List<DeviationDto> Deviations { get; init; } = new();
}

public record DeviationAnalysisApiRequest
{
    public Dictionary<string, object> ApplicationData { get; init; } = new();
    public List<DeviationDto> Deviations { get; init; } = new();
}

public record DeviationDto
{
    public string Code { get; init; } = default!;
    public string Name { get; init; } = default!;
    public string Severity { get; init; } = default!;
    public string Reason { get; init; } = default!;
    public string? ActualValue { get; init; }
    public string? ExpectedValue { get; init; }
}

public record AcceptRuleRequest
{
    public Guid? CategoryId { get; init; }
}

public class AiGeneratedRuleRecord
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string UserPrompt { get; set; } = default!;
    public RuleDefinition GeneratedRule { get; set; } = default!;
    public Guid? RuleId { get; set; }
    public bool? IsAccepted { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public interface IAiRuleGeneratorRepository
{
    Task<Guid> SaveGeneratedRuleAsync(AiGeneratedRuleRecord record, CancellationToken ct = default);
    Task<bool> AcceptRuleAsync(Guid generationId, Guid tenantId, Guid? categoryId, Guid userId, CancellationToken ct = default);
    Task<List<AiGeneratedRuleRecord>> GetHistoryAsync(Guid tenantId, int page, int pageSize, CancellationToken ct = default);
}

public interface IFieldCatalogRepository
{
    Task<List<string>> GetFieldPathsAsync(Guid tenantId, CancellationToken ct = default);
    Task<List<FieldCatalogDto>> GetCatalogAsync(Guid tenantId, string? category = null, CancellationToken ct = default);
}

public record FieldCatalogDto
{
    public string FieldPath { get; init; } = default!;
    public string DisplayName { get; init; } = default!;
    public string DataType { get; init; } = default!;
    public string? Category { get; init; }
    public string? Description { get; init; }
}
