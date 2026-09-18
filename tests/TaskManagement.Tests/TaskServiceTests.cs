using Microsoft.EntityFrameworkCore;
using TaskManagement.Web.Helpers;
using TaskManagement.Web.Models.Dtos;
using TaskManagement.Web.Models.Entities;
using TaskManagement.Web.Services;
using Xunit;

namespace TaskManagement.Tests;

public class TaskServiceTests
{
    private static (TaskService service, FakeNotificationService notes, Microsoft.EntityFrameworkCore.DbContext db) Build()
    {
        var db = TestDb.Create();
        var notes = new FakeNotificationService();
        return (new TaskService(db, notes), notes, db);
    }

    [Fact]
    public async Task Manager_can_create_a_task_for_a_member_of_their_team()
    {
        var (service, notes, _) = Build();

        var created = await service.CreateAsync(new CreateTaskRequest
        {
            Title = "Write the API tests",
            AssignedToUserId = 3,
            TeamId = 1,
            Priority = TaskPriority.High
        }, actorId: 2, actorRole: UserRole.Manager);

        Assert.Equal("Write the API tests", created.Title);
        Assert.Equal("ToDo", created.Status);
        Assert.Contains(notes.Sent, s => s.StartsWith("assigned:"));
    }

    [Fact]
    public async Task Manager_cannot_assign_a_task_to_someone_outside_their_team()
    {
        var (service, _, _) = Build();

        await Assert.ThrowsAsync<ForbiddenException>(() => service.CreateAsync(new CreateTaskRequest
        {
            Title = "Not allowed",
            AssignedToUserId = 4
        }, actorId: 2, actorRole: UserRole.Manager));
    }

    [Fact]
    public async Task Plain_users_cannot_create_tasks()
    {
        var (service, _, _) = Build();

        await Assert.ThrowsAsync<ForbiddenException>(() => service.CreateAsync(
            new CreateTaskRequest { Title = "Nope" }, actorId: 3, actorRole: UserRole.User));
    }

    [Fact]
    public async Task A_user_only_sees_the_tasks_assigned_to_them()
    {
        var (service, _, _) = Build();

        await service.CreateAsync(new CreateTaskRequest { Title = "Mine", AssignedToUserId = 3, TeamId = 1 }, 2, UserRole.Manager);
        await service.CreateAsync(new CreateTaskRequest { Title = "Someone else's", AssignedToUserId = 2, TeamId = 1 }, 2, UserRole.Manager);

        var mine = await service.GetTasksAsync(3, UserRole.User, new TaskFilter());
        var everything = await service.GetTasksAsync(1, UserRole.Admin, new TaskFilter());

        Assert.Single(mine);
        Assert.Equal("Mine", mine[0].Title);
        Assert.Equal(2, everything.Count);
    }

    [Fact]
    public async Task Assignee_can_move_their_own_task_and_a_notification_goes_out()
    {
        var (service, notes, _) = Build();
        var task = await service.CreateAsync(new CreateTaskRequest { Title = "Move me", AssignedToUserId = 3, TeamId = 1 }, 2, UserRole.Manager);

        var updated = await service.UpdateStatusAsync(task.Id, TaskState.InProgress, actorId: 3, actorRole: UserRole.User);

        Assert.Equal("InProgress", updated.Status);
        Assert.Contains(notes.Sent, s => s.Contains("ToDo->InProgress"));
    }

    [Fact]
    public async Task A_user_cannot_touch_a_task_that_is_not_theirs()
    {
        var (service, _, _) = Build();
        var task = await service.CreateAsync(new CreateTaskRequest { Title = "Private", AssignedToUserId = 3, TeamId = 1 }, 2, UserRole.Manager);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.UpdateStatusAsync(task.Id, TaskState.Done, actorId: 4, actorRole: UserRole.User));
    }

    [Fact]
    public async Task Filters_narrow_the_list_by_status_and_priority()
    {
        var (service, _, _) = Build();
        var a = await service.CreateAsync(new CreateTaskRequest { Title = "High one", AssignedToUserId = 3, TeamId = 1, Priority = TaskPriority.High }, 2, UserRole.Manager);
        await service.CreateAsync(new CreateTaskRequest { Title = "Low one", AssignedToUserId = 3, TeamId = 1, Priority = TaskPriority.Low }, 2, UserRole.Manager);
        await service.UpdateStatusAsync(a.Id, TaskState.Done, 2, UserRole.Manager);

        var done = await service.GetTasksAsync(1, UserRole.Admin, new TaskFilter { Status = TaskState.Done });
        var low = await service.GetTasksAsync(1, UserRole.Admin, new TaskFilter { Priority = TaskPriority.Low });

        Assert.Single(done);
        Assert.Equal("High one", done[0].Title);
        Assert.Single(low);
        Assert.Equal("Low one", low[0].Title);
    }

    [Fact]
    public async Task Summary_counts_every_status_bucket()
    {
        var (service, _, _) = Build();
        var t1 = await service.CreateAsync(new CreateTaskRequest { Title = "One", AssignedToUserId = 3, TeamId = 1 }, 2, UserRole.Manager);
        await service.CreateAsync(new CreateTaskRequest { Title = "Two", AssignedToUserId = 3, TeamId = 1 }, 2, UserRole.Manager);
        await service.UpdateStatusAsync(t1.Id, TaskState.Done, 2, UserRole.Manager);

        var summary = await service.GetStatusSummaryAsync(1, UserRole.Admin);

        Assert.Equal(1, summary[TaskState.ToDo]);
        Assert.Equal(0, summary[TaskState.InProgress]);
        Assert.Equal(1, summary[TaskState.Done]);
    }

    [Fact]
    public async Task Comments_are_stored_against_the_task_and_the_author()
    {
        var (service, notes, _) = Build();
        var task = await service.CreateAsync(new CreateTaskRequest { Title = "Discuss", AssignedToUserId = 3, TeamId = 1 }, 2, UserRole.Manager);

        await service.AddCommentAsync(task.Id, "Started on this today.", actorId: 3, actorRole: UserRole.User);
        var comments = await service.GetCommentsAsync(task.Id, 2, UserRole.Manager);

        Assert.Single(comments);
        Assert.Equal("Member", comments[0].UserName);
        Assert.Contains(notes.Sent, s => s.StartsWith("comment:"));
    }

    [Fact]
    public async Task An_empty_comment_is_rejected()
    {
        var (service, _, _) = Build();
        var task = await service.CreateAsync(new CreateTaskRequest { Title = "Discuss", AssignedToUserId = 3, TeamId = 1 }, 2, UserRole.Manager);

        await Assert.ThrowsAsync<DomainException>(() => service.AddCommentAsync(task.Id, "   ", 3, UserRole.User));
    }
}
