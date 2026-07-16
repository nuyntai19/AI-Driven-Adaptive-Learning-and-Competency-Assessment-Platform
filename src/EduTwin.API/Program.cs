using System;
using EduTwin.API.Health;
using EduTwin.BLL.Seeding;

var builder = WebApplication.CreateBuilder(args);

// --- Service Registration ---
builder.Services.AddControllers();

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
app.MapControllers();

app.Run();
