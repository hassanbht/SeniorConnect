using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using SeniorConnect.Api.Endpoints;
using SeniorConnect.Infrastructure;
using SeniorConnect.Api.Middleware;
using SeniorConnect.Modules.Identity.Application;
using SeniorConnect.Modules.Identity.Infrastructure;
using SeniorConnect.Modules.Profiles.Application;
using SeniorConnect.Modules.Profiles.Infrastructure;
using SeniorConnect.Modules.Organizations.Application;
using SeniorConnect.Modules.Organizations.Infrastructure;
using SeniorConnect.Modules.HelpRequests.Application;
using SeniorConnect.Modules.HelpRequests.Infrastructure;
using SeniorConnect.Modules.Matching.Application;
using SeniorConnect.Modules.Matching.Infrastructure;
using SeniorConnect.Modules.TrustSafety.Application;
using SeniorConnect.Modules.TrustSafety.Infrastructure;
using SeniorConnect.Modules.Reporting.Application;
using SeniorConnect.Modules.Reporting.Infrastructure;
using SeniorConnect.Modules.Community.Application;
using SeniorConnect.Modules.Community.Infrastructure;
using SeniorConnect.Modules.Family.Application;
using SeniorConnect.Modules.Family.Infrastructure;
using SeniorConnect.Modules.Notifications.Application;
using SeniorConnect.Modules.Notifications.Infrastructure;
using SeniorConnect.Modules.Geography.Application;
using SeniorConnect.Modules.Geography.Infrastructure;

// Container healthcheck: the chiseled image has no shell or curl, so the
// compose healthcheck runs `dotnet SeniorConnect.Api.dll --healthcheck`.
if (args.Contains("--healthcheck"))
{
    using var probe = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
    try
    {
        var probeResponse = await probe.GetAsync("http://localhost:8080/healthz/live");
        return probeResponse.IsSuccessStatusCode ? 0 : 1;
    }
    catch (HttpRequestException)
    {
        return 1;
    }
    catch (TaskCanceledException)
    {
        return 1;
    }
}

// Load environment variables from .env file
DotNetEnv.Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

// Database Connection Configuration from .env / Environment Variables
var dbHost = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";
var dbPort = Environment.GetEnvironmentVariable("DB_PORT") ?? "5432";
var dbName = Environment.GetEnvironmentVariable("DB_NAME") ?? "seniorconnect_db";
var dbUser = Environment.GetEnvironmentVariable("DB_USER") ?? "seniorconnect_admin";
var dbPass = Environment.GetEnvironmentVariable("DB_PASSWORD")
    ?? (builder.Environment.IsDevelopment() ? "SeniorConnect_SecurePass_2026!" : string.Empty);
var sslMode = Environment.GetEnvironmentVariable("DB_SSL_MODE") ?? "Prefer";

var envConnectionString = $"Host={dbHost};Port={dbPort};Database={dbName};Username={dbUser};Password={dbPass};SSL Mode={sslMode};";
var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? (Environment.GetEnvironmentVariable("DB_NAME") != null ? envConnectionString : builder.Configuration.GetConnectionString("DefaultConnection"))
    ?? envConnectionString;

// In-Memory Caching for high-throughput reads & static reference data
builder.Services.AddMemoryCache();

// DbContext configuration
builder.Services.AddScoped<DefaultTenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<DefaultTenantContext>());
builder.Services.AddDbContext<SeniorConnectDbContext>(options =>
{
    options.UseNpgsql(connectionString);
});
builder.Services.AddDbContext<SafeguardingDbContext>(options =>
{
    options.UseNpgsql(connectionString);
});

