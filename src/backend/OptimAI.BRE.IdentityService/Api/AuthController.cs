using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using OptimAI.BRE.RuleEngine.Infrastructure;
using OptimAI.BRE.Shared.Domain;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace OptimAI.BRE.IdentityService.Api;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly BREDbContext _db;
    private readonly JwtOptions _jwt;
    private readonly ILogger<AuthController> _logger;

    public AuthController(BREDbContext db, IOptions<JwtOptions> jwt, ILogger<AuthController> logger)
    {
        _db = db;
        _jwt = jwt.Value;
        _logger = logger;
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 401)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var user = await _db.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u =>
                u.Email == request.Email.ToLower() &&
                u.IsActive, ct);

        if (user == null || !VerifyPassword(request.Password, user.PasswordHash))
        {
            if (user != null)
            {
                user.FailedLoginCount++;
                if (user.FailedLoginCount >= 5)
                    user.LockedUntil = DateTime.UtcNow.AddMinutes(30);
                await _db.SaveChangesAsync(ct);
            }
            return Unauthorized(new { error = "Invalid email or password" });
        }

        if (user.LockedUntil.HasValue && user.LockedUntil > DateTime.UtcNow)
            return Unauthorized(new { error = "Account locked. Try again later." });

        user.FailedLoginCount = 0;
        user.LastLoginAt = DateTime.UtcNow;
        user.LastLoginIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _db.SaveChangesAsync(ct);

        var permissions = user.UserRoles
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.Permission.PermissionCode)
            .Distinct()
            .ToList();

        var accessToken = GenerateAccessToken(user, permissions);
        var refreshToken = GenerateRefreshToken();

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = HashToken(refreshToken),
            ExpiresAt = DateTime.UtcNow.AddDays(_jwt.RefreshTokenExpiryDays),
            IpAddress = user.LastLoginIp,
            UserAgent = Request.Headers.UserAgent.ToString()
        });
        await _db.SaveChangesAsync(ct);

        return Ok(new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = _jwt.AccessTokenExpiryMinutes * 60,
            TokenType = "Bearer",
            User = new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                FullName = user.FullName,
                TenantId = user.TenantId,
                Permissions = permissions,
                Roles = user.UserRoles.Select(ur => ur.Role.RoleCode).ToList()
            }
        });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request, CancellationToken ct)
    {
        var hash = HashToken(request.RefreshToken);
        var token = await _db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t =>
                t.TokenHash == hash &&
                !t.IsRevoked &&
                t.ExpiresAt > DateTime.UtcNow, ct);

        if (token == null)
            return Unauthorized(new { error = "Invalid or expired refresh token" });

        // Rotate refresh token
        token.IsRevoked = true;
        var newRefreshToken = GenerateRefreshToken();
        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = token.UserId,
            TokenHash = HashToken(newRefreshToken),
            ExpiresAt = DateTime.UtcNow.AddDays(_jwt.RefreshTokenExpiryDays)
        });

        await _db.SaveChangesAsync(ct);

        var permissions = await GetUserPermissionsAsync(token.UserId, ct);
        var accessToken = GenerateAccessToken(token.User!, permissions);

        return Ok(new { accessToken, refreshToken = newRefreshToken });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request, CancellationToken ct)
    {
        var hash = HashToken(request.RefreshToken);
        await _db.RefreshTokens
            .Where(t => t.TokenHash == hash)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsRevoked, true), ct);
        return Ok(new { message = "Logged out successfully" });
    }

    private string GenerateAccessToken(User user, List<string> permissions)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Name, user.FullName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("tenant_id", user.TenantId.ToString()),
        };

        foreach (var p in permissions)
            claims.Add(new Claim("permission", p));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwt.AccessTokenExpiryMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateRefreshToken()
    {
        var bytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    private static string HashToken(string token)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(token))).ToLower();
    }

    private static bool VerifyPassword(string password, string hash)
        => BCrypt.Net.BCrypt.Verify(password, hash);

    private async Task<List<string>> GetUserPermissionsAsync(Guid userId, CancellationToken ct)
    {
        return await _db.UserRoles
            .Where(ur => ur.UserId == userId)
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.Permission.PermissionCode)
            .Distinct()
            .ToListAsync(ct);
    }
}

// Models
public class JwtOptions
{
    public string SecretKey { get; set; } = default!;
    public string Issuer { get; set; } = default!;
    public string Audience { get; set; } = default!;
    public int AccessTokenExpiryMinutes { get; set; } = 60;
    public int RefreshTokenExpiryDays { get; set; } = 30;
}

public record LoginRequest
{
    public string Email { get; init; } = default!;
    public string Password { get; init; } = default!;
    public bool RememberMe { get; init; }
}

public record LoginResponse
{
    public string AccessToken { get; init; } = default!;
    public string RefreshToken { get; init; } = default!;
    public int ExpiresIn { get; init; }
    public string TokenType { get; init; } = "Bearer";
    public UserDto User { get; init; } = default!;
}

public record UserDto
{
    public Guid Id { get; init; }
    public string Email { get; init; } = default!;
    public string FullName { get; init; } = default!;
    public Guid TenantId { get; init; }
    public List<string> Permissions { get; init; } = new();
    public List<string> Roles { get; init; } = new();
}

public record RefreshRequest { public string RefreshToken { get; init; } = default!; }
public record LogoutRequest { public string RefreshToken { get; init; } = default!; }
