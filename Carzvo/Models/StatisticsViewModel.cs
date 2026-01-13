using System.Collections.Generic;

namespace Carzvo.Models
{
    public class StatisticsViewModel
    {
        public Dictionary<string, int>? ShipmentsByStatus { get; set; }
        public Dictionary<string, int>? ShipmentsByMonth { get; set; }
        public Dictionary<string, int>? ShipmentsByVehicleType { get; set; }
        public Dictionary<string, int>? UsersByRole { get; set; }
    }
}