builder.Services.AddScoped<IIdentityDbContext>(sp => sp.GetRequiredService<SeniorConnectDbContext>());
builder.Services.AddScoped<IProfilesDbContext>(sp => sp.GetRequiredService<SeniorConnectDbContext>());
builder.Services.AddScoped<IOrganizationsDbContext>(sp => sp.GetRequiredService<SeniorConnectDbContext>());
builder.Services.AddScoped<IHelpRequestsDbContext>(sp => sp.GetRequiredService<SeniorConnectDbContext>());
builder.Services.AddScoped<ITrustSafetyDbContext>(sp => sp.GetRequiredService<SeniorConnectDbContext>());
builder.Services.AddScoped<ISafeguardingDbContext>(sp => sp.GetRequiredService<SafeguardingDbContext>());
builder.Services.AddScoped<IReportingDbContext>(sp => sp.GetRequiredService<SeniorConnectDbContext>());
builder.Services.AddScoped<ICommunityDbContext>(sp => sp.GetRequiredService<SeniorConnectDbContext>());
builder.Services.AddScoped<IFamilyDbContext>(sp => sp.GetRequiredService<SeniorConnectDbContext>());
builder.Services.AddScoped<INotificationsDbContext>(sp => sp.GetRequiredService<SeniorConnectDbContext>());
builder.Services.AddScoped<IGeographyDbContext>(sp => sp.GetRequiredService<SeniorConnectDbContext>());

// Add Module services
builder.Services.AddIdentityModule(builder.Environment.IsDevelopment());
builder.Services.AddProfilesModule();
builder.Services.Configure<SeniorConnect.Modules.HelpRequests.Infrastructure.VisitAddressOptions>(
    builder.Configuration.GetSection(SeniorConnect.Modules.HelpRequests.Infrastructure.VisitAddressOptions.SectionName));
builder.Services.AddOrganizationsModule();
builder.Services.AddHelpRequestsModule();
builder.Services.AddMatchingModule(builder.Configuration);
builder.Services.AddTrustSafetyModule();
builder.Services.AddReportingModule();
builder.Services.AddCommunityModule();
builder.Services.AddFamilyModule();
builder.Services.AddNotificationModule();
builder.Services.AddGeographyModule(builder.Configuration);

// P2-18 / P7-08: Data Maintenance Hosted Service
builder.Services.AddHostedService<SeniorConnect.Infrastructure.BackgroundJobs.DataMaintenanceHostedService>();

// P3-13 / P3-18: Help-request lifecycle sweep (tiered escalation, assignment reminders)
builder.Services.AddHostedService<SeniorConnect.Infrastructure.BackgroundJobs.HelpRequestLifecycleHostedService>();

// P2-16: Monthly silent-volunteer reminder sweep
builder.Services.AddHostedService<SeniorConnect.Infrastructure.BackgroundJobs.MonthlyVolunteerReminderHostedService>();

// P1-05 / P9-02: Production Health Checks & Probes
builder.Services.AddHealthChecks()
    .AddCheck<SeniorConnect.Api.Common.DatabaseHealthCheck>("database", tags: ["ready"]);

// Configure JWT Authentication
// One resolver for issuing and validating tokens; refuses the public dev key
// outside Development.
var jwtSettings = SeniorConnect.Modules.Identity.Infrastructure.JwtSettings.Resolve(builder.Configuration);
jwtSettings.EnsureSafeFor(builder.Environment.IsDevelopment());
var jwtSecret = jwtSettings.SecretKey;
var jwtIssuer = jwtSettings.Issuer;
var jwtAudience = jwtSettings.Audience;

// Deployment-integration phase: an incomplete email-provider selection must
// fail loudly at startup, never silently send nothing in production.
SeniorConnect.Modules.Identity.Infrastructure.EmailProviderOptions.Resolve(builder.Configuration)
    .EnsureValidSelection();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

var corsAllowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5000", "http://localhost:8080"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("FlutterWeb", policy =>
    {
        policy.WithOrigins(corsAllowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// P7-09: Rate limiting. Every bucket is partitioned per caller (user id, or
// client IP when anonymous) so one client can never exhaust another's quota.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.ContentType = "application/problem+json";
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            type = "https://seniorconnect.at/problems/rate-limited",
            title = "Too many requests. Please wait a moment and try again.",
            status = StatusCodes.Status429TooManyRequests,
            code = "RATE_LIMITED",
        }, ct);
    };

    // Anonymous auth endpoints (login, OTP, refresh): per client IP.
    options.AddPolicy("auth_policy", httpContext =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            RateLimitPartitionKey(httpContext),
            _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));

    // Address lookup calls a paid/limited external provider: per caller.
    options.AddPolicy("geocode_policy", httpContext =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            RateLimitPartitionKey(httpContext),
            _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));

    // Everything else: a generous per-caller ceiling against abuse.
    options.GlobalLimiter = System.Threading.RateLimiting.PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            RateLimitPartitionKey(httpContext),
            _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 300,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
});

// Configure OpenAPI specification
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();

// P1-05 / P9-02: Health Check Endpoints
app.MapHealthChecks("/healthz/live", new HealthCheckOptions
{
    Predicate = _ => false // Liveness just checks if app process is responding
});

app.MapHealthChecks("/healthz/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => true
});

// P7-10: baseline security headers on every response.
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "no-referrer";
    await next();
});

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// API docs are not exposed in Production.
if (!app.Environment.IsProduction())
{
app.MapOpenApi();

// Expose Interactive API Explorer (Scalar & Swagger endpoint)
app.MapScalarApiReference(options =>
{
    options
        .WithTitle("SeniorConnect API Interactive Documentation")
        .WithTheme(ScalarTheme.Purple)
        .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
});

// Redirect /swagger to /scalar/v1 for convenience
app.MapGet("/swagger", () => Results.Redirect("/scalar/v1"));
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseCors("FlutterWeb");
app.UseAuthentication();
// After authentication so authenticated callers are partitioned by user id.
app.UseRateLimiter();
app.UseAuthorization();
// After authentication: the idempotency cache is keyed per user.
app.UseMiddleware<IdempotencyMiddleware>();
app.UseMiddleware<TenantResolutionMiddleware>();

// Map Endpoints
app.MapAuthEndpoints();
app.MapProfileEndpoints();
app.MapReferenceEndpoints();
app.MapOrganizationEndpoints();
app.MapActivityEndpoints();
app.MapHelpRequestEndpoints();
app.MapMatchingEndpoints();
app.MapOnboardingEndpoints();
app.MapTrustSafetyEndpoints();
app.MapSafeguardingEndpoints();
app.MapVerificationEndpoints(); // Phase 4
app.MapCommunityEndpoints();
app.MapFamilyEndpoints();
app.MapNotificationEndpoints();
app.MapPrivacyEndpoints();
app.MapCoordinatorEndpoints();
app.MapFunderEndpoints();
app.MapReportingEndpoints();
app.MapGeographyEndpoints();
app.MapComplianceEndpoints(); // P7-15 / Legal Gate — /api/v1/admin/compliance

// P9-01: apply pending migrations, then seed reference data. The chiseled
// runtime image has no dotnet-ef, so the API migrates itself on startup
// (disable with DB_MIGRATE_ON_STARTUP=false when migrating out of band).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SeniorConnectDbContext>();
    var startupLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    try
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("DB_MIGRATE_ON_STARTUP"), "false", StringComparison.OrdinalIgnoreCase)
            && db.Database.IsRelational())
        {
            await db.Database.MigrateAsync();
        }

        await DataSeeder.SeedInitialDataAsync(db);
    }
    catch (Exception ex) when (app.Environment.IsDevelopment())
    {
        // Local runs without a database keep starting; everywhere else a
        // missing schema must fail loudly instead of reporting ready.
        startupLogger.LogWarning(ex, "Database migration/seeding skipped in Development.");
    }
}

app.Run();
return 0;

static string RateLimitPartitionKey(HttpContext httpContext) =>
    httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
    ?? httpContext.User.FindFirst("sub")?.Value
    ?? httpContext.Connection.RemoteIpAddress?.ToString()
    ?? "unknown";
