using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using SeniorConnect.Api.Endpoints;
using SeniorConnect.Infrastructure;
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

// Load environment variables from .env file
DotNetEnv.Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

// Database Connection Configuration from .env / Environment Variables
var dbHost = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";
var dbPort = Environment.GetEnvironmentVariable("DB_PORT") ?? "5432";
var dbName = Environment.GetEnvironmentVariable("DB_NAME") ?? "seniorconnect_db";
var dbUser = Environment.GetEnvironmentVariable("DB_USER") ?? "seniorconnect_admin";
var dbPass = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "SeniorConnect_SecurePass_2026!";
var sslMode = Environment.GetEnvironmentVariable("DB_SSL_MODE") ?? "Prefer";

var envConnectionString = $"Host={dbHost};Port={dbPort};Database={dbName};Username={dbUser};Password={dbPass};SSL Mode={sslMode};";
var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? (Environment.GetEnvironmentVariable("DB_NAME") != null ? envConnectionString : builder.Configuration.GetConnectionString("DefaultConnection"))
    ?? envConnectionString;

// DbContext configuration
builder.Services.AddScoped<ITenantContext, DefaultTenantContext>();
builder.Services.AddDbContext<SeniorConnectDbContext>(options =>
{
    options.UseNpgsql(connectionString);
});

builder.Services.AddScoped<IIdentityDbContext>(sp => sp.GetRequiredService<SeniorConnectDbContext>());
builder.Services.AddScoped<IProfilesDbContext>(sp => sp.GetRequiredService<SeniorConnectDbContext>());
builder.Services.AddScoped<IOrganizationsDbContext>(sp => sp.GetRequiredService<SeniorConnectDbContext>());
builder.Services.AddScoped<IHelpRequestsDbContext>(sp => sp.GetRequiredService<SeniorConnectDbContext>());
builder.Services.AddScoped<ITrustSafetyDbContext>(sp => sp.GetRequiredService<SeniorConnectDbContext>());
builder.Services.AddScoped<IReportingDbContext>(sp => sp.GetRequiredService<SeniorConnectDbContext>());
builder.Services.AddScoped<ICommunityDbContext>(sp => sp.GetRequiredService<SeniorConnectDbContext>());
builder.Services.AddScoped<IFamilyDbContext>(sp => sp.GetRequiredService<SeniorConnectDbContext>());
builder.Services.AddScoped<INotificationsDbContext>(sp => sp.GetRequiredService<SeniorConnectDbContext>());

// Add Module services
builder.Services.AddIdentityModule();
builder.Services.AddProfilesModule();
builder.Services.AddOrganizationsModule();
builder.Services.AddHelpRequestsModule();
builder.Services.AddMatchingModule();
builder.Services.AddTrustSafetyModule();
builder.Services.AddReportingModule();
builder.Services.AddCommunityModule();
builder.Services.AddFamilyModule();
builder.Services.AddNotificationModule();

// Configure JWT Authentication
var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
    ?? Environment.GetEnvironmentVariable("JWT_SECRET")
    ?? builder.Configuration["Jwt:SecretKey"]
    ?? "SeniorConnect_jwt_super_secret_signing_key_2026_default_secure_key_123456";

var jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER")
    ?? builder.Configuration["Jwt:Issuer"]
    ?? "SeniorConnect.Api";

var jwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE")
    ?? builder.Configuration["Jwt:Audience"]
    ?? "SeniorConnect.Client";

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

// P7-09: Rate Limiting & Lockout across the general API
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("auth_policy", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("general_policy", opt =>
    {
        opt.PermitLimit = 100;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 10;
    });
});

// Configure OpenAPI specification
builder.Services.AddOpenApi();

var app = builder.Build();

// Expose OpenAPI document
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

app.UseHttpsRedirection();
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

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
app.MapCommunityEndpoints();
app.MapFamilyEndpoints();
app.MapNotificationEndpoints();
app.MapPrivacyEndpoints();
app.MapCoordinatorEndpoints();
app.MapFunderEndpoints();
app.MapReportingEndpoints();

app.Run();
