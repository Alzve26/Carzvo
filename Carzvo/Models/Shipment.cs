using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Carzvo.Models
{
    public enum ShipmentStatus
    {
        [Display(Name = "Ожидает обработки")]
        Pending,

        [Display(Name = "Обрабатывается")]
        Processing,

        [Display(Name = "Назначен водителю")]
        Assigned,

        [Display(Name = "В пути")]
        InTransit,

        [Display(Name = "Доставлен")]
        Delivered,

        [Display(Name = "Отменен")]
        Cancelled
    }

    public enum VehicleType
    {
        [Display(Name = "Легковой автомобиль")]
        Car,

        [Display(Name = "Фургон")]
        Van,

        [Display(Name = "Грузовик (до 3.5т)")]
        SmallTruck,

        [Display(Name = "Грузовик (до 10т)")]
        MediumTruck,

        [Display(Name = "Грузовик (свыше 10т)")]
        LargeTruck,

        [Display(Name = "Рефрижератор")]
        Refrigerator,

        [Display(Name = "Цистерна")]
        Tanker
    }

    public enum CargoType
    {
        [Display(Name = "Бытовая техника")]
        Appliances,

        [Display(Name = "Мебель")]
        Furniture,

        [Display(Name = "Строительные материалы")]
        Construction,

        [Display(Name = "Продукты питания")]
        Food,

        [Display(Name = "Химические вещества")]
        Chemicals,

        [Display(Name = "Опасные грузы")]
        Hazardous,

        [Display(Name = "Другое")]
        Other
    }

    public class Shipment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "Номер заказа")]
        [StringLength(20)]
        public string OrderNumber { get; set; } = "ORD-" + DateTime.Now.ToString("yyyyMMddHHmmss");

        [Required]
        [Display(Name = "Описание груза")]
        [StringLength(500, ErrorMessage = "Максимальная длина 500 символов")]
        public string Description { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Тип груза")]
        public CargoType CargoType { get; set; }

        [Required]
        [Display(Name = "Тип транспорта")]
        public VehicleType VehicleType { get; set; }

        [Required]
        [Display(Name = "Вес (кг)")]
        [Range(0.1, 100000, ErrorMessage = "Вес должен быть от 0.1 до 100000 кг")]
        public decimal Weight { get; set; }

        [Required]
        [Display(Name = "Объем (м³)")]
        [Range(0.1, 1000, ErrorMessage = "Объем должен быть от 0.1 до 1000 м³")]
        public decimal Volume { get; set; }

        [Required]
        [Display(Name = "Адрес загрузки")]
        [StringLength(500)]
        public string PickupAddress { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Адрес доставки")]
        [StringLength(500)]
        public string DeliveryAddress { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Дата загрузки")]
        [DataType(DataType.DateTime)]
        public DateTime PickupDate { get; set; } = DateTime.Now.AddDays(1);

        [Required]
        [Display(Name = "Дата доставки")]
        [DataType(DataType.DateTime)]
        public DateTime DeliveryDate { get; set; } = DateTime.Now.AddDays(2);

        [Required]
        [Display(Name = "Статус")]
        public ShipmentStatus Status { get; set; } = ShipmentStatus.Pending;

        [Display(Name = "Цена")]
        [Column(TypeName = "decimal(18,2)")]
        [Range(0, 10000000, ErrorMessage = "Цена должна быть от 0 до 10,000,000")]
        public decimal Price { get; set; }

        [Display(Name = "Примечания")]
        [StringLength(2000)]
        public string? Notes { get; set; }

        [Display(Name = "Дата создания")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Display(Name = "Дата обновления")]
        public DateTime? UpdatedAt { get; set; }

        [Display(Name = "Дата завершения")]
        public DateTime? CompletedAt { get; set; }

        // Внешние ключи
        [Required]
        public string? UserId { get; set; }

        [ForeignKey("UserId")]
        [Display(Name = "Клиент")]
        public virtual ApplicationUser? User { get; set; }

        public string? DriverId { get; set; }

        [ForeignKey("DriverId")]
        [Display(Name = "Водитель")]
        public virtual ApplicationUser? Driver { get; set; }
    }
}