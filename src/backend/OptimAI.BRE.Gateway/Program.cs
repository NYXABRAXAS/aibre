using System.Text;
using System.Threading.RateLimiting;
using Azure.AI.OpenAI;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OptimAI.BRE.AIEngine.Application;
using OptimAI.BRE.IdentityService.Api;
using OptimAI.BRE.RuleEngine.Api;
using OptimAI.BRE.RuleEngine.Application;
using OptimAI.BRE.RuleEngine.Domain;
using OptimAI.BRE.RuleEngine.Infrastructure;
using Serilog;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// CONNECTION STRING HELPERS
// Supports standard appsettings format AND Render's URL format.
// Render provides:
//   DATABASE_URL = postgres://user:pass@host:port/dbname
//   REDIS_URL    = redis://:pass@host:port
//                  rediss://:pass@host:port  (with TLS)
// ============================================================
static string GetPostgresConnectionString(IConfiguration config)
{
    var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
    if (!string.IsNullOrWhiteSpace(databaseUrl))
    {
        try
        {
            var uri = new Uri(databaseUrl);
            var parts = uri.UserInfo.Split(':', 2);
            var user = Uri.UnescapeDataString(parts[0]);
            var pass = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : "";
            var db   = uri.AbsolutePath.TrimStart('/');
            var port = uri.Port > 0 ? uri.Port : 5432;
            return $"Host={uri.Host};Port={port};Database={db};Username={user};Password={pass};" +
                   "SSL Mode=Require;Trust Server Certificate=true;" +
                   "Pooling=true;Minimum Pool Size=1;Maximum Pool Size=20;Connection Lifetime=300;Keepalive=60";
        }
        catch
        {
            // Fall through to appsettings
        }
    }
    return config.GetConnectionString("PostgreSQL")
        ?? throw new InvalidOperationException("No PostgreSQL connection string. Set DATABASE_URL or ConnectionStrings:PostgreSQL.");
}

static string GetRedisConnectionString(IConfiguration config)
{
    var redisUrl = Environment.GetEnvironmentVariable("REDIS_URL");
    if (!string.IsNullOrWhiteSpace(redisUrl))
    {
        try
        {
            var uri      = new Uri(redisUrl);
            var password = Uri.UnescapeDataString(uri.UserInfo.TrimStart(':'));
            var host     = uri.Host;
            var port     = uri.Port > 0 ? uri.Port : 6379;
            var ssl      = uri.Scheme == "rediss";
            var sb       = new StringBuilder($"{host}:{port},abortConnect=false,connectRetry=3,connectTimeout=5000,syncTimeout=5000");
            if (!string.IsNullOrEmpty(password)) sb.Append($",password={password}");
            if (ssl) sb.Append(",ssl=true,sslProtocols=tls12");
            return sb.ToString();
        }
        catch
        {
            // Fall through to appsettings
        }
    }
    return config.GetConnectionString("Redis")
        ?? throw new InvalidOperationException("No Redis connection string. Set REDIS_URL or ConnectionStrings:Redis.");
}

// ============================================================
// LOGGING — Serilog
// ============================================================
builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "OptimAI.BRE")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/optim-ai-bre-.log", rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30, fileSizeLimitBytes: 100 * 1024 * 1024));

// ============================================================
// DATABASE — PostgreSQL via Npgsql
// ============================================================
var pgConnectionString = GetPostgresConnectionString(builder.Configuration);

builder.Services.AddDbContext<BREDbContext>(opts =>
    opts.UseNpgsql(pgConnectionString,
        npg =>
        {
            npg.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
            npg.CommandTimeout(60);
        })
    .UseSnakeCaseNamingConvention());

// ============================================================
// REDIS CACHE
// ============================================================
var redisConnectionString = GetRedisConnectionString(builder.Configuration);

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var opts = ConfigurationOptions.Parse(redisConnectionString);
    opts.AbortOnConnectFail = false;
    return ConnectionMultiplexer.Connect(opts);
});

builder.Services.AddStackExchangeRedisCache(opts =>
    opts.Configuration = redisConnectionString);

