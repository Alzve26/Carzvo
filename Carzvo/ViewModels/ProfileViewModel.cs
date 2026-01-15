using System.ComponentModel.DataAnnotations;

namespace Carzvo.Models.ViewModels
{
    public class ProfileViewModel
    {
        [Display(Name = "Полное имя")]
        [StringLength(100, ErrorMessage = "Максимальная длина 100 символов")]
        public string? FullName { get; set; }

        [EmailAddress(ErrorMessage = "Некорректный формат email")]
        [Display(Name = "Email")]
        public string? Email { get; set; }

        [Phone(ErrorMessage = "Некорректный формат телефона")]
        [Display(Name = "Телефон")]
        public string? PhoneNumber { get; set; }

        [Display(Name = "Адрес")]
        public string? Address { get; set; }

        [Display(Name = "Название компании")]
        public string? CompanyName { get; set; }
    }
}