using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using OptimAI.BRE.RuleEngine.Domain;
using OptimAI.BRE.Shared.Domain;
using System.Text.Json;

namespace OptimAI.BRE.RuleEngine.Infrastructure;

/// <summary>
/// Redis-backed rule loader with 5-minute cache TTL.
/// Cache key includes tenant + product + branch + stage for scope-aware loading.
/// </summary>
public sealed class CachedRuleLoader : IRuleLoader
{
    private readonly BREDbContext _db;
    private readonly IDistributedCache _cache;
    private readonly ILogger<CachedRuleLoader> _logger;

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public CachedRuleLoader(BREDbContext db, IDistributedCache cache, ILogger<CachedRuleLoader> logger)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Rule>> LoadRulesAsync(RuleLoadRequest request, CancellationToken ct = default)
    {
        var cacheKey = BuildCacheKey(request);

        var cached = await _cache.GetStringAsync(cacheKey, ct);
        if (cached != null)
        {
            _logger.LogDebug("Cache HIT for rules: {Key}", cacheKey);
            return JsonSerializer.Deserialize<List<Rule>>(cached, _jsonOpts) ?? new();
        }

        _logger.LogDebug("Cache MISS for rules: {Key}", cacheKey);
        var rules = await LoadFromDatabaseAsync(request, ct);

        await _cache.SetStringAsync(cacheKey,
            JsonSerializer.Serialize(rules, _jsonOpts),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            }, ct);

        return rules;
    }

    public async Task<Rule?> LoadRuleByCodeAsync(Guid tenantId, string ruleCode, CancellationToken ct = default)
    {
        return await _db.Rules
            .Include(r => r.CurrentVersion)
            .Include(r => r.Scopes)
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.RuleCode == ruleCode, ct);
    }

    private async Task<List<Rule>> LoadFromDatabaseAsync(RuleLoadRequest request, CancellationToken ct)
    {
        var query = _db.Rules
            .Include(r => r.CurrentVersion)
            .Include(r => r.Scopes)
            .Where(r => r.TenantId == request.TenantId);

        if (request.PublishedOnly)
            query = query.Where(r => r.IsPublished && r.Status == RuleStatus.Published);

        if (request.RuleTypes.Any())
            query = query.Where(r => request.RuleTypes.Contains(r.RuleType));

        if (request.RuleSetId.HasValue)
        {
            var ruleIds = await _db.RuleSetMembers
                .Where(m => m.SetId == request.RuleSetId.Value)
                .Select(m => m.RuleId)
                .ToListAsync(ct);

            query = query.Where(r => ruleIds.Contains(r.Id));
        }
        else
        {
            // Scope filtering: include GLOBAL rules + rules scoped to the request context
            query = query.Where(r =>
                !r.Scopes.Any() ||  // no scope = global
                r.Scopes.Any(s => s.ScopeType == ScopeType.Global) ||
                (request.ProductCode != null && r.Scopes.Any(s =>
                    s.ScopeType == ScopeType.Product && s.ScopeValue == request.ProductCode && !s.IsExcluded)) ||
                (request.BranchCode != null && r.Scopes.Any(s =>
                    s.ScopeType == ScopeType.Branch && s.ScopeValue == request.BranchCode && !s.IsExcluded)) ||
                (request.StageCode != null && r.Scopes.Any(s =>
                    s.ScopeType == ScopeType.Stage && s.ScopeValue == request.StageCode && !s.IsExcluded))
            );
        }

        return await query
            .OrderBy(r => r.Priority)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task InvalidateCacheAsync(Guid tenantId, CancellationToken ct = default)
    {
        // Invalidate all rule cache entries for this tenant
        // In production, use a tag-based cache invalidation strategy
        var pattern = $"bre:rules:{tenantId}:*";
        _logger.LogInformation("Invalidating rule cache for tenant {TenantId}", tenantId);
        // Redis SCAN + DEL for pattern — implement via IConnectionMultiplexer if needed
    }

    private static string BuildCacheKey(RuleLoadRequest request)
    {
        var parts = new[]
        {
            "bre", "rules",
            request.TenantId.ToString(),
            request.ProductCode ?? "ALL",
            request.BranchCode ?? "ALL",
            request.StageCode ?? "ALL",
            request.RuleSetId?.ToString() ?? "ALL",
            string.Join(",", request.RuleTypes.OrderBy(t => t.ToString())),
            request.PublishedOnly ? "pub" : "all"
        };
        return string.Join(":", parts);
    }
}
