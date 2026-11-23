using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace LogiTrack.Tests.Support;

/// <summary>Hosts the API in memory on its own SQLite file, in the "Testing" environment.</summary>
public class LogiTrackFactory : WebApplicationFactory<Program>
{
    public LogiTrackFactory(string? databasePath = null)
    {
        DatabasePath = databasePath ?? Path.Combine(Path.GetTempPath(), $"logitrack-test-{Guid.NewGuid():N}.db");
    }

    public string DatabasePath { get; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:LogiTrack", $"Data Source={DatabasePath}");
    }

    /// <summary>Deletes the database file and its journal files.</summary>
    public void DeleteDatabase()
    {
        SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { "", "-shm", "-wal" })
        {
            File.Delete(DatabasePath + suffix);
        }
    }
}
