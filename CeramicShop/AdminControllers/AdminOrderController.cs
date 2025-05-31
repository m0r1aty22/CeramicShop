using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using CeramicShop.Data;
using CeramicShop.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

namespace CeramicShop.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminOrderController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AdminOrderController> _logger;

        public AdminOrderController(ApplicationDbContext context, ILogger<AdminOrderController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: AdminOrder
        public async Task<IActionResult> Index(string searchString, string status, DateTime? fromDate, DateTime? toDate)
        {
            var orders = _context.Orders
                .Include(o => o.User)
                .Include(o => o.PaymentMethod)
                .AsQueryable();

            // Lọc theo từ khóa tìm kiếm
            if (!string.IsNullOrEmpty(searchString))
            {
                orders = orders.Where(o => o.OrderID.ToString().Contains(searchString) ||
                                          o.User.FullName.Contains(searchString) ||
                                          o.User.Email.Contains(searchString) ||
                                          o.User.Phone.Contains(searchString));
            }

            // Lọc theo trạng thái
            if (!string.IsNullOrEmpty(status))
            {
                orders = orders.Where(o => o.OrderStatus == status);
            }

            // Lọc theo ngày bắt đầu
            if (fromDate.HasValue)
            {
                orders = orders.Where(o => o.CreatedAt >= fromDate.Value);
            }

            // Lọc theo ngày kết thúc
            if (toDate.HasValue)
            {
                // Thêm 1 ngày để bao gồm cả ngày kết thúc
                var nextDay = toDate.Value.AddDays(1);
                orders = orders.Where(o => o.CreatedAt < nextDay);
            }

            // Sắp xếp theo ngày tạo mới nhất
            orders = orders.OrderByDescending(o => o.CreatedAt);

            ViewBag.SearchString = searchString;
            ViewBag.Status = status;
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
            ViewBag.StatusList = new List<string> { "Pending", "Confirmed", "Shipped", "Delivered", "Cancelled" };

            return View(await orders.ToListAsync());
        }

        // GET: AdminOrder/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var order = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.PaymentMethod)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                        .ThenInclude(p => p.ProductImages)
                .FirstOrDefaultAsync(m => m.OrderID == id);

            if (order == null)
            {
                return NotFound();
            }

            return View(order);
        }

        // GET: AdminOrder/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var order = await _context.Orders.FindAsync(id);
            if (order == null)
            {
                return NotFound();
            }

            ViewBag.UserID = new SelectList(_context.Users, "UserID", "FullName", order.UserID);
            ViewBag.PaymentMethodID = new SelectList(_context.PaymentMethods, "PaymentMethodID", "MethodName", order.PaymentMethodID);
            ViewBag.OrderStatuses = new List<SelectListItem>
            {
                new SelectListItem { Value = "Pending", Text = "Đang xử lý" },
                new SelectListItem { Value = "Confirmed", Text = "Đã xác nhận" },
                new SelectListItem { Value = "Shipped", Text = "Đang giao" },
                new SelectListItem { Value = "Delivered", Text = "Đã giao" },
                new SelectListItem { Value = "Cancelled", Text = "Đã huỷ" },
            };

            return View(order);
        }

        // POST: AdminOrder/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("OrderID,UserID,TotalAmount,PaymentMethodID,OrderStatus,ShippingAddress,Notes")] Order order)
        {
            if (id != order.OrderID)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                LogModelErrors();
                // Lấy đối tượng order hiện có từ DB (theo dõi đầy đủ các navigation nếu cần)
                var existingOrder = await _context.Orders
                    .Include(o => o.OrderDetails)
                    .FirstOrDefaultAsync(o => o.OrderID == id);

                if (existingOrder == null)
                {
                    return NotFound();
                }


                // Lưu lại trạng thái cũ để xử lý logic tồn kho
                var previousStatus = existingOrder.OrderStatus;

                // Cập nhật các trường cho đơn hàng
                existingOrder.TotalAmount = order.TotalAmount;
                existingOrder.PaymentMethodID = order.PaymentMethodID;
                existingOrder.OrderStatus = order.OrderStatus;
                existingOrder.ShippingAddress = order.ShippingAddress;
                existingOrder.Notes = order.Notes;
                existingOrder.UpdatedAt = DateTime.Now;
                // Không cập nhật CreatedAt (giữ nguyên giá trị ban đầu)

                // Nếu trạng thái đơn hàng thay đổi từ "Cancelled" sang trạng thái khác
                // hoặc từ trạng thái khác sang "Cancelled", cập nhật số lượng sản phẩm.
                if (previousStatus != order.OrderStatus &&
                    (previousStatus == "Cancelled" || order.OrderStatus == "Cancelled"))
                {
                    // Lấy danh sách chi tiết đơn hàng
                    var orderDetails = await _context.OrderDetails
                        .Where(od => od.OrderID == id)
                        .ToListAsync();

                    foreach (var detail in orderDetails)
                    {
                        var product = await _context.Products.FindAsync(detail.ProductID);
                        if (product != null)
                        {
                            // Nếu từ trạng thái "Cancelled" sang trạng thái khác: cần giảm tồn kho
                            if (previousStatus == "Cancelled" && order.OrderStatus != "Cancelled")
                            {
                                if (product.StockQuantity < detail.Quantity)
                                {
                                    ModelState.AddModelError("", $"Không đủ tồn kho cho sản phẩm {product.ProductName}.");

                                    // Cần thiết lập lại dữ liệu cho dropdown / ViewBag khi trả về View
                                    ViewBag.UserID = new SelectList(_context.Users, "UserID", "FullName", order.UserID);
                                    ViewBag.PaymentMethodID = new SelectList(_context.PaymentMethods, "PaymentMethodID", "MethodName", order.PaymentMethodID);
                                    ViewBag.OrderStatuses = new List<SelectListItem>
                                        {
                                            new SelectListItem { Value = "Pending", Text = "Đang xử lý" },
                                            new SelectListItem { Value = "Confirmed", Text = "Đã xác nhận" },
                                            new SelectListItem { Value = "Shipped", Text = "Đang giao" },
                                            new SelectListItem { Value = "Delivered", Text = "Đã giao" },
                                            new SelectListItem { Value = "Cancelled", Text = "Đã huỷ" },
                                        };
                                    return View(order);
                                }
                                product.StockQuantity -= detail.Quantity;
                            }
                            // Nếu từ trạng thái khác sang "Cancelled": tăng tồn kho
                            else if (previousStatus != "Cancelled" && order.OrderStatus == "Cancelled")
                            {
                                product.StockQuantity += detail.Quantity;
                            }

                            _context.Update(product);
                        }
                    }
                }

                try
                {
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Đơn hàng đã được cập nhật thành công.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    // Nếu dữ liệu đã được cập nhật bởi người khác, thông báo cho người dùng
                    ModelState.AddModelError("", "Đơn hàng đã bị thay đổi bởi người khác. Vui lòng tải lại trang và thử lại.");
                }
            }

            ViewBag.UserID = new SelectList(_context.Users, "UserID", "FullName", order.UserID);
            ViewBag.PaymentMethodID = new SelectList(_context.PaymentMethods, "PaymentMethodID", "MethodName", order.PaymentMethodID);
            ViewBag.OrderStatuses = new List<SelectListItem>
            {
                new SelectListItem { Value = "Pending", Text = "Đang xử lý" },
                new SelectListItem { Value = "Confirmed", Text = "Đã xác nhận" },
                new SelectListItem { Value = "Shipped", Text = "Đang giao" },
                new SelectListItem { Value = "Delivered", Text = "Đã giao" },
                new SelectListItem { Value = "Cancelled", Text = "Đã huỷ" },
            };

            return View(order);
        }


        private bool OrderExists(int id)
        {
            return _context.Orders.Any(e => e.OrderID == id);
        }
        private void LogModelErrors()
        {
            foreach (var entry in ModelState)
            {
                foreach (var error in entry.Value.Errors)
                {
                    _logger.LogError("❌ ModelState Error - Field: {Field} - Message: {Message}", entry.Key, error.ErrorMessage);
                }
            }
        }
    }
}
