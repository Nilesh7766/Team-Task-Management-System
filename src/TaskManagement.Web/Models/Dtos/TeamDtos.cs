using System.ComponentModel.DataAnnotations;

namespace TaskManagement.Web.Models.Dtos;

public class CreateTeamRequest
{
    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public int? ManagerId { get; set; }
}

public class UpdateTeamRequest : CreateTeamRequest { }

public class TeamDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? ManagerId { get; set; }
    public string? ManagerName { get; set; }
    public int MemberCount { get; set; }
    public int OpenTaskCount { get; set; }
    public List<UserDto> Members { get; set; } = new();
}

public class AddMemberRequest
{
    [Required]
    public int UserId { get; set; }
}
