using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using TaskManagement.Web.Models.Dtos;

namespace TaskManagement.Web.Models.ViewModels;

public class TeamFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Give the team a name.")]
    [MaxLength(120)]
    [Display(Name = "Team name")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    [Display(Name = "What this team does")]
    public string? Description { get; set; }

    [Display(Name = "Team lead")]
    public int? ManagerId { get; set; }

    public List<SelectListItem> Managers { get; set; } = new();
    public bool IsEdit => Id > 0;
}

public class TeamDetailsViewModel
{
    public TeamDto Team { get; set; } = new();
    public List<TaskDto> Tasks { get; set; } = new();
    public List<SelectListItem> AddableUsers { get; set; } = new();
    public bool CanManageMembers { get; set; }
}

public class UserListViewModel
{
    public List<UserDto> Users { get; set; } = new();
    public List<SelectListItem> Teams { get; set; } = new();
}
