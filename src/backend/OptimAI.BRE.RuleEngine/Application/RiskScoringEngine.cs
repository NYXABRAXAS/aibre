using OptimAI.BRE.RuleEngine.Domain;
using OptimAI.BRE.Shared.Domain;
using ExecutionContext = OptimAI.BRE.RuleEngine.Domain.ExecutionContext;

namespace OptimAI.BRE.RuleEngine.Application;

/// <summary>
/// Multi-dimensional risk scoring engine.
/// Computes weighted risk score from 0–100 across bureau, income, FI, fraud, and vehicle dimensions.
/// </summary>
public sealed class RiskScoringEngine : IRiskScoringEngine
{
    private readonly IRiskWeightRepository _weightRepository;

    public RiskScoringEngine(IRiskWeightRepository weightRepository)
    {
        _weightRepository = weightRepository;
    }

    public async Task<RiskScoreResult> CalculateAsync(ExecutionContext context, List<RuleEvaluationResult> results)
    {
        var weights = await _weightRepository.GetWeightsAsync(context.TenantId, context.ProductCode);
        var data = context.Data;
        var componentScores = new Dictionary<string, decimal>();

        // ---- 1. Bureau Score Component (0–100, inverted for risk)
        componentScores["bureau"] = CalculateBureauScore(data, weights);

        // ---- 2. Income / FOIR Component
        componentScores["income"] = CalculateIncomeScore(data, weights);

        // ---- 3. FI Component
        componentScores["fi"] = CalculateFiScore(data, weights);

        // ---- 4. Fraud Component
        componentScores["fraud"] = CalculateFraudScore(data, weights);

        // ---- 5. Vehicle Component (for vehicle loans)
        componentScores["vehicle"] = CalculateVehicleScore(data, weights);

        // ---- 6. Employment Component
        componentScores["employment"] = CalculateEmploymentScore(data, weights);

        // ---- 7. Rule Match Penalty
        componentScores["rule_penalty"] = CalculateRulePenalty(results);

        // Weighted aggregate
        decimal totalWeight = weights.Sum(w => w.Value);
        decimal weightedScore = componentScores.Sum(cs =>
        {
            var weight = weights.GetValueOrDefault(cs.Key, GetDefaultWeight(cs.Key));
            return cs.Value * weight;
        });

        decimal finalScore = totalWeight > 0
            ? Math.Round(weightedScore / totalWeight, 2)
            : Math.Round(componentScores.Values.Average(), 2);

        finalScore = Math.Clamp(finalScore, 0, 100);

        return new RiskScoreResult
        {
            Score = finalScore,
            Category = ClassifyRisk(finalScore),
            TrafficLight = ScoreToTrafficLight(finalScore),
            ComponentScores = componentScores
        };
    }

    private static decimal CalculateBureauScore(DynamicDataContext data, Dictionary<string, decimal> weights)
    {
        var cibilScore = GetDecimal(data, "bureau.cibil_score");
        var maxDpd = GetDecimal(data, "bureau.max_dpd_24m");
        var writtenOff = GetBool(data, "bureau.written_off_amount");
        var suitFiled = GetBool(data, "bureau.suit_filed");
        var wilfulDefaulter = GetBool(data, "bureau.wilful_defaulter");

        decimal score = 50m; // default medium

        if (cibilScore.HasValue)
        {
            score = cibilScore.Value switch
            {
                >= 780 => 5,
                >= 750 => 15,
                >= 720 => 25,
                >= 700 => 35,
                >= 680 => 45,
                >= 650 => 60,
                >= 620 => 70,
                >= 600 => 80,
                _ => 90
            };
        }

        // DPD penalty
        if (maxDpd.HasValue)
        {
            score += maxDpd.Value switch
            {
                0 => 0,
                <= 30 => 10,
                <= 60 => 20,
                <= 90 => 30,
                _ => 40
            };
        }

        // Critical flags
        if (writtenOff) score = Math.Max(score, 85);
        if (suitFiled) score = Math.Max(score, 90);
        if (wilfulDefaulter) score = 100;

        return Math.Clamp(score, 0, 100);
    }

