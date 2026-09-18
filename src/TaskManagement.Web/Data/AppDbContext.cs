using Microsoft.EntityFrameworkCore;
using TaskManagement.Web.Models.Entities;

namespace TaskManagement.Web.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<User>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Role).HasConversion<int>();

            e.HasOne(u => u.Team)
             .WithMany(t => t.Members)
             .HasForeignKey(u => u.TeamId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<Team>(e =>
        {
            e.HasIndex(t => t.Name).IsUnique();

            e.HasOne(t => t.Manager)
             .WithMany(u => u.ManagedTeams)
             .HasForeignKey(t => t.ManagerId)
             .OnDelete(DeleteBehavior.NoAction);
        });

        b.Entity<TaskItem>(e =>
        {
            e.Property(t => t.Status).HasConversion<int>();
            e.Property(t => t.Priority).HasConversion<int>();
            e.HasIndex(t => t.Status);
            e.HasIndex(t => t.DueDate);

            e.HasOne(t => t.AssignedToUser)
             .WithMany(u => u.AssignedTasks)
             .HasForeignKey(t => t.AssignedToUserId)
             .OnDelete(DeleteBehavior.NoAction);

            e.HasOne(t => t.CreatedByUser)
             .WithMany(u => u.CreatedTasks)
             .HasForeignKey(t => t.CreatedByUserId)
             .OnDelete(DeleteBehavior.NoAction);

            e.HasOne(t => t.Team)
             .WithMany(tm => tm.Tasks)
             .HasForeignKey(t => t.TeamId)
             .OnDelete(DeleteBehavior.SetNull);

            e.Ignore(t => t.IsOverdue);
        });

        b.Entity<Comment>(e =>
        {
            e.HasOne(c => c.TaskItem)
             .WithMany(t => t.Comments)
             .HasForeignKey(c => c.TaskItemId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(c => c.User)
             .WithMany(u => u.Comments)
             .HasForeignKey(c => c.UserId)
             .OnDelete(DeleteBehavior.NoAction);
        });

        b.Entity<Notification>(e =>
        {
            e.Property(n => n.Type).HasConversion<int>();

            e.HasOne(n => n.User)
             .WithMany(u => u.Notifications)
             .HasForeignKey(n => n.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
