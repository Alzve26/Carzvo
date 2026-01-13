using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Carzvo.Models
{
    public class EditUserRolesViewModel
    {
        [Required]
        public string UserId { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;

        public string? UserFullName { get; set; }

        public List<string> CurrentRoles { get; set; } = new List<string>();

        public List<string> AllRoles { get; set; } = new List<string>();

        public List<string>? SelectedRoles { get; set; }
    }
}