using Microsoft.EntityFrameworkCore;
using TaskManagement.Web.Data;
using TaskManagement.Web.Helpers;
using TaskManagement.Web.Models.Dtos;
using TaskManagement.Web.Models.Entities;

namespace TaskManagement.Web.Services;

public interface ITeamService
{
    Task<List<TeamDto>> GetTeamsAsync(int actorId, UserRole actorRole);
    Task<TeamDto> GetTeamAsync(int id, int actorId, UserRole actorRole);
    Task<TeamDto> CreateAsync(CreateTeamRequest request, UserRole actorRole);
    Task<TeamDto> UpdateAsync(int id, UpdateTeamRequest request, int actorId, UserRole actorRole);
    Task DeleteAsync(int id, UserRole actorRole);
}

public class TeamService : ITeamService
{
    private readonly AppDbContext _db;

    public TeamService(AppDbContext db) => _db = db;

    public async Task<List<TeamDto>> GetTeamsAsync(int actorId, UserRole actorRole)
    {
        var query = _db.Teams
            .AsNoTracking()
            .Include(t => t.Manager)
            .Include(t => t.Members)
            .Include(t => t.Tasks)
            .AsQueryable();

        if (actorRole == UserRole.Manager)
        {
            var user = await _db.Users.AsNoTracking().FirstAsync(u => u.Id == actorId);
            query = query.Where(t => t.ManagerId == actorId || t.Id == user.TeamId);
        }
        else if (actorRole == UserRole.User)
        {
            var user = await _db.Users.AsNoTracking().FirstAsync(u => u.Id == actorId);
            query = query.Where(t => t.Id == user.TeamId);
        }

        var teams = await query.OrderBy(t => t.Name).ToListAsync();
        return teams.Select(ToDto).ToList();
    }

    public async Task<TeamDto> GetTeamAsync(int id, int actorId, UserRole actorRole)
    {
        var team = await _db.Teams
            .AsNoTracking()
            .Include(t => t.Manager)
            .Include(t => t.Members)
            .Include(t => t.Tasks)
            .FirstOrDefaultAsync(t => t.Id == id)
            ?? throw new NotFoundException($"Team {id} was not found.");

        if (actorRole != UserRole.Admin)
        {
            var user = await _db.Users.AsNoTracking().FirstAsync(u => u.Id == actorId);
            var allowed = team.ManagerId == actorId || user.TeamId == team.Id;
            if (!allowed) throw new ForbiddenException("You do not have access to this team.");
        }

        return ToDto(team);
    }

    public async Task<TeamDto> CreateAsync(CreateTeamRequest request, UserRole actorRole)
    {
        if (actorRole != UserRole.Admin)
            throw new ForbiddenException("Only administrators can create teams.");

        var name = request.Name.Trim();
        if (await _db.Teams.AnyAsync(t => t.Name == name))
            throw new DomainException("A team with that name already exists.");

        await ValidateManagerAsync(request.ManagerId);

        var team = new Team
        {
            Name = name,
            Description = request.Description?.Trim(),
            ManagerId = request.ManagerId
        };

        _db.Teams.Add(team);
        await _db.SaveChangesAsync();

        return await GetTeamAsync(team.Id, 0, UserRole.Admin);
    }

    public async Task<TeamDto> UpdateAsync(int id, UpdateTeamRequest request, int actorId, UserRole actorRole)
    {
        var team = await _db.Teams.FirstOrDefaultAsync(t => t.Id == id)
                   ?? throw new NotFoundException($"Team {id} was not found.");

        if (actorRole == UserRole.User || (actorRole == UserRole.Manager && team.ManagerId != actorId))
            throw new ForbiddenException("You can only edit teams you manage.");

        var name = request.Name.Trim();
        if (await _db.Teams.AnyAsync(t => t.Name == name && t.Id != id))
            throw new DomainException("A team with that name already exists.");

        team.Name = name;
        team.Description = request.Description?.Trim();

        // Only an administrator may hand a team to a different manager.
        if (actorRole == UserRole.Admin)
        {
            await ValidateManagerAsync(request.ManagerId);
            team.ManagerId = request.ManagerId;
        }

        await _db.SaveChangesAsync();
        return await GetTeamAsync(team.Id, actorId, actorRole);
    }

    public async Task DeleteAsync(int id, UserRole actorRole)
    {
        if (actorRole != UserRole.Admin)
            throw new ForbiddenException("Only administrators can delete teams.");

        var team = await _db.Teams.Include(t => t.Members).Include(t => t.Tasks)
                       .FirstOrDefaultAsync(t => t.Id == id)
                   ?? throw new NotFoundException($"Team {id} was not found.");

        foreach (var member in team.Members) member.TeamId = null;
        foreach (var task in team.Tasks) task.TeamId = null;

        _db.Teams.Remove(team);
        await _db.SaveChangesAsync();
    }

    private async Task ValidateManagerAsync(int? managerId)
    {
        if (!managerId.HasValue) return;

        var manager = await _db.Users.FirstOrDefaultAsync(u => u.Id == managerId.Value)
                      ?? throw new NotFoundException($"User {managerId} was not found.");

        if (manager.Role == UserRole.User)
            throw new DomainException("A team lead must have the Manager or Admin role.");
    }

    internal static TeamDto ToDto(Team t) => new()
    {
        Id = t.Id,
        Name = t.Name,
        Description = t.Description,
        ManagerId = t.ManagerId,
        ManagerName = t.Manager?.FullName,
        MemberCount = t.Members?.Count ?? 0,
        OpenTaskCount = t.Tasks?.Count(x => x.Status != TaskState.Done) ?? 0,
        Members = t.Members?.Select(m => new UserDto
        {
            Id = m.Id,
            FullName = m.FullName,
            Email = m.Email,
            Role = m.Role.ToString(),
            TeamId = m.TeamId,
            TeamName = t.Name,
            IsActive = m.IsActive
        }).OrderBy(m => m.FullName).ToList() ?? new List<UserDto>()
    };
}
