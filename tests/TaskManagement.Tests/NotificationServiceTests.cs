using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TaskManagement.Web.Models.Entities;
using TaskManagement.Web.Services;
using Xunit;

namespace TaskManagement.Tests;

public class NotificationServiceTests
{
    private static IConfiguration Config() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Notifications:Mode"] = "Mock"
        }).Build();

    [Fact]
    public async Task Account_and_password_notifications_do_not_appear_in_the_in_app_list()
    {
        var db = TestDb.Create();
        var service = new NotificationService(db, Config(), NullLogger<NotificationService>.Instance);

        var user = await db.Users.FindAsync(3); // "Member" from TestDb

        await service.NotifyAccountCreatedAsync(user!);
        await service.NotifyPasswordResetRequestedAsync(user!, "https://example.com/reset");
        await service.NotifyPasswordChangedAsync(user!);
        await service.NotifyTeamChangedAsync(user!, null, "Admin");

        var items = await service.GetForUserAsync(user!.Id);

        // Only the team-change (task/work related) notification should show up.
        Assert.Single(items);
        Assert.Equal("Team updated", items[0].Title);
    }

    [Fact]
    public async Task Account_and_password_notifications_do_not_count_towards_unread_count()
    {
        var db = TestDb.Create();
        var service = new NotificationService(db, Config(), NullLogger<NotificationService>.Instance);

        var user = await db.Users.FindAsync(3);

        await service.NotifyAccountCreatedAsync(user!);
        await service.NotifyPasswordChangedAsync(user!);

        Assert.Equal(0, await service.UnreadCountAsync(user!.Id));

        await service.NotifyTeamChangedAsync(user!, null, "Admin");

        Assert.Equal(1, await service.UnreadCountAsync(user!.Id));
    }
}
