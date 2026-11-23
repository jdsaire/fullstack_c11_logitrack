using LogiTrack.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("LogiTrack")
    ?? throw new InvalidOperationException("Connection string 'LogiTrack' is missing.");

builder.Services.AddDbContext<LogiTrackContext>(options => options.UseSqlite(connectionString));
builder.Services.AddControllers();

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

app.UseAuthorization();

app.MapControllers();

app.Run();

// Exposes the entry point to the integration tests (WebApplicationFactory<Program>).
public partial class Program { }
