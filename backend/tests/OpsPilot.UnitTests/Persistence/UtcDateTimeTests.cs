using Microsoft.EntityFrameworkCore;
using OpsPilot.Domain.Notifications;
using OpsPilot.UnitTests.TestSupport;

namespace OpsPilot.UnitTests.Persistence;

public class UtcDateTimeTests
{
    [Fact]
    public async Task Dates_read_back_from_the_database_are_marked_utc()
    {
        using var database = new TestDatabase();
        var created = new DateTime(2026, 10, 1, 23, 10, 0, DateTimeKind.Utc);
        await using (var db = database.NewContext())
        {
            db.Notifications.Add(new Notification { UserId = Guid.NewGuid(), Title = "t", Body = "b", Source = "test", CreatedAtUtc = created });
            await db.SaveChangesAsync();
        }

        await using (var db = database.NewContext())
        {
            var stored = await db.Notifications.AsNoTracking().SingleAsync();
            Assert.Equal(DateTimeKind.Utc, stored.CreatedAtUtc.Kind);
            Assert.Equal(created, stored.CreatedAtUtc);
            Assert.Null(stored.ReadAtUtc);
            // Serialises with a "Z", so browsers convert it to local time correctly.
            Assert.EndsWith("Z\"", System.Text.Json.JsonSerializer.Serialize(stored.CreatedAtUtc));
        }
    }
}
