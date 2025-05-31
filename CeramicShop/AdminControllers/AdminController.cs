using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CeramicShop.Data;
using CeramicShop.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using CeramicShop.Models.ViewModels;

namespace CeramicShop.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // Thống kê tổng quan cho Dashboard
            var viewModel = new DashboardViewModel
            {
                TotalProducts = await _context.Products.CountAsync(),
                TotalUsers = await _context.Users.CountAsync(u => u.Role == "Customer"),
                TotalOrders = await _context.Orders.CountAsync(),
                TotalRevenue = await _context.Orders
                    .Where(o => o.OrderStatus != "Cancelled")
                    .SumAsync(o => o.TotalAmount),

                // Đơn hàng theo trạng thái
                PendingOrders = await _context.Orders.CountAsync(o => o.OrderStatus == "Pending"),
                ConfirmedOrders = await _context.Orders.CountAsync(o => o.OrderStatus == "Confirmed"),
                CancelledOrders = await _context.Orders.CountAsync(o => o.OrderStatus == "Cancelled"),

                // Đơn hàng gần đây
                RecentOrders = await _context.Orders
                    .Include(o => o.User)
                    .OrderByDescending(o => o.CreatedAt)
                    .Take(5)
                    .ToListAsync(),

                // Sản phẩm bán chạy
                TopProducts = await _context.OrderDetails
                    .GroupBy(od => od.ProductID)
                    .Select(g => new TopProductViewModel
                    {
                        ProductID = g.Key,
                        ProductName = g.First().Product.ProductName,
                        TotalSold = g.Sum(od => od.Quantity)
                    })
                    .OrderByDescending(p => p.TotalSold)
                    .Take(5)
                    .ToListAsync(),

                // Dữ liệu biểu đồ đơn hàng theo tháng
                MonthlyOrders = await _context.Orders
                    .Where(o => o.CreatedAt >= DateTime.Now.AddMonths(-6))
                    .GroupBy(o => new { Month = o.CreatedAt.Month, Year = o.CreatedAt.Year })
                    .Select(g => new ChartDataViewModel
                    {
                        Label = g.Key.Month + "/" + g.Key.Year,
                        Value = g.Count()
                    })
                    .ToListAsync(),

                // Dữ liệu biểu đồ doanh thu theo tháng
                MonthlyRevenue = await _context.Orders
                    .Where(o => o.CreatedAt >= DateTime.Now.AddMonths(-6) && o.OrderStatus != "Cancelled")
                    .GroupBy(o => new { Month = o.CreatedAt.Month, Year = o.CreatedAt.Year })
                    .Select(g => new ChartDataViewModel
                    {
                        Label = g.Key.Month + "/" + g.Key.Year,
                        Value = g.Sum(o => o.TotalAmount)
                    })
                    .ToListAsync()
            };

            return View(viewModel);
        }
    }
}