using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using HabitTracker.Api.Config;
using HabitTracker.Api.Models;
using HabitTracker.Api.Services;
using MongoDB.Driver;
using System.Security.Claims;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<MongoDbSettings>(builder.Configuration.GetSection("MongoDbSettings"));
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));

builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<MongoDbSettings>>().Value);
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<JwtSettings>>().Value);
builder.Services.AddSingleton(sp => new MongoClient(sp.GetRequiredService<MongoDbSettings>().ConnectionString));
builder.Services.AddSingleton(sp =>
{
    var settings = sp.GetRequiredService<MongoDbSettings>();
    return sp.GetRequiredService<MongoClient>().GetDatabase(settings.DatabaseName);
});

builder.Services.AddMemoryCache();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<HabitService>();
builder.Services.AddSingleton<HabitPredictionService>();
builder.Services.AddHostedService<DatabaseInitializerService>();
builder.Services.AddHostedService<PredictionWarmupService>();

builder.Services.AddControllers();
builder.Services.AddRateLimiter(options =>
{
    static string PartitionKey(HttpContext context)
    {
        return context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";
    }

    options.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    options.AddPolicy("mutations", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            PartitionKey(context),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 90,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    options.AddPolicy("prediction", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            PartitionKey(context),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>();
if (jwtSettings == null || string.IsNullOrEmpty(jwtSettings.Key))
{
    throw new ArgumentNullException(nameof(jwtSettings), "JWT Key not configured in appsettings.json");
}
var key = Encoding.ASCII.GetBytes(jwtSettings.Key);

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
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidIssuer = jwtSettings.Issuer,
        ValidateAudience = true,
        ValidAudience = jwtSettings.Audience,
        ClockSkew = TimeSpan.Zero 
    };
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policyBuilder =>
    {
        policyBuilder.WithOrigins(
                "http://localhost:5173",
                "http://127.0.0.1:5173",
                "http://localhost:5174",
                "http://127.0.0.1:5174") 
                     .AllowAnyHeader()
                     .AllowAnyMethod();
        
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { message = "Unexpected server error." });
        });
    });
}

app.Use(async (context, next) =>
{
    context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
    context.Response.Headers.TryAdd("X-Frame-Options", "DENY");
    context.Response.Headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
    if (!app.Environment.IsDevelopment())
    {
        context.Response.Headers.TryAdd("Content-Security-Policy", "default-src 'self'; frame-ancestors 'none'; base-uri 'self'");
    }

    await next();
});

app.UseCors("AllowReactApp"); 

app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.Use(async (context, next) =>
{
    await next();

    var method = context.Request.Method;
    if (method is not ("POST" or "PUT" or "PATCH" or "DELETE")) return;
    if (!context.Request.Path.StartsWithSegments("/api")) return;

    var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrWhiteSpace(userId)) return;

    var path = context.Request.Path.Value ?? string.Empty;
    var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
    var resourceType = segments.Length >= 2 ? segments[1] : "unknown";
    var resourceId = segments.FirstOrDefault(segment =>
        segment.Length == 24 && segment.All(Uri.IsHexDigit));

    try
    {
        var database = context.RequestServices.GetRequiredService<IMongoDatabase>();
        var auditLogs = database.GetCollection<AuditLog>("AuditLogs");
        await auditLogs.InsertOneAsync(new AuditLog
        {
            UserId = userId,
            Method = method,
            Endpoint = path,
            ResourceType = resourceType,
            ResourceId = resourceId,
            StatusCode = context.Response.StatusCode,
            TimestampUtc = DateTime.UtcNow
        });
    }
    catch
    {
    }
});

app.MapControllers();

app.Run();
