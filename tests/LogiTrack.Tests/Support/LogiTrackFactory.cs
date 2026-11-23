using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace LogiTrack.Tests.Support;

/// <summary>
/// Hosts the API in memory on its own SQLite file, in the "Testing" environment, with a signing key generated
/// for this run only. Tests never read the developer's user-secrets.
/// </summary>
public class LogiTrackFactory : WebApplicationFactory<Program>
{
    public LogiTrackFactory(string? databasePath = null)
    {
        DatabasePath = databasePath ?? Path.Combine(Path.GetTempPath(), $"logitrack-test-{Guid.NewGuid():N}.db");
    }

    public const string ManagerEmail = "manager@logitrack.test";

    public string DatabasePath { get; }

    /// <summary>The signing key for this host; null leaves it unset to test the startup check.</summary>
    public string? JwtKey { get; init; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:LogiTrack", $"Data Source={DatabasePath}");
        builder.UseSetting("Jwt:Key", JwtKey ?? string.Empty);
        builder.UseSetting("Manager:Email", ManagerEmail);
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
