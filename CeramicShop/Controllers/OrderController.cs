using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using CeramicShop.Data;
using CeramicShop.Models;
using CeramicShop.Models.ViewModels;
using System.Text.Json;
using System.Diagnostics;
using Newtonsoft.Json;
using System.Text;


namespace CeramicShop.Controllers
{
    [Authorize]
    public class OrderController : Controller
    {
        private readonly ApplicationDbContext _context;

        public OrderController(ApplicationDbContext context)
        {
            _context = context;
        }
        public JsonResult GenerateCode()
        {
            var code = Guid.NewGuid().ToString("N").Substring(0, 15); 
            return Json(new { code });
        }

        [HttpPost]
        public async Task<JsonResult> CheckPayment(int amount, string gencode)
        {
            using var httpClient = new HttpClient();
            var url = "http://103.82.36.41:5000/checkPayment";

            var data = new
            {
                amount,
                gencode
            };

            var response = await httpClient.PostAsJsonAsync(url, data);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<dynamic>();
                return Json(result); 
            }

            return Json(new { success = false, message = "Server error" });
        }


        [HttpPost]
        public async Task<IActionResult> PlaceOrder(int paymentMethodId, string shippingAddress, string notes)
        {
            try
            {
                var cart = GetCartFromSession();
                if (cart.Items.Count == 0)
                {
                    return RedirectToAction("Index", "Cart");
                }

                // Get current user
                var username = User.Identity.Name;
                var user = _context.Users.FirstOrDefault(u => u.UserName == username);
                if (user == null)
                {
                    return RedirectToAction("Login", "Account");
                }

                // Lấy khuyến mãi từ Session
                var promoCode = HttpContext.Session.GetString("AppliedPromoCode");
                var discountPercentageString = HttpContext.Session.GetString("PromoDiscountPercentage");
                decimal discountPercentage = 0;
                decimal systemWideDiscountPercentage = 0;

                var systemWidePromotion = await _context.Promotions
                    .FirstOrDefaultAsync(p => p.ProductID == null && p.IsActive
                                           && p.StartDate <= DateTime.Now && p.EndDate >= DateTime.Now);

                if (systemWidePromotion != null)
                {
                    systemWideDiscountPercentage = systemWidePromotion.DiscountPercentage;
                }

                if (!string.IsNullOrEmpty(discountPercentageString))
                    discountPercentage = decimal.Parse(discountPercentageString);

                decimal subtotal = cart.TotalAmount;
                decimal discountedTotal = discountPercentage > 0
                    ? subtotal * (1 - discountPercentage / 100)
                    : subtotal;

                if (systemWideDiscountPercentage > 0)
                {
                    discountedTotal = discountedTotal * (1 - systemWideDiscountPercentage / 100);
                }

                // Create order with safe datetime values
                var now = DateTime.Now;
                if (now.Year < 1753)
                {
                    now = new DateTime(1753, 1, 1);
                }

                var paymentMethod = _context.PaymentMethods.FirstOrDefault(pm => pm.PaymentMethodID == paymentMethodId);

                var order = new Order
                {
                    UserID = user.UserID,
                    TotalAmount = discountedTotal,
                    PaymentMethodID = paymentMethodId,
                    OrderStatus = (paymentMethod != null && paymentMethod.MethodName == "Chuyển khoản (Banking)") ? "Confirmed" : "Pending",
                    Notes = notes ?? string.Empty,
                    CreatedAt = now,
                    UpdatedAt = now,
                    ShippingAddress = shippingAddress ?? string.Empty
                };


                _context.Orders.Add(order);
                await _context.SaveChangesAsync(); // sử dụng async

                // Create order details
                foreach (var item in cart.Items)
                {
                    var orderDetail = new OrderDetail
                    {
                        OrderID = order.OrderID,
                        ProductID = item.ProductID,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice
                    };

                    _context.OrderDetails.Add(orderDetail);

                    // Update product stock
                    var product = await _context.Products.FindAsync(item.ProductID);
                    if (product != null)
                    {
                        product.StockQuantity -= item.Quantity;
                        _context.Entry(product).State = EntityState.Modified;
                    }
                }

                await _context.SaveChangesAsync(); // sử dụng async

                // Clear cart
                HttpContext.Session.Remove("Cart");
                HttpContext.Session.Remove("AppliedPromoCode");
                HttpContext.Session.Remove("PromoDiscountPercentage");

                return RedirectToAction("OrderConfirmation", new { id = order.OrderID });
            }
            catch (Exception ex)
            {
                // Log the error
                Console.WriteLine($"Error in PlaceOrder: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }

                // Add error to ModelState
                ModelState.AddModelError("", "Có lỗi xảy ra khi đặt hàng. Vui lòng thử lại sau.");

                // Get payment methods for the view
                var paymentMethods = _context.PaymentMethods
                    .Where(p => p.IsActive)
                    .ToList();

                ViewBag.PaymentMethods = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(paymentMethods, "PaymentMethodID", "MethodName");
                ViewBag.ErrorMessage = "Có lỗi xảy ra khi đặt hàng: " + ex.Message;

                // Return to checkout with cart
                return View("Checkout", GetCartFromSession());
            }
        }

        public IActionResult OrderConfirmation(int id)
        {
            try
            {
                var order = _context.Orders
                    .Include(o => o.User)
                    .Include(o => o.PaymentMethod)
                    .FirstOrDefault(o => o.OrderID == id);

                if (order == null || order.User.UserName != User.Identity.Name)
                {
                    return NotFound();
                }

                var viewModel = new OrderViewModel
                {
                    OrderID = order.OrderID,
                    TotalAmount = order.TotalAmount,
                    OrderStatus = order.OrderStatus ?? "Pending", // Default if null
                    CreatedAt = order.CreatedAt,
                    PaymentMethod = order.PaymentMethod?.MethodName ?? "Unknown", // Default if null
                    ShippingAddress = order.ShippingAddress ?? string.Empty // Default if null
                };

                var orderDetails = _context.OrderDetails
                    .Where(od => od.OrderID == id)
                    .Select(od => new OrderDetailViewModel
                    {
                        ProductID = od.ProductID,
                        ProductName = od.Product.ProductName,
                        ImagePath = _context.ProductImages
                            .Where(i => i.ProductID == od.ProductID && i.IsMainImage)
                            .Select(i => i.ImagePath)
                            .FirstOrDefault() ?? "/images/no-image.jpg",
                        Quantity = od.Quantity,
                        UnitPrice = od.UnitPrice,
                        TotalPrice = od.UnitPrice * od.Quantity
                    })
                    .ToList();

                viewModel.OrderDetails = orderDetails;

                return View(viewModel);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in OrderConfirmation: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }

                return RedirectToAction("TrackOrders");
            }
        }

