using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using OptimAI.BRE.RuleEngine.Domain;
using OptimAI.BRE.Shared.Domain;

namespace OptimAI.BRE.RuleEngine.Application;

public sealed class ConditionEvaluator : IConditionEvaluator
{
    private readonly IDynamicFieldResolver _fieldResolver;
    private readonly ILogger<ConditionEvaluator> _logger;

    public ConditionEvaluator(IDynamicFieldResolver fieldResolver, ILogger<ConditionEvaluator> logger)
    {
        _fieldResolver = fieldResolver;
        _logger = logger;
    }

    public bool Evaluate(ConditionNode condition, DynamicDataContext context)
    {
        try
        {
            var actualValue = ResolveValue(condition.Field!, context);
            var expectedValue = ResolveExpectedValue(condition, context);

            return condition.Operator switch
            {
                ComparisonOperator.Equals => AreEqual(actualValue, expectedValue),
                ComparisonOperator.NotEquals => !AreEqual(actualValue, expectedValue),
                ComparisonOperator.GreaterThan => Compare(actualValue, expectedValue) > 0,
                ComparisonOperator.GreaterThanOrEqual => Compare(actualValue, expectedValue) >= 0,
                ComparisonOperator.LessThan => Compare(actualValue, expectedValue) < 0,
                ComparisonOperator.LessThanOrEqual => Compare(actualValue, expectedValue) <= 0,
                ComparisonOperator.Between => EvaluateBetween(actualValue, condition, context),
                ComparisonOperator.NotBetween => !EvaluateBetween(actualValue, condition, context),
                ComparisonOperator.In => EvaluateIn(actualValue, condition.Value),
                ComparisonOperator.NotIn => !EvaluateIn(actualValue, condition.Value),
                ComparisonOperator.Contains => EvaluateContains(actualValue, expectedValue),
                ComparisonOperator.NotContains => !EvaluateContains(actualValue, expectedValue),
                ComparisonOperator.StartsWith => EvaluateStartsWith(actualValue, expectedValue),
                ComparisonOperator.EndsWith => EvaluateEndsWith(actualValue, expectedValue),
                ComparisonOperator.IsNull => actualValue == null,
                ComparisonOperator.IsNotNull => actualValue != null,
                ComparisonOperator.IsTrue => IsTruthy(actualValue),
                ComparisonOperator.IsFalse => !IsTruthy(actualValue),
                ComparisonOperator.Regex => EvaluateRegex(actualValue, expectedValue?.ToString()),
                _ => false
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Condition evaluation failed for field {Field}", condition.Field);
            return false;
        }
    }

    private object? ResolveValue(string field, DynamicDataContext context)
    {
        return _fieldResolver.TryResolve(field, context, out var value) ? value : null;
    }

    private object? ResolveExpectedValue(ConditionNode condition, DynamicDataContext context)
    {
        return condition.ValueType switch
        {
            OptimAI.BRE.Shared.Domain.ValueType.Field when condition.ReferenceField != null => ResolveValue(condition.ReferenceField, context),
            OptimAI.BRE.Shared.Domain.ValueType.Literal => condition.Value,
            _ => condition.Value
        };
    }

    private static bool AreEqual(object? left, object? right)
    {
        if (left == null && right == null) return true;
        if (left == null || right == null) return false;

        if (TryToDecimal(left, out var ld) && TryToDecimal(right, out var rd))
            return ld == rd;

        return left.ToString()?.Equals(right.ToString(), StringComparison.OrdinalIgnoreCase) ?? false;
    }

    private static int Compare(object? left, object? right)
    {
        if (left == null && right == null) return 0;
        if (left == null) return -1;
        if (right == null) return 1;

        if (TryToDecimal(left, out var ld) && TryToDecimal(right, out var rd))
            return ld.CompareTo(rd);

        if (left is DateTime dl && right is DateTime dr)
            return dl.CompareTo(dr);

        return string.Compare(left.ToString(), right.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private bool EvaluateBetween(object? actual, ConditionNode condition, DynamicDataContext context)
    {
        var value1 = ResolveExpectedValue(condition, context);
        var value2 = condition.Value2 != null ? (object)condition.Value2 : null;

        if (value1 == null || value2 == null) return false;
        return Compare(actual, value1) >= 0 && Compare(actual, value2) <= 0;
    }

    private static bool EvaluateIn(object? actual, object? values)
    {
        if (actual == null || values == null) return false;

        IEnumerable<object> list = values switch
        {
            IEnumerable<object> e => e,
            string s => s.Split(',').Select(x => (object)x.Trim()),
            _ => new[] { values }
        };

        return list.Any(v => AreEqual(actual, v));
    }

    private static bool EvaluateContains(object? actual, object? expected)
    {
        if (actual == null || expected == null) return false;

        if (actual is IEnumerable<object> list)
            return list.Any(item => AreEqual(item, expected));

        return actual.ToString()?.Contains(expected.ToString() ?? "", StringComparison.OrdinalIgnoreCase) ?? false;
    }

    private static bool EvaluateStartsWith(object? actual, object? expected)
        => actual?.ToString()?.StartsWith(expected?.ToString() ?? "", StringComparison.OrdinalIgnoreCase) ?? false;

    private static bool EvaluateEndsWith(object? actual, object? expected)
        => actual?.ToString()?.EndsWith(expected?.ToString() ?? "", StringComparison.OrdinalIgnoreCase) ?? false;

    private static bool EvaluateRegex(object? actual, string? pattern)
    {
        if (actual == null || pattern == null) return false;
        return Regex.IsMatch(actual.ToString()!, pattern, RegexOptions.IgnoreCase);
    }

    private static bool IsTruthy(object? value) => value switch
    {
        bool b => b,
        decimal d => d != 0,
        int i => i != 0,
        string s => !string.IsNullOrEmpty(s) && s != "0" && s.ToLower() != "false",
        null => false,
        _ => true
    };

    private static bool TryToDecimal(object value, out decimal result)
    {
        result = 0;
        return value switch
        {
            decimal d => (result = d) == d,
            double d => (result = (decimal)d) == result,
            float f => (result = (decimal)f) == result,
            int i => (result = i) == i,
            long l => (result = l) == l,
            string s => decimal.TryParse(s, out result),
            _ => false
        };
    }
}
