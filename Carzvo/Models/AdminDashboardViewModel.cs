using System.Collections.Generic;

namespace Carzvo.Models
{
    public class AdminDashboardViewModel
    {
        public int TotalUsers { get; set; }
        public int TotalDrivers { get; set; }
        public int TotalShipments { get; set; }
        public int PendingShipments { get; set; }
        public int PendingDriverApplications { get; set; }
        public List<ApplicationUser>? RecentUsers { get; set; }
    }
}