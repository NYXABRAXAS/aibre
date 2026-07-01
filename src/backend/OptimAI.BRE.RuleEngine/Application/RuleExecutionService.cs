using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OptimAI.BRE.RuleEngine.Domain;
using OptimAI.BRE.Shared.Domain;
using ExecutionContext = OptimAI.BRE.RuleEngine.Domain.ExecutionContext;

namespace OptimAI.BRE.RuleEngine.Application;

public sealed class RuleExecutionService : IRuleExecutionService
{
    private readonly IRuleLoader _ruleLoader;
    private readonly IConditionEvaluator _conditionEvaluator;
    private readonly IActionExecutor _actionExecutor;
    private readonly IRiskScoringEngine _riskScoringEngine;
    private readonly ILogger<RuleExecutionService> _logger;

    public RuleExecutionService(
        IRuleLoader ruleLoader,
        IConditionEvaluator conditionEvaluator,
        IActionExecutor actionExecutor,
        IRiskScoringEngine riskScoringEngine,
        ILogger<RuleExecutionService> logger)
    {
        _ruleLoader = ruleLoader;
        _conditionEvaluator = conditionEvaluator;
        _actionExecutor = actionExecutor;
        _riskScoringEngine = riskScoringEngine;
        _logger = logger;
    }

    public async Task<ExecutionResult> ExecuteAsync(ExecutionContext context, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var state = new ExecutionState();
        var ruleResults = new List<RuleEvaluationResult>();

        try
        {
            var rules = await _ruleLoader.LoadRulesAsync(new RuleLoadRequest
            {
                TenantId = context.TenantId,
                ProductCode = context.ProductCode,
                BranchCode = context.BranchCode,
                StageCode = context.StageCode,
                RuleSetId = context.RuleSetId,
                PublishedOnly = true
            }, ct);

            _logger.LogInformation("Executing {Count} rules for tenant {TenantId}, correlation {CorrelationId}",
                rules.Count, context.TenantId, context.CorrelationId);

            var executionState = new RuleExecutionState
            {
                GlobalState = state,
                Data = context.Data,
                TenantId = context.TenantId
            };

            var orderedRules = rules.OrderBy(r => r.Priority).ToList();
            int executionOrder = 0;

            foreach (var rule in orderedRules)
            {
                if (ct.IsCancellationRequested) break;
                if (state.ShouldStop) break;

                var ruleResult = await EvaluateRuleAsync(rule, context, ct);
                ruleResult.ExecutionOrder = ++executionOrder;
                ruleResults.Add(ruleResult);

                if (ruleResult.IsMatched && !ruleResult.HasError)
                {
                    foreach (var action in rule.CurrentVersion!.RuleDefinition.Actions)
                    {
                        await _actionExecutor.ExecuteAsync(action, context, executionState);
                    }

                    if (context.Options.StopOnFirstReject && state.CurrentDecision == Decision.Reject)
                    {
                        state.ShouldStop = true;
                    }
                }
            }

            // Calculate risk score
            var riskResult = await _riskScoringEngine.CalculateAsync(context, ruleResults);
            state.RiskScore = riskResult.Score;

            // Determine final traffic light
            state.TrafficLight = DetermineTrafficLight(state.CurrentDecision, riskResult.Category);

            sw.Stop();

            return BuildResult(context, state, ruleResults, riskResult, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rule execution failed for tenant {TenantId}", context.TenantId);
            throw;
        }
    }

    public async Task<RuleEvaluationResult> EvaluateRuleAsync(Rule rule, ExecutionContext context, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var result = new RuleEvaluationResult
        {
            RuleId = rule.Id,
            RuleCode = rule.RuleCode,
            RuleName = rule.RuleName,
            VersionNumber = rule.CurrentVersion?.VersionNumber ?? 0
        };

        try
        {
            if (rule.CurrentVersion?.RuleDefinition == null)
            {
                result.HasError = true;
                result.ErrorMessage = "Rule has no published version";
                return result;
            }

            var definition = rule.CurrentVersion.RuleDefinition;

            // Evaluate conditions with detailed tracking
            var (matched, conditionResults) = await EvaluateConditionGroupDetailedAsync(
                definition.Conditions, context.Data);

            result.IsMatched = matched;
            result.ConditionResults = conditionResults;
        }
        catch (Exception ex)
        {
            result.HasError = true;
            result.ErrorMessage = ex.Message;
            _logger.LogWarning(ex, "Error evaluating rule {RuleCode}", rule.RuleCode);
        }
        finally
        {
            sw.Stop();
            result.ExecutionMs = (int)sw.ElapsedMilliseconds;
        }

        return result;
    }

    public async Task<bool> EvaluateConditionGroupAsync(ConditionGroup group, DynamicDataContext data, CancellationToken ct = default)
    {
        var results = new List<bool>();

        foreach (var node in group.Rules)
        {
            bool nodeResult;
            if (node.IsGroup && node.Group != null)
                nodeResult = await EvaluateConditionGroupAsync(node.Group, data, ct);
            else
                nodeResult = _conditionEvaluator.Evaluate(node, data);

            results.Add(nodeResult);
        }

        return group.Operator switch
        {
            LogicalOperator.And => results.All(r => r),
            LogicalOperator.Or => results.Any(r => r),
            LogicalOperator.Not => results.Count > 0 && !results[0],
            _ => false
        };
    }

    private async Task<(bool matched, List<ConditionEvaluationResult> details)> EvaluateConditionGroupDetailedAsync(
        ConditionGroup group, DynamicDataContext data)
    {
        var details = new List<ConditionEvaluationResult>();
        var results = new List<bool>();

        foreach (var node in group.Rules)
        {
            bool nodeResult;
            if (node.IsGroup && node.Group != null)
            {
                var (groupResult, groupDetails) = await EvaluateConditionGroupDetailedAsync(node.Group, data);
                nodeResult = groupResult;
                details.AddRange(groupDetails);
            }
            else
            {
                var actualValue = data.GetValue(node.Field!);
                nodeResult = _conditionEvaluator.Evaluate(node, data);
                details.Add(new ConditionEvaluationResult
                {
                    ConditionId = node.Id,
                    Field = node.Field ?? "",
                    Operator = node.Operator?.ToString() ?? "",
                    ExpectedValue = node.Value,
                    ActualValue = actualValue,
                    Result = nodeResult
                });
            }
            results.Add(nodeResult);
        }

        bool finalResult = group.Operator switch
        {
            LogicalOperator.And => results.All(r => r),
            LogicalOperator.Or => results.Any(r => r),
            LogicalOperator.Not => results.Count > 0 && !results[0],
            _ => false
        };

        return (finalResult, details);
    }

    private static TrafficLight DetermineTrafficLight(Decision decision, RiskCategory riskCategory)
    {
        return decision switch
        {
            Decision.Approve when riskCategory is RiskCategory.Low or RiskCategory.Medium => TrafficLight.Green,
            Decision.Reject => TrafficLight.Red,
            Decision.Deviation => TrafficLight.Amber,
            _ => riskCategory switch
            {
                RiskCategory.Critical => TrafficLight.Red,
                RiskCategory.High => TrafficLight.Amber,
                _ => TrafficLight.Green
            }
        };
    }

    private static ExecutionResult BuildResult(
        ExecutionContext context,
        ExecutionState state,
        List<RuleEvaluationResult> ruleResults,
        RiskScoreResult riskResult,
        long elapsedMs)
    {
        return new ExecutionResult
        {
            TenantId = context.TenantId,
            FinalDecision = state.CurrentDecision,
            RiskScore = state.RiskScore,
            RiskCategory = riskResult.Category,
            TrafficLight = state.TrafficLight,
            TotalRulesEvaluated = ruleResults.Count,
            RulesPassed = ruleResults.Count(r => r.IsMatched),
            RulesFailed = ruleResults.Count(r => !r.IsMatched),
            RulesSkipped = ruleResults.Count(r => r.HasError),
            DeviationsCount = state.Deviations.Count,
            ExecutionMs = (int)elapsedMs,
            RuleResults = ruleResults.Select(r => new RuleExecutionSummary
            {
                RuleId = r.RuleId,
                RuleCode = r.RuleCode,
                RuleName = r.RuleName,
                VersionNumber = r.VersionNumber,
                IsMatched = r.IsMatched,
                ConditionsEvaluated = r.ConditionResults,
                ActionsExecuted = r.ExecutedActions,
                ExecutionMs = r.ExecutionMs,
                ErrorMessage = r.ErrorMessage
            }).ToList(),
            FieldValues = state.OutputFields
        };
    }
}
