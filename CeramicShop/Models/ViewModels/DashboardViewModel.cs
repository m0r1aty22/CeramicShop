using System;
using System.Collections.Generic;

namespace CeramicShop.Models
{
    public class DashboardViewModel
    {
        public int TotalProducts { get; set; }
        public int TotalUsers { get; set; }
        public int TotalOrders { get; set; }
        public decimal? TotalRevenue { get; set; }

        public int PendingOrders { get; set; }
        public int ConfirmedOrders { get; set; }
        public int CancelledOrders { get; set; }

        public List<Order> RecentOrders { get; set; }
        public List<TopProductViewModel> TopProducts { get; set; }
        public List<ChartDataViewModel> MonthlyOrders { get; set; }
        public List<ChartDataViewModel> MonthlyRevenue { get; set; }
    }

    public class TopProductViewModel
    {
        public int ProductID { get; set; }
        public string ProductName { get; set; }
        public int TotalSold { get; set; }
    }

    public class ChartDataViewModel
    {
        public string Label { get; set; }
        public decimal Value { get; set; }
    }
}