    private static decimal CalculateIncomeScore(DynamicDataContext data, Dictionary<string, decimal> weights)
    {
        var foir = GetDecimal(data, "ratios.foir");
        var income = GetDecimal(data, "employment.monthly_income");
        var emiObligation = GetDecimal(data, "bureau.total_emi_obligation");

        decimal score = 30m;

        if (foir.HasValue)
        {
            score = foir.Value switch
            {
                <= 30 => 10,
                <= 40 => 20,
                <= 50 => 35,
                <= 60 => 55,
                <= 70 => 70,
                <= 80 => 85,
                _ => 95
            };
        }
        else if (income.HasValue && emiObligation.HasValue && income.Value > 0)
        {
            var computedFoir = (emiObligation.Value / income.Value) * 100;
            score = computedFoir <= 40 ? 15 : computedFoir <= 60 ? 45 : 75;
        }

        return Math.Clamp(score, 0, 100);
    }

    private static decimal CalculateFiScore(DynamicDataContext data, Dictionary<string, decimal> weights)
    {
        var fiNegative = GetBool(data, "fi.negative");
        var addressMatch = GetBool(data, "fi.address_match");
        var mobileMatch = GetBool(data, "fi.mobile_match");
        var fiVerified = GetBool(data, "fi.verified");

        if (!fiVerified) return 40m; // unverified

        decimal score = 10m;
        if (fiNegative) score += 60;
        if (!addressMatch) score += 20;
        if (!mobileMatch) score += 10;

        return Math.Clamp(score, 0, 100);
    }

    private static decimal CalculateFraudScore(DynamicDataContext data, Dictionary<string, decimal> weights)
    {
        var fraudScore = GetDecimal(data, "fraud.score");
        var blacklisted = GetBool(data, "fraud.blacklisted");

        if (blacklisted) return 100;
        return fraudScore.HasValue ? Math.Clamp(fraudScore.Value, 0, 100) : 20m;
    }

    private static decimal CalculateVehicleScore(DynamicDataContext data, Dictionary<string, decimal> weights)
    {
        var vehicleAge = GetDecimal(data, "vehicle.age_years");
        var ltv = GetDecimal(data, "ratios.ltv");

        decimal score = 20m;

        if (vehicleAge.HasValue)
        {
            score += vehicleAge.Value switch
            {
                <= 2 => 0,
                <= 5 => 10,
                <= 8 => 20,
                <= 10 => 35,
                _ => 50
            };
        }

        if (ltv.HasValue)
        {
            score += ltv.Value switch
            {
                <= 70 => 0,
                <= 80 => 10,
                <= 90 => 20,
                _ => 35
            };
        }

        return Math.Clamp(score, 0, 100);
    }

    private static decimal CalculateEmploymentScore(DynamicDataContext data, Dictionary<string, decimal> weights)
    {
        var vintage = GetDecimal(data, "employment.vintage_months");
        var empType = data.GetValue("employment.type")?.ToString();

        decimal score = 30m;

        if (vintage.HasValue)
        {
            score = vintage.Value switch
            {
                >= 60 => 5,
                >= 36 => 15,
                >= 24 => 25,
                >= 12 => 40,
                >= 6 => 60,
                _ => 75
            };
        }

        // Salaried lower risk than self-employed
        if (empType?.ToLower() == "salaried") score = Math.Max(score - 10, 0);

        return Math.Clamp(score, 0, 100);
    }

    private static decimal CalculateRulePenalty(List<RuleEvaluationResult> results)
    {
        int matchedCount = results.Count(r => r.IsMatched);
        int totalCount = results.Count;
        if (totalCount == 0) return 0;

        return Math.Round((decimal)matchedCount / totalCount * 100, 2);
    }

    private static RiskCategory ClassifyRisk(decimal score) => score switch
    {
        <= 25 => RiskCategory.Low,
        <= 50 => RiskCategory.Medium,
        <= 75 => RiskCategory.High,
        _ => RiskCategory.Critical
    };

    private static TrafficLight ScoreToTrafficLight(decimal score) => score switch
    {
        <= 40 => TrafficLight.Green,
        <= 65 => TrafficLight.Amber,
        _ => TrafficLight.Red
    };

    private static decimal? GetDecimal(DynamicDataContext data, string path)
    {
        var val = data.GetValue(path);
        if (val == null) return null;
        if (decimal.TryParse(val.ToString(), out var d)) return d;
        return null;
    }

    private static bool GetBool(DynamicDataContext data, string path)
    {
        var val = data.GetValue(path);
        return val is true or "true" or 1;
    }

    private static decimal GetDefaultWeight(string component) => component switch
    {
        "bureau" => 30,
        "income" => 25,
        "fi" => 15,
        "fraud" => 15,
        "vehicle" => 10,
        "employment" => 10,
        "rule_penalty" => 5,
        _ => 10
    };
}

public interface IRiskWeightRepository
{
    Task<Dictionary<string, decimal>> GetWeightsAsync(Guid tenantId, string? productCode = null);
}
