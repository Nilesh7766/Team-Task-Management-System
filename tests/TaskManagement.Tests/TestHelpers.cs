using Microsoft.EntityFrameworkCore;
using TaskManagement.Web.Data;
using TaskManagement.Web.Models.Dtos;
using TaskManagement.Web.Models.Entities;
using TaskManagement.Web.Services;

namespace TaskManagement.Tests;

/// <summary>Records what would have been sent, so tests can assert on notifications without SMTP.</summary>
public class FakeNotificationService : INotificationService
{
    public List<string> Sent { get; } = new();

    public Task NotifyTaskAssignedAsync(TaskItem task, string actorName)
    {
        Sent.Add($"assigned:{task.Id}:{task.AssignedToUserId}");
        return Task.CompletedTask;
    }

    public Task NotifyStatusChangedAsync(TaskItem task, TaskState oldStatus, string actorName)
    {
        Sent.Add($"status:{task.Id}:{oldStatus}->{task.Status}");
        return Task.CompletedTask;
    }

    public Task NotifyCommentAddedAsync(TaskItem task, string actorName, int actorId)
    {
        Sent.Add($"comment:{task.Id}");
        return Task.CompletedTask;
    }

    public Task NotifyTeamChangedAsync(User member, Team? team, string actorName)
    {
        Sent.Add($"team:{member.Id}:{team?.Id.ToString() ?? "none"}");
        return Task.CompletedTask;
    }

    public Task NotifyAccountCreatedAsync(User user)
    {
        Sent.Add($"account-created:{user.Id}");
        return Task.CompletedTask;
    }

    public Task NotifyPasswordResetRequestedAsync(User user, string resetLink)
    {
        Sent.Add($"password-reset-requested:{user.Id}:{resetLink}");
        return Task.CompletedTask;
    }

    public Task NotifyPasswordChangedAsync(User user)
    {
        Sent.Add($"password-changed:{user.Id}");
        return Task.CompletedTask;
    }

    public Task<List<NotificationDto>> GetForUserAsync(int userId, bool onlyUnread = false)
        => Task.FromResult(new List<NotificationDto>());

    public Task<int> UnreadCountAsync(int userId) => Task.FromResult(0);
    public Task MarkReadAsync(int notificationId, int userId) => Task.CompletedTask;
    public Task MarkAllReadAsync(int userId) => Task.CompletedTask;
}

public static class TestDb
{
    /// <summary>A fresh in-memory database seeded with one admin, one manager, two users and a team.</summary>
    public static AppDbContext Create(string? name = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString())
            .EnableSensitiveDataLogging()
            .Options;

        var db = new AppDbContext(options);

        var admin = new User { Id = 1, FullName = "Admin", Email = "admin@test.com", PasswordHash = "x", Role = UserRole.Admin };
        var manager = new User { Id = 2, FullName = "Manager", Email = "manager@test.com", PasswordHash = "x", Role = UserRole.Manager };
        var member = new User { Id = 3, FullName = "Member", Email = "member@test.com", PasswordHash = "x", Role = UserRole.User };
        var outsider = new User { Id = 4, FullName = "Outsider", Email = "outsider@test.com", PasswordHash = "x", Role = UserRole.User };

        db.Users.AddRange(admin, manager, member, outsider);
        db.Teams.Add(new Team { Id = 1, Name = "Alpha", ManagerId = 2 });
        db.SaveChanges();

        manager.TeamId = 1;
        member.TeamId = 1;
        db.SaveChanges();

        return db;
    }
}