// ============================================================
// AUTHENTICATION & AUTHORIZATION
// ============================================================
var jwtSection = builder.Configuration.GetSection("Jwt");
builder.Services.Configure<JwtOptions>(jwtSection);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = jwtSection["Issuer"],
            ValidAudience            = jwtSection["Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSection["SecretKey"]
                    ?? throw new InvalidOperationException("Jwt:SecretKey is required."))),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
        opts.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = ctx =>
            {
                Log.Warning("JWT auth failed: {Error}", ctx.Exception.Message);
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("RuleWrite",     p => p.RequireClaim("permission", "RULE.CREATE", "RULE.EDIT"))
    .AddPolicy("RuleApprove",   p => p.RequireClaim("permission", "RULE.APPROVE"))
    .AddPolicy("RulePublish",   p => p.RequireClaim("permission", "RULE.PUBLISH"))
    .AddPolicy("SandboxAccess", p => p.RequireClaim("permission", "EXECUTION.SANDBOX"))
    .AddPolicy("AiGenerate",    p => p.RequireClaim("permission", "AI.GENERATE"))
    .AddPolicy("AiAnalysis",    p => p.RequireClaim("permission", "AI.ANALYSIS"))
    .AddPolicy("AdminOnly",     p => p.RequireClaim("permission", "ADMIN.FULL"));

// ============================================================
// RATE LIMITING
// ============================================================
builder.Services.AddRateLimiter(opts =>
{
    opts.AddTokenBucketLimiter("bre-execution", cfg =>
    {
        cfg.TokenLimit             = 1000;
        cfg.QueueProcessingOrder   = QueueProcessingOrder.OldestFirst;
        cfg.QueueLimit             = 100;
        cfg.ReplenishmentPeriod    = TimeSpan.FromMinutes(1);
        cfg.TokensPerPeriod        = 1000;
        cfg.AutoReplenishment      = true;
    });
    opts.AddFixedWindowLimiter("api-standard", cfg =>
    {
        cfg.PermitLimit = 500;
        cfg.Window      = TimeSpan.FromMinutes(1);
    });
    opts.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.StatusCode = 429;
        await ctx.HttpContext.Response.WriteAsJsonAsync(new
        {
            error      = "Rate limit exceeded",
            retryAfter = ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry) ? retry.TotalSeconds : 60
        }, ct);
    };
});

// ============================================================
// HEALTH CHECKS
// ============================================================
builder.Services.AddHealthChecks()
    .AddNpgSql(pgConnectionString, name: "postgresql")
    .AddRedis(redisConnectionString, name: "redis");

// ============================================================
// AI SERVICE (OpenAI / Azure OpenAI)
// Gracefully degrades when no key is configured.
// ============================================================
builder.Services.Configure<AiOptions>(builder.Configuration.GetSection("AiOptions"));
builder.Services.AddSingleton<OpenAIClient>(_ =>
{
    var ai         = builder.Configuration.GetSection("AiOptions");
    var useAzure   = ai.GetValue<bool>("UseAzureOpenAI");
    var endpoint   = ai["Endpoint"] ?? "";
    var apiKey     = ai["ApiKey"] ?? "";

    if (useAzure && !string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(apiKey))
        return new OpenAIClient(new Uri(endpoint), new Azure.AzureKeyCredential(apiKey));

    // Non-Azure OpenAI or AI disabled — use placeholder key (AI features return fallback gracefully)
    return new OpenAIClient(string.IsNullOrWhiteSpace(apiKey) ? "placeholder-ai-disabled" : apiKey);
});

// ============================================================
// BRE SERVICES
// ============================================================
builder.Services.AddScoped<IRuleExecutionService, RuleExecutionService>();
builder.Services.AddScoped<IConditionEvaluator, ConditionEvaluator>();
builder.Services.AddScoped<IActionExecutor, ActionExecutor>();
builder.Services.AddScoped<IRiskScoringEngine, RiskScoringEngine>();
builder.Services.AddScoped<IDynamicFieldResolver, DynamicFieldResolver>();
builder.Services.AddScoped<IRuleLoader, CachedRuleLoader>();
builder.Services.AddScoped<IAiCreditAnalystService, AiCreditAnalystService>();
builder.Services.AddScoped<ITenantContextAccessor, TenantContextAccessor>();

