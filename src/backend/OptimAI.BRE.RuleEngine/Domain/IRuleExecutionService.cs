using OptimAI.BRE.Shared.Domain;
using System.Text.Json.Nodes;

namespace OptimAI.BRE.RuleEngine.Domain;

public interface IRuleExecutionService
{
    Task<ExecutionResult> ExecuteAsync(ExecutionContext context, CancellationToken ct = default);
    Task<RuleEvaluationResult> EvaluateRuleAsync(Rule rule, ExecutionContext context, CancellationToken ct = default);
    Task<bool> EvaluateConditionGroupAsync(ConditionGroup group, DynamicDataContext data, CancellationToken ct = default);
}

public interface IRuleLoader
{
    Task<IReadOnlyList<Rule>> LoadRulesAsync(RuleLoadRequest request, CancellationToken ct = default);
    Task<Rule?> LoadRuleByCodeAsync(Guid tenantId, string ruleCode, CancellationToken ct = default);
}

public interface IDynamicFieldResolver
{
    object? Resolve(string fieldPath, DynamicDataContext context);
    bool TryResolve(string fieldPath, DynamicDataContext context, out object? value);
}

public interface IConditionEvaluator
{
    bool Evaluate(ConditionNode condition, DynamicDataContext context);
}

public interface IActionExecutor
{
    Task ExecuteAsync(RuleAction action, ExecutionContext context, RuleExecutionState state);
}

public interface IRiskScoringEngine
{
    Task<RiskScoreResult> CalculateAsync(ExecutionContext context, List<RuleEvaluationResult> results);
}

// ============================================================
// EXECUTION CONTEXT & STATE
// ============================================================

public class ExecutionContext
{
    public Guid TenantId { get; set; }
    public string? CorrelationId { get; set; }
    public string? ApplicationId { get; set; }
    public string? ProductCode { get; set; }
    public string? BranchCode { get; set; }
    public string? StageCode { get; set; }
    public string? SourceSystem { get; set; }
    public Guid? RuleSetId { get; set; }
    public DynamicDataContext Data { get; set; } = default!;
    public ExecutionOptions Options { get; set; } = new();
    public Guid? RequestedBy { get; set; }
}

public class ExecutionOptions
{
    public bool EnableAiAnalysis { get; set; } = true;
    public bool EnableRiskScoring { get; set; } = true;
    public bool StopOnFirstReject { get; set; } = false;
    public int TimeoutMs { get; set; } = 5000;
    public bool IncludeConditionDetails { get; set; } = true;
    public bool EnableCaching { get; set; } = true;
}

public class DynamicDataContext
{
    private readonly JsonObject _root;

    public DynamicDataContext(Dictionary<string, object> data)
    {
        _root = ConvertToJsonObject(data);
    }

    public DynamicDataContext(JsonObject root)
    {
        _root = root;
    }

    public object? GetValue(string path)
    {
        var parts = path.Split('.');
        JsonNode? current = _root;

        foreach (var part in parts)
        {
            if (current is JsonObject obj)
            {
                if (!obj.TryGetPropertyValue(part, out current) || current == null)
                    return null;
            }
            else if (current is JsonArray arr && int.TryParse(part, out var idx))
            {
                current = idx < arr.Count ? arr[idx] : null;
            }
            else
            {
                return null;
            }
        }

        return ExtractValue(current);
    }

    public bool SetValue(string path, object value)
    {
        var parts = path.Split('.');
        JsonObject? current = _root;

        for (int i = 0; i < parts.Length - 1; i++)
        {
            if (current == null) return false;
            if (!current.TryGetPropertyValue(parts[i], out var next) || next is not JsonObject nextObj)
            {
                nextObj = new JsonObject();
                current[parts[i]] = nextObj;
            }
            current = nextObj as JsonObject;
        }

        if (current == null) return false;
        current[parts[^1]] = JsonValue.Create(value);
        return true;
    }

    public Dictionary<string, object> ToDictionary()
    {
        var result = new Dictionary<string, object>();
        FlattenJsonObject(_root, "", result);
        return result;
    }

    private static object? ExtractValue(JsonNode? node) => node switch
    {
        JsonValue v when v.TryGetValue<decimal>(out var d) => d,
        JsonValue v when v.TryGetValue<bool>(out var b) => b,
        JsonValue v when v.TryGetValue<string>(out var s) => s,
        JsonValue v when v.TryGetValue<DateTime>(out var dt) => dt,
        JsonArray arr => arr.Select(ExtractValue).ToList(),
        JsonObject obj => obj.ToDictionary(kv => kv.Key, kv => ExtractValue(kv.Value)!),
        null => null,
        _ => node.ToString()
    };

    private static JsonObject ConvertToJsonObject(Dictionary<string, object> data)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(data);
        return JsonNode.Parse(json) as JsonObject ?? new JsonObject();
    }

    private static void FlattenJsonObject(JsonObject obj, string prefix, Dictionary<string, object> result)
    {
        foreach (var kv in obj)
        {
            var key = string.IsNullOrEmpty(prefix) ? kv.Key : $"{prefix}.{kv.Key}";
            if (kv.Value is JsonObject nested)
                FlattenJsonObject(nested, key, result);
            else if (kv.Value != null)
                result[key] = ExtractValue(kv.Value)!;
        }
    }
}

public class ExecutionState
{
    public Decision CurrentDecision { get; set; } = Decision.Pending;
    public decimal RiskScore { get; set; } = 50m;
    public TrafficLight TrafficLight { get; set; } = TrafficLight.Amber;
    public List<ExecutionDeviation> Deviations { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public Dictionary<string, object> OutputFields { get; set; } = new();
    public bool ShouldStop { get; set; }
}

public class RuleExecutionState
{
    public ExecutionState GlobalState { get; set; } = default!;
    public DynamicDataContext Data { get; set; } = default!;
    public Guid TenantId { get; set; }
}

public class RuleEvaluationResult
{
    public Guid RuleId { get; set; }
    public string RuleCode { get; set; } = default!;
    public string RuleName { get; set; } = default!;
    public int VersionNumber { get; set; }
    public int ExecutionOrder { get; set; }
    public bool IsMatched { get; set; }
    public bool HasError { get; set; }
    public string? ErrorMessage { get; set; }
    public List<ConditionEvaluationResult> ConditionResults { get; set; } = new();
    public List<string> ExecutedActions { get; set; } = new();
    public int ExecutionMs { get; set; }
}

public class RuleLoadRequest
{
    public Guid TenantId { get; set; }
    public string? ProductCode { get; set; }
    public string? BranchCode { get; set; }
    public string? StageCode { get; set; }
    public Guid? RuleSetId { get; set; }
    public List<RuleType> RuleTypes { get; set; } = new();
    public bool PublishedOnly { get; set; } = true;
}

public class RiskScoreResult
{
    public decimal Score { get; set; }
    public RiskCategory Category { get; set; }
    public TrafficLight TrafficLight { get; set; }
    public Dictionary<string, decimal> ComponentScores { get; set; } = new();
}
