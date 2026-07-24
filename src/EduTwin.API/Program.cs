using System;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using EduTwin.API.Health;
using EduTwin.BLL.Seeding;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Organization;
using EduTwin.BLL.DigitalTwin;
using EduTwin.BLL.KnowledgeGraph;
using EduTwin.BLL.CurriculumAndQuestions;
using EduTwin.API.Security;

var builder = WebApplication.CreateBuilder(args);

// --- Service Registration ---
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter(allowIntegerValues: false));
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = new System.Collections.Generic.Dictionary<string, string[]>();
            foreach (var entry in context.ModelState)
            {
                if (entry.Value.Errors.Count > 0)
                {
                    var key = entry.Key;
                    if (key.Length > 0 && char.IsUpper(key[0]))
                    {
                        key = char.ToLowerInvariant(key[0]) + key.Substring(1);
                    }
                    // Strip "$." prefix from JSON deserialization keys
                    if (key.StartsWith("$."))
                    {
                        key = key.Substring(2);
                        if (key.Length > 0 && char.IsUpper(key[0]))
                        {
                            key = char.ToLowerInvariant(key[0]) + key.Substring(1);
                        }
                    }
                    errors[key] = entry.Value.Errors
                        .Select(e => string.IsNullOrEmpty(e.ErrorMessage) ? "Giá trị không hợp lệ." : e.ErrorMessage)
                        .ToArray();
                }
            }

            var problemDetails = new ProblemDetails
            {
                Type = "https://edutwin.local/problems/validation",
                Title = "Dữ liệu không hợp lệ",
                Status = Microsoft.AspNetCore.Http.StatusCodes.Status400BadRequest,
                Detail = "Một hoặc nhiều trường không hợp lệ.",
                Instance = context.HttpContext.Request.Path
            };
            problemDetails.Extensions.Add("traceId", context.HttpContext.TraceIdentifier);
            problemDetails.Extensions.Add("errorCode", "VALIDATION_FAILED");
            problemDetails.Extensions.Add("errors", errors);

            return new BadRequestObjectResult(problemDetails);
        };
    });

// Bind JwtOptions and check configuration validity early
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>();
if (jwtOptions == null)
{
    throw new InvalidOperationException("JWT configuration section is missing.");
}
jwtOptions.Validate();

builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization(options =>
{
    var policies = EduTwin.BLL.IdentityAndTenancy.AuthorizationPolicies.GetPolicyRoles();
    foreach (var kvp in policies)
    {
        var roleNames = System.Linq.Enumerable.Select(kvp.Value, r => r.ToString()).ToArray();
        options.AddPolicy(kvp.Key, policy => policy.RequireClaim("role", roleNames));
    }
});
builder.Services.AddHealthChecks()
    .Add(new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckRegistration(
        "mysql",
        sp => new TcpHealthCheck(
            builder.Configuration["HealthChecks:MySqlHost"] ?? "localhost",
            int.Parse(builder.Configuration["HealthChecks:MySqlPort"] ?? "3306")
        ),
        failureStatus: null,
        tags: null
    ));

builder.Services.AddEduTwinBll(builder.Configuration);
builder.Services.AddIdentityAndTenancy();
builder.Services.AddOrganization();
builder.Services.AddDigitalTwin();
builder.Services.AddKnowledgeGraph();
builder.Services.AddCurriculumAndQuestions();

// We also need TimeProvider
builder.Services.AddSingleton(TimeProvider.System);

var app = builder.Build();

// --- Initialization ---
bool isDev = app.Environment.IsDevelopment();

bool seedEnabled = app.Configuration.GetValue<bool>("Seed:Enabled");

if (seedEnabled && !isDev)
{
    // R11: Strict startup guard
    throw new InvalidOperationException("CRITICAL SECURITY ERROR: Seeding is enabled in a non-Development environment! Shutting down to protect data.");
}

await app.Services.ApplyMigrationsAndSeedAsync(app.Configuration, isDev);

// --- Middleware Pipeline ---
app.UseAuthentication();
app.UseMiddleware<EduTwin.API.Middleware.TenantContextMiddleware>();
app.UseAuthorization();

app.MapControllers();

app.Run();