// Infrastructure repositories
builder.Services.AddScoped<IDeviationTypeRepository, DeviationTypeRepository>();
builder.Services.AddScoped<IRiskWeightRepository, RiskWeightRepository>();
builder.Services.AddScoped<IExecutionRequestRepository, ExecutionRequestRepository>();
builder.Services.AddScoped<IExecutionResultRepository, ExecutionResultRepository>();
builder.Services.AddScoped<IDecisionReportService, DecisionReportService>();
builder.Services.AddScoped<ISandboxService, SandboxService>();
builder.Services.AddScoped<IAiPromptRepository, AiPromptRepository>();
builder.Services.AddScoped<IAiRuleGeneratorRepository, AiRuleGeneratorRepository>();
builder.Services.AddScoped<IFieldCatalogRepository, FieldCatalogRepository>();
builder.Services.AddScoped<IRuleRepository, RuleRepository>();
builder.Services.AddScoped<IRuleVersionRepository, RuleVersionRepository>();
builder.Services.AddScoped<IRuleApprovalService, RuleApprovalService>();
builder.Services.AddScoped<IAuditService, AuditService>();

// ============================================================
// API & SWAGGER
// Swagger enabled in all environments (accessible at /swagger)
// ============================================================
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        opts.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title       = "OPTIM AI BRE Engine API",
        Version     = "v1",
        Description = "Enterprise AI-Powered Business Rule Engine for Banks, NBFCs, and Lending Institutions",
        Contact     = new OpenApiContact { Name = "OPTIM AI Support", Email = "support@optimai.in" }
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type        = SecuritySchemeType.Http,
        Scheme      = "bearer",
        BearerFormat = "JWT",
        Description = "Enter JWT token"
    });
    c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Type        = SecuritySchemeType.ApiKey,
        In          = ParameterLocation.Header,
        Name        = "X-API-Key",
        Description = "API Key authentication"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
    var xmlPath = Path.Combine(AppContext.BaseDirectory,
        $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml");
    if (File.Exists(xmlPath)) c.IncludeXmlComments(xmlPath);
});

// ============================================================
// CORS
// AllowedOrigins comes from config or env vars.
// In Render, set AllowedOrigins__0 = https://optim-ai-bre-ui.onrender.com
// ============================================================
builder.Services.AddCors(opts =>
    opts.AddPolicy("AllowFrontend", p => p
        .WithOrigins(
            builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
            ?? new[] { "*" })
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials()));

// ============================================================
// BUILD APP
// ============================================================
var app = builder.Build();

app.UseSerilogRequestLogging(opts =>
{
    opts.EnrichDiagnosticContext = (diag, ctx) =>
    {
        diag.Set("TenantId",  ctx.Request.Headers["X-Tenant-ID"].FirstOrDefault() ?? "");
        diag.Set("RequestId", ctx.TraceIdentifier);
    };
});

// Swagger available in all environments
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "OPTIM AI BRE v1");
    c.RoutePrefix = "swagger";
    c.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);
});

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseRateLimiter();

app.UseMiddleware<TenantResolutionMiddleware>();
app.UseMiddleware<ApiKeyMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready");

// ============================================================
// DATABASE INITIALIZATION
// EnsureCreatedAsync creates all tables from the EF model.
// DatabaseSeeder inserts initial data if the DB is empty.
// This replaces MigrateAsync() since no EF migration files exist.
// ============================================================
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BREDbContext>();

    Log.Information("Checking database connection...");
    await db.Database.CanConnectAsync();

    Log.Information("Ensuring database schema...");

    // Install PostgreSQL extensions (required for uuid-ossp, pgcrypto)
    try
    {
        await db.Database.ExecuteSqlRawAsync("CREATE EXTENSION IF NOT EXISTS \"uuid-ossp\";");
        await db.Database.ExecuteSqlRawAsync("CREATE EXTENSION IF NOT EXISTS pgcrypto;");
        await db.Database.ExecuteSqlRawAsync("CREATE EXTENSION IF NOT EXISTS pg_trgm;");
    }
    catch (Exception ex)
    {
        Log.Warning("Extension creation skipped (may require superuser): {Message}", ex.Message);
    }

    // Create all tables from EF model (idempotent — only creates if they don't exist)
    await db.Database.EnsureCreatedAsync();

    Log.Information("Seeding initial data...");
    await DatabaseSeeder.SeedAsync(db);

    Log.Information("Database ready.");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Database initialization failed. Application will not start.");
    throw;
}

Log.Information("OPTIM AI BRE Engine starting on {Urls}", builder.Configuration["ASPNETCORE_URLS"] ?? "http://+:8080");
await app.RunAsync();
