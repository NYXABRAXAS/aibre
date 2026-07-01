using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OptimAI.BRE.Shared.Domain;
using System.Text.Json;

namespace OptimAI.BRE.AIEngine.Application;

public sealed class AiCreditAnalystService : IAiCreditAnalystService
{
    private readonly OpenAIClient _openAiClient;
    private readonly AiOptions _options;
    private readonly IAiPromptRepository _promptRepo;
    private readonly ILogger<AiCreditAnalystService> _logger;

    public AiCreditAnalystService(
        OpenAIClient openAiClient,
        IOptions<AiOptions> options,
        IAiPromptRepository promptRepo,
        ILogger<AiCreditAnalystService> logger)
    {
        _openAiClient = openAiClient;
        _options = options.Value;
        _promptRepo = promptRepo;
        _logger = logger;
    }

    public async Task<AiAnalysis> AnalyzeCreditAsync(CreditAnalysisRequest request, CancellationToken ct = default)
    {
        var systemPrompt = await _promptRepo.GetPromptAsync("CREDIT_ANALYSIS_SYSTEM") ??
            GetDefaultCreditAnalysisSystemPrompt();

        var userPrompt = BuildCreditAnalysisPrompt(request);

        try
        {
            var chatOptions = new ChatCompletionsOptions
            {
                DeploymentName = _options.ModelName,
                Temperature = 0.3f,
                MaxTokens = 2500,
                Messages =
                {
                    new ChatRequestSystemMessage(systemPrompt),
                    new ChatRequestUserMessage(userPrompt)
                },
                ResponseFormat = ChatCompletionsResponseFormat.JsonObject
            };

            var response = await _openAiClient.GetChatCompletionsAsync(chatOptions, ct);
            var content = response.Value.Choices[0].Message.Content;

            var analysis = JsonSerializer.Deserialize<AiAnalysisJson>(content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return MapToAiAnalysis(analysis!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI credit analysis failed");
            return GetFallbackAnalysis(request);
        }
    }

    public async Task<RuleDefinition?> GenerateRuleFromPromptAsync(RuleGenerationRequest request, CancellationToken ct = default)
    {
        var systemPrompt = await _promptRepo.GetPromptAsync("RULE_GENERATION_SYSTEM") ??
            GetRuleGenerationSystemPrompt();

        var fieldCatalogJson = JsonSerializer.Serialize(request.AvailableFields?.Take(50));

        var userPrompt = $"""
            Generate a BRE rule definition from this natural language description:

            User Input: {request.UserPrompt}

            Available Fields:
            {fieldCatalogJson}

            Product Type: {request.ProductType ?? "GENERAL"}

            Return a JSON object with this exact structure:
            {{
                "ruleName": "string",
                "ruleCode": "string (snake_case)",
                "ruleType": "string (ELIGIBILITY|CREDIT|BUREAU|FI|VALUATION|FRAUD|COMPLIANCE)",
                "description": "string",
                "conditions": {{
                    "operator": "AND|OR",
                    "rules": [
                        {{
                            "id": "uuid",
                            "isGroup": false,
                            "field": "field.path",
                            "operator": "LESS_THAN|GREATER_THAN|EQUALS|etc",
                            "value": "value",
                            "valueType": "Literal"
                        }}
                    ]
                }},
                "actions": [
                    {{
                        "id": "uuid",
                        "type": "SetDecision|SetRisk|AddDeviation|SetTrafficLight",
                        "value": "string"
                    }}
                ]
            }}
            """;

        try
        {
            var chatOptions = new ChatCompletionsOptions
            {
                DeploymentName = _options.ModelName,
                Temperature = 0.2f,
                MaxTokens = 2000,
                Messages =
                {
                    new ChatRequestSystemMessage(systemPrompt),
                    new ChatRequestUserMessage(userPrompt)
                },
                ResponseFormat = ChatCompletionsResponseFormat.JsonObject
            };

            var response = await _openAiClient.GetChatCompletionsAsync(chatOptions, ct);
            var content = response.Value.Choices[0].Message.Content;

            return JsonSerializer.Deserialize<RuleDefinition>(content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI rule generation failed for prompt: {Prompt}", request.UserPrompt);
            return null;
        }
    }

    public async Task<DeviationAnalysis> AnalyzeDeviationsAsync(DeviationAnalysisRequest request, CancellationToken ct = default)
    {
        var prompt = $"""
            You are a credit risk expert analyzing loan application deviations.

            Application Data:
            {JsonSerializer.Serialize(request.ApplicationData, new JsonSerializerOptions { WriteIndented = true })}

            Detected Deviations:
            {JsonSerializer.Serialize(request.Deviations, new JsonSerializerOptions { WriteIndented = true })}

            Analyze these deviations and provide:
            1. Combined risk impact
            2. Whether deviations can be mitigated
            3. Recommended approval conditions
            4. Documents needed to override each deviation

            Respond in JSON format with fields: riskImpact, canMitigate, approvalConditions (array), documentRequirements (array), overallRecommendation.
            """;

        try
        {
            var chatOptions = new ChatCompletionsOptions
            {
                DeploymentName = _options.ModelName,
                Temperature = 0.3f,
                MaxTokens = 1500,
                Messages = { new ChatRequestUserMessage(prompt) },
                ResponseFormat = ChatCompletionsResponseFormat.JsonObject
            };

            var response = await _openAiClient.GetChatCompletionsAsync(chatOptions, ct);
            var content = response.Value.Choices[0].Message.Content;

            return JsonSerializer.Deserialize<DeviationAnalysis>(content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Deviation analysis failed");
            return new DeviationAnalysis
            {
                RiskImpact = "Unable to analyze - manual review required",
                CanMitigate = false,
                OverallRecommendation = "Manual underwriter review required"
            };
        }
    }

    private static string BuildCreditAnalysisPrompt(CreditAnalysisRequest request)
    {
        var deviationList = request.Deviations.Any()
            ? string.Join("\n", request.Deviations.Select(d => $"- {d.DeviationName} ({d.Severity}): {d.Reason}"))
            : "None";

        return $"""
            Analyze this loan application and provide a comprehensive credit assessment:

            === APPLICANT PROFILE ===
            Age: {GetField(request.Data, "applicant.age")}
            Employment Type: {GetField(request.Data, "employment.type")}
            Monthly Income: ₹{GetField(request.Data, "employment.monthly_income")}

            === BUREAU PROFILE ===
            CIBIL Score: {GetField(request.Data, "bureau.cibil_score")}
            Max DPD (24M): {GetField(request.Data, "bureau.max_dpd_24m")}
            Total Active Loans: {GetField(request.Data, "bureau.total_active_loans")}
            Total EMI Obligation: ₹{GetField(request.Data, "bureau.total_emi_obligation")}
            Written Off: {GetField(request.Data, "bureau.written_off_amount")}

            === LOAN DETAILS ===
            Loan Amount: ₹{GetField(request.Data, "loan.amount")}
            Product: {request.ProductCode}
            FOIR: {GetField(request.Data, "ratios.foir")}%
            LTV: {GetField(request.Data, "ratios.ltv")}%

            === BRE DECISION ===
            Final Decision: {request.Decision}
            Risk Score: {request.RiskScore}/100
            Risk Category: {request.RiskCategory}

            === DEVIATIONS ===
            {deviationList}

            Provide response in JSON with these exact fields:
            riskSummary, creditSummary, strengths (array), weaknesses (array),
            deviationsSummary, approvalRecommendation, rejectionReasons (array),
            additionalDocuments (array), underwritingNotes, confidenceScore (0-1)
            """;
    }

    private static string? GetField(Dictionary<string, object> data, string path)
    {
        var parts = path.Split('.');
        object? current = data;

        foreach (var part in parts)
        {
            if (current is Dictionary<string, object> dict)
            {
                current = dict.TryGetValue(part, out var val) ? val : null;
            }
            else return null;
        }

        return current?.ToString();
    }

    private static AiAnalysis MapToAiAnalysis(AiAnalysisJson json) => new()
    {
        RiskSummary = json.RiskSummary ?? "Risk analysis not available",
        CreditSummary = json.CreditSummary ?? "Credit summary not available",
        Strengths = json.Strengths ?? new(),
        Weaknesses = json.Weaknesses ?? new(),
        DeviationsSummary = json.DeviationsSummary ?? "",
        ApprovalRecommendation = json.ApprovalRecommendation ?? "",
        RejectionReasons = json.RejectionReasons ?? new(),
        AdditionalDocuments = json.AdditionalDocuments ?? new(),
        UnderwritingNotes = json.UnderwritingNotes ?? "",
        ConfidenceScore = json.ConfidenceScore
    };

    private static AiAnalysis GetFallbackAnalysis(CreditAnalysisRequest request) => new()
    {
        RiskSummary = $"Risk Score: {request.RiskScore}/100 - {request.RiskCategory} Risk",
        CreditSummary = $"Application decision: {request.Decision}. Manual review recommended.",
        Strengths = new List<string> { "Application data available for review" },
        Weaknesses = request.Deviations.Select(d => d.DeviationName).ToList(),
        DeviationsSummary = $"{request.Deviations.Count} deviation(s) detected",
        ApprovalRecommendation = request.Decision == "APPROVE" ? "Recommend approval" : "Further review required",
        RejectionReasons = new(),
        AdditionalDocuments = new(),
        UnderwritingNotes = "AI analysis unavailable - manual underwriter assessment required",
        ConfidenceScore = 0
    };

    private static string GetDefaultCreditAnalysisSystemPrompt() => """
        You are OPTIM AI, an expert credit risk analyst with 20+ years experience in banking, NBFCs, and lending.
        You specialize in vehicle finance, tractor loans, MSME lending, and consumer credit.

        Your role: Analyze loan applications and provide objective, data-driven credit assessments.

        Guidelines:
        - Be concise but comprehensive
        - Use Indian banking terminology and regulations
        - Reference RBI guidelines where applicable
        - Identify genuine strengths, not just positives
        - Be specific about weaknesses and their impact
        - Recommend specific documents to mitigate risks
        - Underwriting notes should be actionable for credit managers

        Always respond in valid JSON format only.
        """;

    private static string GetRuleGenerationSystemPrompt() => """
        You are OPTIM AI Rule Generator. You convert natural language credit policy descriptions into structured BRE rule definitions.

        Field naming conventions:
        - Bureau fields: bureau.cibil_score, bureau.max_dpd_24m, bureau.total_active_loans, bureau.written_off_amount
        - Income fields: employment.monthly_income, employment.annual_income, employment.vintage_months
        - Ratio fields: ratios.foir, ratios.ltv, ratios.dscr
        - Applicant fields: applicant.age, applicant.gender, applicant.pan_number
        - Vehicle fields: vehicle.age_years, vehicle.valuation, vehicle.type
        - FI fields: fi.verified, fi.negative, fi.address_match
        - Loan fields: loan.amount, loan.tenure_months, loan.emi

        Operator values: EQUALS, NOT_EQUALS, GREATER_THAN, GREATER_THAN_OR_EQUAL, LESS_THAN, LESS_THAN_OR_EQUAL, BETWEEN, IN, NOT_IN, IS_NULL, IS_NOT_NULL, IS_TRUE, IS_FALSE

        Action types: SetDecision (APPROVE/REJECT/DEVIATION/REFER), SetRisk (LOW/MEDIUM/HIGH/CRITICAL), AddDeviation (deviation code), SetTrafficLight (GREEN/AMBER/RED)

        Always generate valid, executable rule definitions. Use snake_case for ruleCode.
        Respond with valid JSON only.
        """;
}

// ============================================================
// SUPPORTING TYPES
// ============================================================

public interface IAiCreditAnalystService
{
    Task<AiAnalysis> AnalyzeCreditAsync(CreditAnalysisRequest request, CancellationToken ct = default);
    Task<RuleDefinition?> GenerateRuleFromPromptAsync(RuleGenerationRequest request, CancellationToken ct = default);
    Task<DeviationAnalysis> AnalyzeDeviationsAsync(DeviationAnalysisRequest request, CancellationToken ct = default);
}

public class CreditAnalysisRequest
{
    public Dictionary<string, object> Data { get; set; } = new();
    public string Decision { get; set; } = default!;
    public decimal RiskScore { get; set; }
    public string RiskCategory { get; set; } = default!;
    public string? ProductCode { get; set; }
    public List<ExecutionDeviation> Deviations { get; set; } = new();
}

public class RuleGenerationRequest
{
    public string UserPrompt { get; set; } = default!;
    public string? ProductType { get; set; }
    public List<string>? AvailableFields { get; set; }
    public Guid TenantId { get; set; }
    public Guid CreatedBy { get; set; }
}

public class DeviationAnalysisRequest
{
    public Dictionary<string, object> ApplicationData { get; set; } = new();
    public List<ExecutionDeviation> Deviations { get; set; } = new();
}

public class DeviationAnalysis
{
    public string RiskImpact { get; set; } = default!;
    public bool CanMitigate { get; set; }
    public List<string> ApprovalConditions { get; set; } = new();
    public List<string> DocumentRequirements { get; set; } = new();
    public string OverallRecommendation { get; set; } = default!;
}

public class AiOptions
{
    public string Endpoint { get; set; } = default!;
    public string ApiKey { get; set; } = default!;
    public string ModelName { get; set; } = "gpt-4o";
    public bool UseAzureOpenAI { get; set; } = true;
}

public interface IAiPromptRepository
{
    Task<string?> GetPromptAsync(string promptCode, CancellationToken ct = default);
}

private class AiAnalysisJson
{
    public string? RiskSummary { get; set; }
    public string? CreditSummary { get; set; }
    public List<string>? Strengths { get; set; }
    public List<string>? Weaknesses { get; set; }
    public string? DeviationsSummary { get; set; }
    public string? ApprovalRecommendation { get; set; }
    public List<string>? RejectionReasons { get; set; }
    public List<string>? AdditionalDocuments { get; set; }
    public string? UnderwritingNotes { get; set; }
    public double ConfidenceScore { get; set; }
}
