using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using OptimAI.BRE.RuleEngine.Api;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace OptimAI.BRE.RuleEngine.Infrastructure;

/// <summary>
/// Resolves tenant from JWT claims (X-Tenant-ID header fallback).
/// Every request must carry a valid tenant context.
/// </summary>
public sealed class TenantResolutionMiddleware : IMiddleware
{
    public async Task InvokeAsync(HttpContext ctx, RequestDelegate next)
    {
        // Skip for health checks and swagger
        if (ctx.Request.Path.StartsWithSegments("/health") ||
            ctx.Request.Path.StartsWithSegments("/swagger"))
        {
            await next(ctx);
            return;
        }

        var tenantId = ResolveTenantId(ctx);
        if (tenantId == null && !IsPublicEndpoint(ctx.Request.Path))
        {
            ctx.Response.StatusCode = 400;
            await ctx.Response.WriteAsJsonAsync(new { error = "Tenant context missing" });
            return;
        }

        if (tenantId != null)
            ctx.Items["TenantId"] = tenantId;

        await next(ctx);
    }

    private static Guid? ResolveTenantId(HttpContext ctx)
    {
        // 1. From JWT claim
        var tenantClaim = ctx.User.FindFirst("tenant_id")?.Value;
        if (tenantClaim != null && Guid.TryParse(tenantClaim, out var tid)) return tid;

        // 2. From header (API key auth)
        var header = ctx.Request.Headers["X-Tenant-ID"].FirstOrDefault();
        if (header != null && Guid.TryParse(header, out var hid)) return hid;

        return null;
    }

    private static bool IsPublicEndpoint(PathString path) =>
        path.StartsWithSegments("/api/v1/auth");
}

/// <summary>
/// API Key authentication middleware. Validates X-API-Key header.
/// Populates tenant context and user claims from API key.
/// </summary>
public sealed class ApiKeyMiddleware : IMiddleware
{
    private readonly BREDbContext _db;

    public ApiKeyMiddleware(BREDbContext db) => _db = db;

    public async Task InvokeAsync(HttpContext ctx, RequestDelegate next)
    {
        var apiKey = ctx.Request.Headers["X-API-Key"].FirstOrDefault();

        if (apiKey != null && !ctx.User.Identity?.IsAuthenticated == true)
        {
            var prefix = apiKey.Length >= 12 ? apiKey[..12] : apiKey;
            var keyHash = HashApiKey(apiKey);

            var key = await _db.ApiKeys
                .FirstOrDefaultAsync(k =>
                    k.ApiKeyPrefix == prefix &&
                    k.ApiKeyHash == keyHash &&
                    k.IsActive &&
                    (k.ExpiresAt == null || k.ExpiresAt > DateTime.UtcNow));

            if (key != null)
            {
                await _db.ApiKeys
                    .Where(k => k.Id == key.Id)
                    .ExecuteUpdateAsync(s => s.SetProperty(k => k.LastUsedAt, DateTime.UtcNow));

                var claims = new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, key.Id.ToString()),
                    new("tenant_id", key.TenantId.ToString()),
                    new("auth_type", "api_key"),
                    new("api_key_id", key.Id.ToString()),
                };

                foreach (var scope in key.Scopes)
                    claims.Add(new Claim("permission", scope));

                var identity = new ClaimsIdentity(claims, "ApiKey");
                ctx.User = new ClaimsPrincipal(identity);
                ctx.Items["TenantId"] = key.TenantId;
            }
        }

        await next(ctx);
    }

    private static string HashApiKey(string key)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(key);
        return Convert.ToHexString(sha.ComputeHash(bytes)).ToLower();
    }
}

/// <summary>
/// HttpContext-scoped tenant and user context accessor.
/// </summary>
public sealed class TenantContextAccessor : ITenantContextAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantContextAccessor(IHttpContextAccessor httpContextAccessor)
        => _httpContextAccessor = httpContextAccessor;

    public Guid TenantId
    {
        get
        {
            var ctx = _httpContextAccessor.HttpContext;
            if (ctx?.Items["TenantId"] is Guid id) return id;

            var claim = ctx?.User.FindFirst("tenant_id")?.Value;
            return claim != null && Guid.TryParse(claim, out var tid) ? tid : Guid.Empty;
        }
    }

    public Guid UserId
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return claim != null && Guid.TryParse(claim, out var id) ? id : Guid.Empty;
        }
    }

    public string? UserEmail =>
        _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Email)?.Value;
}