        public IActionResult TrackOrders()
        {
            try
            {
                var username = User.Identity.Name;
                var orders = _context.Orders
                    .Include(o => o.User)
                    .Include(o => o.PaymentMethod)
                    .Where(o => o.User.UserName == username)
                    .OrderByDescending(o => o.CreatedAt)
                    .Select(o => new OrderViewModel
                    {
                        OrderID = o.OrderID,
                        TotalAmount = o.TotalAmount,
                        OrderStatus = o.OrderStatus ?? "Pending", // Default if null
                        CreatedAt = o.CreatedAt,
                        PaymentMethod = o.PaymentMethod.MethodName ?? "Unknown" // Default if null
                    })
                    .ToList();

                return View(orders);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in TrackOrders: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }

                return View(new List<OrderViewModel>());
            }
        }

        public IActionResult OrderDetails(int id)
        {
            try
            {
                var order = _context.Orders
                    .Include(o => o.User)
                    .Include(o => o.PaymentMethod)
                    .FirstOrDefault(o => o.OrderID == id);

                if (order == null || order.User.UserName != User.Identity.Name)
                {
                    return NotFound();
                }

                var viewModel = new OrderViewModel
                {
                    OrderID = order.OrderID,
                    TotalAmount = order.TotalAmount,
                    OrderStatus = order.OrderStatus ?? "Pending", // Default if null
                    CreatedAt = order.CreatedAt,
                    PaymentMethod = order.PaymentMethod?.MethodName ?? "Unknown", // Default if null
                    ShippingAddress = order.ShippingAddress ?? string.Empty // Default if null
                };

                var orderDetails = _context.OrderDetails
                    .Where(od => od.OrderID == id)
                    .Select(od => new OrderDetailViewModel
                    {
                        ProductID = od.ProductID,
                        ProductName = od.Product.ProductName,
                        ImagePath = _context.ProductImages
                            .Where(i => i.ProductID == od.ProductID && i.IsMainImage)
                            .Select(i => i.ImagePath)
                            .FirstOrDefault() ?? "/images/no-image.jpg",
                        Quantity = od.Quantity,
                        UnitPrice = od.UnitPrice,
                        TotalPrice = od.UnitPrice * od.Quantity
                    })
                    .ToList();

                viewModel.OrderDetails = orderDetails;

                return View(viewModel);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in OrderDetails: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }

                return RedirectToAction("TrackOrders");
            }
        }

        [HttpPost]
        public JsonResult CancelOrder(int orderId)
        {
            try
            {
                Debug.WriteLine($"CancelOrder called with orderId: {orderId}");

                // Lấy thông tin người dùng hiện tại
                var username = User.Identity.Name;
                Debug.WriteLine($"Current username: {username}");

                // Tìm đơn hàng theo ID
                var order = _context.Orders
                    .Include(o => o.User)
                    .FirstOrDefault(o => o.OrderID == orderId);

                if (order == null)
                {
                    Debug.WriteLine($"Order not found: {orderId}");
                    return Json(new { success = false, message = "Không tìm thấy đơn hàng." });
                }

                Debug.WriteLine($"Order found: {order.OrderID}, Status: {order.OrderStatus ?? "null"}");

                // Kiểm tra quyền hủy đơn hàng
                if (order.User == null || order.User.UserName != username)
                {
                    Debug.WriteLine($"User mismatch: Order user {order.User?.UserName ?? "null"} vs current user {username}");
                    return Json(new { success = false, message = "Bạn không có quyền hủy đơn hàng này." });
                }

                // Kiểm tra trạng thái đơn hàng
                string orderStatus = order.OrderStatus ?? "Unknown";
                if (orderStatus != "Pending" && orderStatus != "Confirmed")
                {
                    Debug.WriteLine($"Invalid order status for cancellation: {orderStatus}");
                    return Json(new { success = false, message = "Không thể hủy đơn hàng ở trạng thái này." });
                }

                // Lấy chi tiết đơn hàng để khôi phục số lượng sản phẩm
                var orderDetails = _context.OrderDetails
                    .Where(od => od.OrderID == orderId)
                    .ToList();

                Debug.WriteLine($"Found {orderDetails.Count} order details");

                foreach (var detail in orderDetails)
                {
                    var product = _context.Products.Find(detail.ProductID);
                    if (product != null)
                    {
                        product.StockQuantity += detail.Quantity;
                        Debug.WriteLine($"Restored {detail.Quantity} items to product {product.ProductID}");
                    }
                }

                // Cập nhật trạng thái đơn hàng
                order.OrderStatus = "Cancelled";
                order.UpdatedAt = DateTime.Now;

                Debug.WriteLine("Saving changes to database");
                _context.SaveChanges();
                Debug.WriteLine("Changes saved successfully");

                return Json(new { success = true, message = "Đơn hàng đã được hủy thành công." });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in CancelOrder: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Debug.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");

                return Json(new { success = false, message = "Lỗi khi hủy đơn hàng: " + ex.Message });
            }
        }

        private CartViewModel GetCartFromSession()
        {
            var cartJson = HttpContext.Session.GetString("Cart");
            if (string.IsNullOrEmpty(cartJson))
            {
                return new CartViewModel();
            }

            return Newtonsoft.Json.JsonConvert.DeserializeObject<CartViewModel>(cartJson);
        }
    }
}
