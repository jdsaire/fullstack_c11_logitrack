using LogiTrack.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("LogiTrack")
    ?? throw new InvalidOperationException("Connection string 'LogiTrack' is missing.");

builder.Services.AddDbContext<LogiTrackContext>(options => options.UseSqlite(connectionString));
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "LogiTrack API", Version = "v1" }));

var app = builder.Build();

// Local runs and tests create or update the SQLite schema at startup.
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<LogiTrackContext>();
    context.Database.Migrate();

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

app.UseAuthorization();

app.MapControllers();

app.Run();

// Exposes the entry point to the integration tests (WebApplicationFactory<Program>).
public partial class Program { }
