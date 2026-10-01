using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OpsPilot.Application.Common.Interfaces;
using OpsPilot.Infrastructure.Persistence;
using OpsPilot.Infrastructure.Persistence.Interceptors;

namespace OpsPilot.UnitTests.TestSupport;

/// <summary>
/// A real relational database (SQLite in-memory) so unique indexes, FKs and cascades behave like SQL Server.
/// The connection stays open for the lifetime of the test; each <see cref="NewContext"/> shares it.
/// </summary>
public sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public FakeCurrentUser CurrentUser { get; } = new();

    public TestDatabase()
    {
        _connection.Open();
        using var context = NewContext();
        context.Database.EnsureCreated();
    }

    public ApplicationDbContext NewContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(new AuditableEntityInterceptor(CurrentUser))
            .Options);

    public void Dispose() => _connection.Dispose();
}

public class FakeCurrentUser : ICurrentUserService
{
    public Guid? UserId { get; set; } = Guid.NewGuid();
}
