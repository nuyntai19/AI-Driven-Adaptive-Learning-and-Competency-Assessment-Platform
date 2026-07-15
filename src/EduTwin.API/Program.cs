var builder = WebApplication.CreateBuilder(args);

// --- Service Registration ---
builder.Services.AddControllers();

var app = builder.Build();

// --- Middleware Pipeline ---
app.MapControllers();

app.Run();
