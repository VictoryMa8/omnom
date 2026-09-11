using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Omnom.Api.Data;
using Omnom.Api.Services;

EnvironmentFile.Load(Directory.GetCurrentDirectory());
var builder = WebApplication.CreateBuilder(args);

var databaseUrl = builder.Configuration["DATABASE_URL"];
var publicDiary = builder.Configuration.GetValue<bool>("PUBLIC_DIARY");
var requirePasscode = builder.Configuration.GetValue<bool>("REQUIRE_PASSCODE");
if (requirePasscode && !publicDiary && (string.IsNullOrWhiteSpace(builder.Configuration["APP_PASSCODE"])
    || string.IsNullOrWhiteSpace(builder.Configuration["Jwt:Secret"])
    || string.IsNullOrWhiteSpace(databaseUrl)))
    throw new InvalidOperationException("Hosted PIN mode requires APP_PASSCODE, Jwt__Secret, and DATABASE_URL.");

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (!string.IsNullOrWhiteSpace(databaseUrl)) options.UseNpgsql(HostedDatabase.ConnectionString(databaseUrl));
    else
    {
        var dataDir = builder.Configuration["DATA_DIR"];
        if (!string.IsNullOrWhiteSpace(dataDir)) Directory.CreateDirectory(dataDir);
        options.UseSqlite($"Data Source={Path.Combine(dataDir ?? ".", "omnom.db")}");
    }
});

// HTTP Clients and Services
builder.Services.AddHttpClient<IOpenRouterService, OpenRouterService>(client =>
    client.Timeout = ParseLimits.AiWait + TimeSpan.FromSeconds(2));
builder.Services.AddHttpClient<IUsdaFoodService, UsdaFoodService>(client =>
    client.Timeout = ParseLimits.UsdaLookup + TimeSpan.FromSeconds(2));
builder.Services.AddScoped<IMealParserService, MealParserService>();

// CORS for frontend development
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllDev", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Controllers & JSON configuration
builder.Services.AddScoped<PasscodeAccess>();
builder.Services.AddScoped<RequestBudget>();
builder.Services.AddControllers(options =>
    {
        options.Filters.Add<PasscodeAccessFilter>();
        options.Filters.Add<RequestBudgetFilter>();
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Auto-migrate and seed staple foods on startup
using (var scope = app.Services.CreateScope())
{
    var directUrl = builder.Configuration["DATABASE_URL_UNPOOLED"];
    using var directDb = string.IsNullOrWhiteSpace(directUrl) ? null : new AppDbContext(
        new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(HostedDatabase.ConnectionString(directUrl)).Options);
    var db = directDb ?? scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (db.Database.IsNpgsql())
    {
        db.Database.OpenConnection();
        try
        {
            // Serialize schema creation and seeding during simultaneous cold starts.
            db.Database.ExecuteSqlRaw("SELECT pg_advisory_lock(72719431)");
            DbInitializer.Initialize(db);
        }
        finally
        {
            db.Database.ExecuteSqlRaw("SELECT pg_advisory_unlock(72719431)");
            db.Database.CloseConnection();
        }
    }
    else DbInitializer.Initialize(db);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (app.Environment.IsDevelopment()) app.UseCors("AllowAllDev");

// Serve frontend SPA from wwwroot
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRouting();

app.MapControllers();

// SPA fallback: any non-API route serves index.html
app.MapFallbackToFile("index.html");

app.Run();
