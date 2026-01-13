namespace Carzvo.Models
{
    public class SystemSettingsViewModel
    {
        public string CompanyName { get; set; } = string.Empty;
        public string SupportEmail { get; set; } = string.Empty;
        public string SupportPhone { get; set; } = string.Empty;
        public decimal DefaultPricePerKg { get; set; }
        public decimal UrgentDeliveryMultiplier { get; set; }
        public decimal MaxWeightPerShipment { get; set; }
    }
}