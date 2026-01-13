using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Carzvo.Models
{
    public class DriverApplication
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string? UserId { get; set; }

        [ForeignKey("UserId")]
        public virtual ApplicationUser? User { get; set; }

        [Required]
        [Display(Name = "Опыт работы (лет)")]
        [Range(0, 50)]
        public int ExperienceYears { get; set; }

        [Required]
        [Display(Name = "Типы транспорта")]
        [StringLength(500)]
        public string? VehicleTypes { get; set; }

        [Required]
        [Display(Name = "Комментарий")]
        [StringLength(1000)]
        public string? Comment { get; set; }

        [Display(Name = "Статус")]
        public ApplicationStatus Status { get; set; } = ApplicationStatus.Pending;

        [Display(Name = "Дата подачи")]
        public DateTime ApplicationDate { get; set; } = DateTime.Now;

        [Display(Name = "Дата рассмотрения")]
        public DateTime? ReviewDate { get; set; }

        [Display(Name = "Комментарий менеджера")]
        [StringLength(1000)]
        public string? ManagerComment { get; set; }


    }

    public enum ApplicationStatus
    {
        [Display(Name = "Ожидает")]
        Pending,
        [Display(Name = "Одобрено")]
        Approved,
        [Display(Name = "Отклонено")]
        Rejected
    }
}