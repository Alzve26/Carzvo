using System.Collections.Generic;
using Carzvo.Models;

namespace Carzvo.Models
{
    public class DashboardViewModel
    {
        public bool IsAdminOrManager { get; set; }
        public bool IsDriver { get; set; }
        public int TotalShipments { get; set; }
        public int PendingShipments { get; set; }
        public int InTransitShipments { get; set; }
        public int TotalUsers { get; set; }
        public int TotalDrivers { get; set; }
        public List<Shipment>? RecentShipments { get; set; }
        public List<Shipment>? MyShipments { get; set; }
    }
}