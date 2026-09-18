using Microsoft.EntityFrameworkCore;
using TaskManagement.Web.Models.Entities;
using TaskManagement.Web.Services;

namespace TaskManagement.Web.Data;

/// <summary>
/// Creates the database schema on startup and inserts the required
/// default Admin and Manager accounts if they do not already exist.
/// </summary>
public static class DbInitializer
{
    public static async Task InitializeAsync(
        AppDbContext db,
        ILogger logger)
    {
        // Create database and tables if they don't exist
        await db.Database.EnsureCreatedAsync();

        // Password hasher used by the application login system
        var hasher = new Pbkdf2PasswordHasher();

        // =========================================================
        // ADMIN
        // =========================================================

        var adminEmail = "admin@gamil.com";

        var admin = await db.Users
            .FirstOrDefaultAsync(u => u.Email == adminEmail);

        if (admin == null)
        {
            admin = new User
            {
                FullName = "Admin",
                Email = adminEmail,
                PasswordHash = hasher.Hash("Admin@123"),
                Role = UserRole.Admin,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            db.Users.Add(admin);

            logger.LogInformation(
                "Default Admin account created: {Email}",
                adminEmail);
        }
        else
        {
            // Make sure the seeded account remains an Admin
            admin.Role = UserRole.Admin;
            admin.IsActive = true;
        }

        // =========================================================
        // MANAGER
        // =========================================================

        var managerEmail = "manager@gamil.com";

        var manager = await db.Users
            .FirstOrDefaultAsync(u => u.Email == managerEmail);

        if (manager == null)
        {
            manager = new User
            {
                FullName = "Manager",
                Email = managerEmail,
                PasswordHash = hasher.Hash("Manager@123"),
                Role = UserRole.Manager,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            db.Users.Add(manager);

            logger.LogInformation(
                "Default Manager account created: {Email}",
                managerEmail);
        }
        else
        {
            // Make sure the seeded account remains a Manager
            manager.Role = UserRole.Manager;
            manager.IsActive = true;
        }

        await db.SaveChangesAsync();

        logger.LogInformation(
            "Database is ready. Default accounts verified.");
    }
}