using System.ComponentModel.DataAnnotations;

namespace Carzvo.Models.ViewModels
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Пожалуйста, введите полное имя")]
        [Display(Name = "Полное имя")]
        [StringLength(100, ErrorMessage = "Максимальная длина 100 символов")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Пожалуйста, введите email")]
        [EmailAddress(ErrorMessage = "Некорректный формат email")]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Пожалуйста, введите телефон")]
        [Phone(ErrorMessage = "Некорректный формат телефона")]
        [Display(Name = "Телефон")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Пожалуйста, введите пароль")]
        [StringLength(100, ErrorMessage = "Пароль должен содержать минимум {2} и максимум {1} символов.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Пароль")]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Подтверждение пароля")]
        [Compare("Password", ErrorMessage = "Пароли не совпадают")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Display(Name = "Адрес")]
        public string? Address { get; set; }

        [Display(Name = "Название компании")]
        public string? CompanyName { get; set; }
    }
}