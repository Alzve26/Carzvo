using Microsoft.AspNetCore.Identity;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Carzvo.Models
{
    public class ApplicationUser : IdentityUser
    {
        [PersonalData]
        [Display(Name = "Полное имя")]
        [StringLength(100, ErrorMessage = "Максимальная длина 100 символов")]
        public string? FullName { get; set; }

        [PersonalData]
        [Display(Name = "Адрес")]
        [StringLength(200, ErrorMessage = "Максимальная длина 200 символов")]
        public string? Address { get; set; }

        [PersonalData]
        [Display(Name = "Название компании")]
        [StringLength(100, ErrorMessage = "Максимальная длина 100 символов")]
        public string? CompanyName { get; set; }

        [PersonalData]
        [Display(Name = "Водительское удостоверение")]
        [StringLength(50, ErrorMessage = "Максимальная длина 50 символов")]
        public string? DriverLicense { get; set; }

        [Display(Name = "Статус водителя")]
        public bool IsDriver { get; set; } = false;

        [Display(Name = "Рейтинг")]
        [Range(0, 5, ErrorMessage = "Рейтинг должен быть от 0 до 5")]
        public double? Rating { get; set; } = 5.0;

        [Display(Name = "Статус")]
        public string Status { get; set; } = "Активен";

        [Display(Name = "Дата регистрации")]
        public DateTime RegistrationDate { get; set; } = DateTime.Now;


        // Навигационные свойства
        public virtual ICollection<Shipment>? CreatedShipments { get; set; }
        public virtual ICollection<Shipment>? AssignedShipments { get; set; }
    }
}