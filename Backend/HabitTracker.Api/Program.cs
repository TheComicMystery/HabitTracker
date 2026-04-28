using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using HabitTracker.Api.Config;
using HabitTracker.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<MongoDbSettings>(builder.Configuration.GetSection("MongoDbSettings"));
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));

builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<MongoDbSettings>>().Value);
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<JwtSettings>>().Value);

builder.Services.AddMemoryCache();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<HabitService>();
builder.Services.AddSingleton<HabitPredictionService>();

builder.Services.AddControllers();

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
        policyBuilder.WithOrigins("http://localhost:5173") 
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

app.UseCors("AllowReactApp"); 

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
