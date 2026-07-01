using Microsoft.Extensions.Logging;
using OptimAI.BRE.RuleEngine.Domain;
using OptimAI.BRE.Shared.Domain;
using ExecutionContext = OptimAI.BRE.RuleEngine.Domain.ExecutionContext;

namespace OptimAI.BRE.RuleEngine.Application;

public sealed class ActionExecutor : IActionExecutor
{
    private readonly IDeviationTypeRepository _deviationTypeRepo;
    private readonly ILogger<ActionExecutor> _logger;

    public ActionExecutor(IDeviationTypeRepository deviationTypeRepo, ILogger<ActionExecutor> logger)
    {
        _deviationTypeRepo = deviationTypeRepo;
        _logger = logger;
    }

    public async Task ExecuteAsync(RuleAction action, ExecutionContext context, RuleExecutionState state)
    {
        try
        {
            switch (action.Type)
            {
                case ActionType.SetDecision:
                    ExecuteSetDecision(action, state);
                    break;

                case ActionType.SetRisk:
                    ExecuteSetRisk(action, state);
                    break;

                case ActionType.SetTrafficLight:
                    ExecuteSetTrafficLight(action, state);
                    break;

                case ActionType.AddDeviation:
                    await ExecuteAddDeviationAsync(action, context, state);
                    break;

                case ActionType.SetField:
                    ExecuteSetField(action, state);
                    break;

                case ActionType.AddTag:
                    ExecuteAddTag(action, state);
                    break;

                case ActionType.SetScore:
                    ExecuteSetScore(action, state);
                    break;

                default:
                    _logger.LogWarning("Unknown action type: {ActionType}", action.Type);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Action execution failed: {ActionType}", action.Type);
        }
    }

    private static void ExecuteSetDecision(RuleAction action, RuleExecutionState state)
    {
        if (Enum.TryParse<Decision>(action.Value, true, out var decision))
        {
            // Higher severity decisions take priority: Reject > Deviation > Refer > Approve
            var priority = new Dictionary<Decision, int>
            {
                [Decision.Reject] = 4,
                [Decision.Deviation] = 3,
                [Decision.Refer] = 2,
                [Decision.Approve] = 1,
                [Decision.Pending] = 0
            };

            if (priority.GetValueOrDefault(decision, 0) > priority.GetValueOrDefault(state.GlobalState.CurrentDecision, 0))
            {
                state.GlobalState.CurrentDecision = decision;

                if (decision == Decision.Reject)
                    state.GlobalState.ShouldStop = false; // Continue collecting all rejection reasons
            }
        }
    }

    private static void ExecuteSetRisk(RuleAction action, RuleExecutionState state)
    {
        if (Enum.TryParse<RiskCategory>(action.Value, true, out var risk))
        {
            // Escalate risk level only
            var currentRisk = state.GlobalState.OutputFields.GetValueOrDefault("_riskCategory") as string;
            if (currentRisk == null || ShouldEscalate(currentRisk, risk.ToString()))
                state.GlobalState.OutputFields["_riskCategory"] = risk.ToString();
        }
    }

    private static void ExecuteSetTrafficLight(RuleAction action, RuleExecutionState state)
    {
        if (Enum.TryParse<TrafficLight>(action.Value, true, out var tl))
            state.GlobalState.TrafficLight = tl;
    }

    private async Task ExecuteAddDeviationAsync(RuleAction action, ExecutionContext context, RuleExecutionState state)
    {
        var deviationCode = action.Value ?? action.Parameters.GetValueOrDefault("code");
        if (deviationCode == null) return;

        var deviationType = await _deviationTypeRepo.FindByCodeAsync(context.TenantId, deviationCode);

        var deviation = new ExecutionDeviation
        {
            TenantId = context.TenantId,
            DeviationCode = deviationCode,
            DeviationName = deviationType?.DeviationName ?? deviationCode,
            Severity = deviationType?.DefaultSeverity ?? ParseSeverity(action.Parameters.GetValueOrDefault("severity")),
            Reason = action.Parameters.GetValueOrDefault("reason") ?? deviationType?.Description ?? "Policy deviation detected",
            FieldPath = action.Parameters.GetValueOrDefault("field"),
            RecommendedAction = action.Parameters.GetValueOrDefault("action") ?? deviationType?.RecommendedAction,
            DeviationTypeId = deviationType?.Id
        };

        // Resolve field values for context
        if (deviation.FieldPath != null)
        {
            var val = context.Data.GetValue(deviation.FieldPath);
            deviation.ActualValue = val?.ToString();
        }

        state.GlobalState.Deviations.Add(deviation);

        // Automatically set decision to DEVIATION if not already REJECT
        if (state.GlobalState.CurrentDecision != Decision.Reject)
            state.GlobalState.CurrentDecision = Decision.Deviation;
    }

    private static void ExecuteSetField(RuleAction action, RuleExecutionState state)
    {
        if (action.Field != null)
            state.GlobalState.OutputFields[action.Field] = action.Value ?? "";
    }

    private static void ExecuteAddTag(RuleAction action, RuleExecutionState state)
    {
        if (action.Value != null && !state.GlobalState.Tags.Contains(action.Value))
            state.GlobalState.Tags.Add(action.Value);
    }

    private static void ExecuteSetScore(RuleAction action, RuleExecutionState state)
    {
        if (decimal.TryParse(action.Value, out var score))
        {
            // Score adjustments: can be absolute or relative (+10, -5)
            if (action.Value!.StartsWith('+') || action.Value.StartsWith('-'))
                state.GlobalState.RiskScore = Math.Clamp(state.GlobalState.RiskScore + score, 0, 100);
            else
                state.GlobalState.RiskScore = Math.Clamp(score, 0, 100);
        }
    }

    private static bool ShouldEscalate(string current, string incoming)
    {
        var order = new[] { "Low", "Medium", "High", "Critical" };
        var currentIdx = Array.IndexOf(order, current);
        var incomingIdx = Array.IndexOf(order, incoming);
        return incomingIdx > currentIdx;
    }

    private static Severity ParseSeverity(string? value) =>
        Enum.TryParse<Severity>(value, true, out var s) ? s : Severity.Medium;
}

public interface IDeviationTypeRepository
{
    Task<DeviationType?> FindByCodeAsync(Guid tenantId, string code, CancellationToken ct = default);
    Task<IReadOnlyList<DeviationType>> GetAllActiveAsync(Guid tenantId, CancellationToken ct = default);
}
