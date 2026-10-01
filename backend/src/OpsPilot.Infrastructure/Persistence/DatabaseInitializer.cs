using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace OpsPilot.Infrastructure.Persistence;

/// <summary>
/// Optional start-up database preparation for containerised environments (off by default for local development):
/// <list type="bullet">
/// <item><c>Database:MigrateOnStartup</c> applies pending migrations, retrying while SQL Server is still starting.</item>
/// <item><c>Database:EnsureAiReader</c> creates the read-only login named in <c>ConnectionStrings:AiReadOnly</c> and grants it SELECT on the ai schema only.</item>
/// </list>
/// </summary>
public partial class DatabaseInitializer(ApplicationDbContext db, IConfiguration configuration, ILogger<DatabaseInitializer> logger)
{
    public async Task InitializeAsync()
    {
        if (configuration.GetValue<bool>("Database:MigrateOnStartup"))
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    await db.Database.MigrateAsync();
                    logger.LogInformation("Database migrations applied.");
                    break;
                }
                catch (SqlException ex) when (attempt < 10)
                {
                    logger.LogWarning("Database not ready (attempt {Attempt}): {Message}", attempt, ex.Message);
                    await Task.Delay(TimeSpan.FromSeconds(3 * attempt));
                }
            }
        }

        if (configuration.GetValue<bool>("Database:EnsureAiReader"))
        {
            await EnsureAiReaderAsync();
        }
    }

    private async Task EnsureAiReaderAsync()
    {
        var connectionString = configuration.GetConnectionString("AiReadOnly");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            logger.LogWarning("ConnectionStrings:AiReadOnly is not set; skipping AI reader login setup.");
            return;
        }

        var builder = new SqlConnectionStringBuilder(connectionString);
        if (!LoginName().IsMatch(builder.UserID) || string.IsNullOrEmpty(builder.Password))
        {
            throw new InvalidOperationException("The AI read-only connection must use a SQL login with a simple name and a password.");
        }

        // CREATE LOGIN can't take parameters; the name is validated above and the password is quote-escaped.
        var login = builder.UserID;
        var password = builder.Password.Replace("'", "''");
        var sql = $"""
            IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = '{login}')
                CREATE LOGIN [{login}] WITH PASSWORD = '{password}', CHECK_POLICY = ON;
            ELSE
                ALTER LOGIN [{login}] WITH PASSWORD = '{password}';
            IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = '{login}')
                CREATE USER [{login}] FOR LOGIN [{login}];
            GRANT SELECT ON SCHEMA::ai TO [{login}];
            """;

#pragma warning disable EF1002 // Interpolated values are a validated identifier and an escaped literal.
        await db.Database.ExecuteSqlRawAsync(sql);
#pragma warning restore EF1002
        logger.LogInformation("AI read-only login {Login} is configured.", login);
    }

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_]{2,63}$")]
    private static partial Regex LoginName();
}
