using System.Text;
using LogiTrack.Data;
using LogiTrack.Models;
using LogiTrack.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("LogiTrack")
    ?? throw new InvalidOperationException("Connection string 'LogiTrack' is missing.");

// Token settings: issuer, audience and expiry from appsettings; the signing key from user-secrets.
var jwt = builder.Configuration.GetSection(JwtSettings.Section).Get<JwtSettings>() ?? new JwtSettings();
if (Encoding.UTF8.GetByteCount(jwt.Key) < 32)
{
    throw new InvalidOperationException(
        "Jwt:Key is missing or shorter than 32 bytes. Set it with: dotnet user-secrets set \"Jwt:Key\" \"<value>\"");
}

builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.Section));
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<ManagerSeeder>();

builder.Services.AddDbContext<LogiTrackContext>(options => options.UseSqlite(connectionString));
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options => options.User.RequireUniqueEmail = true)
    .AddEntityFrameworkStores<LogiTrackContext>();

// Bearer tokens are the only way in: no cookie sign-in, no login redirects.
builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultForbidScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = "sub",
            RoleClaimType = "role"
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "LogiTrack API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Paste the token from POST /api/auth/login."
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

var app = builder.Build();

// Local runs and tests create or update the SQLite schema at startup.
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<LogiTrackContext>();
    context.Database.Migrate();
    await scope.ServiceProvider.GetRequiredService<ManagerSeeder>().SeedAsync();

    if (app.Environment.IsDevelopment())
    {
        DevSeeder.Seed(context);
    }
}

// Swagger UI for local review only.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Exposes the entry point to the integration tests (WebApplicationFactory<Program>).
public partial class Program { }
