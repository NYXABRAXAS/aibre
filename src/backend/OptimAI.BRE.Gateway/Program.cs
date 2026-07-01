using System.Text;
using System.Threading.RateLimiting;
using Azure.AI.OpenAI;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OptimAI.BRE.AIEngine.Application;
using OptimAI.BRE.RuleEngine.Api;
using OptimAI.BRE.RuleEngine.Application;
using OptimAI.BRE.RuleEngine.Domain;
using OptimAI.BRE.RuleEngine.Infrastructure;
using Serilog;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// LOGGING - Serilog
// ============================================================
builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "OptimAI.BRE")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.File("logs/optim-ai-bre-.log", rollingInterval: RollingInterval.Day));

// ============================================================
// DATABASE
// ============================================================
builder.Services.AddDbContext<BREDbContext>(opts =>
    opts.UseNpgsql(
        builder.Configuration.GetConnectionString("PostgreSQL"),
        npg => npg.EnableRetryOnFailure(3)
    )
    .UseSnakeCaseNamingConvention()
);

// ============================================================
// REDIS CACHE
// ============================================================
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")!));

builder.Services.AddStackExchangeRedisCache(opts =>
    opts.Configuration = builder.Configuration.GetConnectionString("Redis"));

// ============================================================
// AUTHENTICATION & AUTHORIZATION
// ============================================================
var jwtSettings = builder.Configuration.GetSection("Jwt");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!)),
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
    .AddPolicy("RuleWrite", p => p.RequireClaim("permission", "RULE.CREATE", "RULE.EDIT"))
    .AddPolicy("RuleApprove", p => p.RequireClaim("permission", "RULE.APPROVE"))
    .AddPolicy("RulePublish", p => p.RequireClaim("permission", "RULE.PUBLISH"))
    .AddPolicy("SandboxAccess", p => p.RequireClaim("permission", "EXECUTION.SANDBOX"))
    .AddPolicy("AiGenerate", p => p.RequireClaim("permission", "AI.GENERATE"))
    .AddPolicy("AiAnalysis", p => p.RequireClaim("permission", "AI.ANALYSIS"))
    .AddPolicy("AdminOnly", p => p.RequireClaim("permission", "ADMIN.FULL"));

// ============================================================
// RATE LIMITING
// ============================================================
builder.Services.AddRateLimiter(opts =>
{
    opts.AddTokenBucketLimiter("bre-execution", cfg =>
    {
        cfg.TokenLimit = 1000;
        cfg.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        cfg.QueueLimit = 100;
        cfg.ReplenishmentPeriod = TimeSpan.FromMinutes(1);
        cfg.TokensPerPeriod = 1000;
        cfg.AutoReplenishment = true;
    });

    opts.AddFixedWindowLimiter("api-standard", cfg =>
    {
        cfg.PermitLimit = 500;
        cfg.Window = TimeSpan.FromMinutes(1);
    });

    opts.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.StatusCode = 429;
        await ctx.HttpContext.Response.WriteAsJsonAsync(new
        {
            error = "Rate limit exceeded",
            retryAfter = ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry) ? retry.TotalSeconds : 60
        }, ct);
    };
});

// ============================================================
// HEALTH CHECKS
// ============================================================
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("PostgreSQL")!, name: "postgresql")
    .AddRedis(builder.Configuration.GetConnectionString("Redis")!, name: "redis");

// ============================================================
// OPENAI / AZURE OPENAI
// ============================================================
builder.Services.Configure<AiOptions>(builder.Configuration.GetSection("AiOptions"));
builder.Services.AddSingleton<OpenAIClient>(_ =>
{
    var aiConfig = builder.Configuration.GetSection("AiOptions");
    return aiConfig.GetValue<bool>("UseAzureOpenAI")
        ? new OpenAIClient(new Uri(aiConfig["Endpoint"]!), new Azure.AzureKeyCredential(aiConfig["ApiKey"]!))
        : new OpenAIClient(aiConfig["ApiKey"]);
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
        Title = "OPTIM AI BRE Engine API",
        Version = "v1",
        Description = "Enterprise AI-Powered Business Rule Engine for Banks, NBFCs, and Lending Institutions",
        Contact = new OpenApiContact
        {
            Name = "OPTIM AI Support",
            Email = "support@optimai.in"
        }
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Enter JWT token"
    });

    c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Name = "X-API-Key",
        Description = "API Key authentication"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });

    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath)) c.IncludeXmlComments(xmlPath);
});

// CORS
builder.Services.AddCors(opts =>
    opts.AddPolicy("AllowFrontend", p => p
        .WithOrigins(builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? new[] { "*" })
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
        diag.Set("TenantId", ctx.Request.Headers["X-Tenant-ID"].FirstOrDefault() ?? "");
        diag.Set("RequestId", ctx.TraceIdentifier);
    };
});

if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "OPTIM AI BRE v1");
        c.RoutePrefix = "swagger";
        c.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);
    });
}

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseRateLimiter();

// Tenant resolution middleware
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseMiddleware<ApiKeyMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready");

// Auto-migrate on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BREDbContext>();
    await db.Database.MigrateAsync();
}

await app.RunAsync();
