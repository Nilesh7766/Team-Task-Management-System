using Microsoft.EntityFrameworkCore;
using TaskManagement.Web.Data;
using TaskManagement.Web.Helpers;
using TaskManagement.Web.Models.Dtos;
using TaskManagement.Web.Models.Entities;

namespace TaskManagement.Web.Services;

public interface IUserService
{
    Task<List<UserDto>> GetAllAsync(UserRole? role = null);
    Task<UserDto?> GetAsync(int id);
    Task<User> GetEntityAsync(int id);
    /// <summary>Users the given actor is allowed to assign work to.</summary>
    Task<List<UserDto>> GetAssignableUsersAsync(int actorId, UserRole actorRole);
    Task ChangeRoleAsync(int userId, UserRole role);
    Task SetActiveAsync(int userId, bool isActive);
    Task AssignTeamAsync(int userId, int? teamId, int actorId, UserRole actorRole);
}

public class UserService : IUserService
{
    private readonly AppDbContext _db;
    private readonly INotificationService _notifications;

    public UserService(AppDbContext db, INotificationService notifications)
    {
        _db = db;
        _notifications = notifications;
    }

    public async Task<List<UserDto>> GetAllAsync(UserRole? role = null)
    {
        var query = _db.Users.AsNoTracking().Include(u => u.Team).AsQueryable();
        if (role.HasValue) query = query.Where(u => u.Role == role.Value);

        var users = await query.OrderBy(u => u.FullName).ToListAsync();
        return users.Select(ToDto).ToList();
    }

    public async Task<UserDto?> GetAsync(int id)
    {
        var user = await _db.Users.AsNoTracking().Include(u => u.Team).FirstOrDefaultAsync(u => u.Id == id);
        return user is null ? null : ToDto(user);
    }

    public async Task<User> GetEntityAsync(int id)
        => await _db.Users.Include(u => u.Team).FirstOrDefaultAsync(u => u.Id == id)
           ?? throw new NotFoundException($"User {id} was not found.");

    public async Task<List<UserDto>> GetAssignableUsersAsync(
    int actorId,
    UserRole actorRole)
    {
        var query = _db.Users
            .AsNoTracking()
            .Include(u => u.Team)
            .Where(u => u.IsActive);

        if (actorRole == UserRole.Manager)
        {
            var teamIds = await _db.Teams
                .Where(t => t.ManagerId == actorId)
                .Select(t => t.Id)
                .ToListAsync();

            
            query = query.Where(u =>
                u.Role == UserRole.User &&
                u.TeamId != null &&
                teamIds.Contains(u.TeamId.Value));
        }
        else if (actorRole == UserRole.User)
        {
            
            query = query.Where(u => u.Id == actorId);
        }

        var users = await query
            .OrderBy(u => u.FullName)
            .ToListAsync();

        return users.Select(ToDto).ToList();
    }

    public async Task ChangeRoleAsync(int userId, UserRole role)
    {
        var user = await GetEntityAsync(userId);

        if (user.Role == UserRole.Admin && role != UserRole.Admin)
        {
            var admins = await _db.Users.CountAsync(u => u.Role == UserRole.Admin);
            if (admins <= 1)
                throw new DomainException("The last administrator cannot be demoted.");
        }

        user.Role = role;
        await _db.SaveChangesAsync();
    }

    public async Task SetActiveAsync(int userId, bool isActive)
    {
        var user = await GetEntityAsync(userId);

        if (!isActive && user.Role == UserRole.Admin)
        {
            var admins = await _db.Users.CountAsync(u => u.Role == UserRole.Admin && u.IsActive);
            if (admins <= 1)
                throw new DomainException("The last active administrator cannot be deactivated.");
        }

        user.IsActive = isActive;
        await _db.SaveChangesAsync();
    }

    public async Task AssignTeamAsync(int userId, int? teamId, int actorId, UserRole actorRole)
    {
        var user = await GetEntityAsync(userId);
        Team? team = null;

        if (teamId.HasValue)
        {
            team = await _db.Teams.FirstOrDefaultAsync(t => t.Id == teamId.Value)
                   ?? throw new NotFoundException($"Team {teamId} was not found.");

            // A manager may only move people in and out of the teams they own.
            if (actorRole == UserRole.Manager && team.ManagerId != actorId)
                throw new ForbiddenException("You can only manage members of your own teams.");
        }
        else if (actorRole == UserRole.Manager)
        {
            if (user.TeamId is null)
                throw new DomainException("This user is not in a team.");

            var owns = await _db.Teams.AnyAsync(t => t.Id == user.TeamId && t.ManagerId == actorId);
            if (!owns)
                throw new ForbiddenException("You can only manage members of your own teams.");
        }

        user.TeamId = teamId;
        await _db.SaveChangesAsync();

        var actorName = await _db.Users.Where(u => u.Id == actorId).Select(u => u.FullName).FirstOrDefaultAsync() ?? "An administrator";
        await _notifications.NotifyTeamChangedAsync(user, team, actorName);
    }

    /// <summary>Entity to DTO mapping, applied after the query has been materialised.</summary>
    internal static UserDto ToDto(User u) => new()
    {
        Id = u.Id,
        FullName = u.FullName,
        Email = u.Email,
        Role = u.Role.ToString(),
        TeamId = u.TeamId,
        TeamName = u.Team != null ? u.Team.Name : null,
        IsActive = u.IsActive
    };
}
