using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SeniorConnect.Api.Endpoints;
using SeniorConnect.Infrastructure;
using SeniorConnect.Modules.Identity.Application;
using SeniorConnect.Modules.Identity.Infrastructure;
using SeniorConnect.Modules.Profiles.Application;
using SeniorConnect.Modules.Profiles.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// DbContext configuration
builder.Services.AddScoped<ITenantContext, DefaultTenantContext>();
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Database=SeniorConnect_dev;Username=postgres;Password=postgres";

builder.Services.AddDbContext<SeniorConnectDbContext>(options =>
{
    options.UseNpgsql(connectionString);
});

builder.Services.AddScoped<IIdentityDbContext>(sp => sp.GetRequiredService<SeniorConnectDbContext>());
builder.Services.AddScoped<IProfilesDbContext>(sp => sp.GetRequiredService<SeniorConnectDbContext>());

// Add Module services
builder.Services.AddIdentityModule();
builder.Services.AddProfilesModule();

// Configure JWT Authentication
var jwtSecret = builder.Configuration["Jwt:SecretKey"] ?? "SeniorConnect_jwt_super_secret_signing_key_2026_default_secure_key_123456";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "SeniorConnect.Api";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "SeniorConnect.Client";

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
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

// Map Endpoints
app.MapAuthEndpoints();
app.MapProfileEndpoints();
app.MapReferenceEndpoints();

app.Run();
