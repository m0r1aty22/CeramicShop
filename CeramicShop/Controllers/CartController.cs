using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CeramicShop.Data;
using CeramicShop.Models;
using CeramicShop.Models.ViewModels;
using System.Text.Json;
using System.Threading.Tasks;

namespace CeramicShop.Controllers
{
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CartController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            var cart = GetCartFromSession();
            return View(cart);
        }

        [HttpPost]
        public async Task<IActionResult> AddToCart(int productId, int quantity = 1)
        {
            var product = await _context.Products.FindAsync(productId);
            if (product == null || !product.IsActive)
            {
                return Json(new { success = false, message = "Không tìm thấy sản phẩm" });
            }

            if (quantity <= 0)
            {
                return Json(new { success = false, message = "Số lượng không hợp lệ" });
            }

            if (product.StockQuantity < quantity)
            {
                return Json(new { success = false, message = "Không đủ số lượng trong kho" });
            }

            var cart = GetCartFromSession();
            var existingItem = cart.Items.FirstOrDefault(i => i.ProductID == productId);

            if (existingItem != null)
            {
                existingItem.Quantity += quantity;
            }
            else
            {
                var mainImage = await _context.ProductImages
                    .Where(i => i.ProductID == productId && i.IsMainImage)
                    .Select(i => i.ImagePath)
                    .FirstOrDefaultAsync() ?? "/Images/no-image.jpg";

                // Kiểm tra khuyến mãi đang áp dụng cho sản phẩm
                var promotion = await _context.Promotions
                    .Where(p => p.ProductID == productId && p.IsActive
                                && p.StartDate <= DateTime.Now && p.EndDate >= DateTime.Now)
                    .OrderByDescending(p => p.DiscountPercentage)
                    .FirstOrDefaultAsync();

                decimal finalPrice = product.Price;

                if (promotion != null)
                {
                    finalPrice = product.Price * (1 - promotion.DiscountPercentage / 100);
                }

                cart.Items.Add(new CartItemViewModel
                {
                    ProductID = productId,
                    ProductName = product.ProductName,
                    ImagePath = mainImage,
                    UnitPrice = finalPrice,
                    Quantity = quantity
                });
            }

            SaveCartToSession(cart);

            return Json(new
            {
                success = true,
                cartCount = cart.Items.Sum(i => i.Quantity),
                message = "Sản phẩm đã được thêm vào giỏ hàng!",
                productName = product.ProductName
            });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateQuantity(int productId, int quantity)
        {
            if (quantity <= 0)
            {
                return Json(new { success = false, message = "Số lượng không hợp lệ" });
            }

            var cart = GetCartFromSession();
            var item = cart.Items.FirstOrDefault(i => i.ProductID == productId);

            if (item == null)
            {
                return Json(new { success = false, message = "Không tìm thấy sản phẩm trong giỏ hàng" });
            }

            var product = await _context.Products.FindAsync(productId);
            if (product == null || product.StockQuantity < quantity)
            {
                return Json(new { success = false, message = "Không đủ số lượng trong kho" });
            }

            item.Quantity = quantity;
            SaveCartToSession(cart);

            return Json(new
            {
                success = true,
                itemTotal = item.TotalPrice,
                cartTotal = cart.TotalAmount,
                cartCount = cart.Items.Sum(i => i.Quantity),
                message = "Số lượng đã được cập nhật!"
            });
        }

        [HttpPost]
        public IActionResult RemoveFromCart(int productId)
        {
            var cart = GetCartFromSession();
            var item = cart.Items.FirstOrDefault(i => i.ProductID == productId);

            if (item != null)
            {
                cart.Items.Remove(item);
                SaveCartToSession(cart);
            }

            return Json(new
            {
                success = true,
                cartTotal = cart.TotalAmount,
                cartCount = cart.Items.Sum(i => i.Quantity),
                message = "Sản phẩm đã được xóa khỏi giỏ hàng!"
            });
        }

        [HttpPost]
        public async Task<IActionResult> ApplyPromoCode(string promoCode)
        {
            if (string.IsNullOrEmpty(promoCode))
            {
                return Json(new { success = false, message = "Vui lòng nhập mã khuyến mãi." });
            }

            var promotion = await _context.Promotions
                .FirstOrDefaultAsync(p => p.PromoCode == promoCode &&
                                    p.IsActive &&
                                    DateTime.Now >= p.StartDate &&
                                    DateTime.Now <= p.EndDate);

            if (promotion == null)
            {
                return Json(new { success = false, message = "Mã khuyến mãi không hợp lệ hoặc đã hết hạn." });
            }

            try
            {
                var cart = GetCartFromSession();
                var subtotal = cart.TotalAmount;
                var discount = subtotal * (promotion.DiscountPercentage / 100);
                var total = subtotal - discount;

                HttpContext.Session.SetString("AppliedPromoCode", promoCode);
                HttpContext.Session.SetString("PromoDiscountPercentage", promotion.DiscountPercentage.ToString());

                return Json(new
                {
                    success = true,
                    discountPercentage = promotion.DiscountPercentage,
                    discount = discount,
                    total = total,
                    message = "Mã khuyến mãi đã được áp dụng thành công!"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Lỗi khi áp dụng mã khuyến mãi: {ex.Message}" });
            }
        }

        public async Task<IActionResult> Checkout()
        {
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Checkout", "Cart") });
            }

            var cart = GetCartFromSession();
            if (cart.Items.Count == 0)
            {
                return RedirectToAction("Index", "Cart");
            }

            // Lấy thông tin giảm giá từ Session
            var promoCode = HttpContext.Session.GetString("AppliedPromoCode");
            var discountPercentageString = HttpContext.Session.GetString("PromoDiscountPercentage");
            decimal discountPercentage = 0;

            if (!string.IsNullOrEmpty(discountPercentageString) && decimal.TryParse(discountPercentageString, out var parsedDiscount))
            {
                discountPercentage = parsedDiscount;
            }

            // Kiểm tra khuyến mãi toàn hệ thống (nếu có)
            var systemWidePromotion = await _context.Promotions
                .FirstOrDefaultAsync(p => p.ProductID == null && p.IsActive
                                      && p.StartDate <= DateTime.Now && p.EndDate >= DateTime.Now);

            decimal systemWideDiscountPercentage = 0;
            if (systemWidePromotion != null)
            {
                systemWideDiscountPercentage = systemWidePromotion.DiscountPercentage;
            }

            // Tính toán tổng tiền
            var subtotal = cart.TotalAmount;
            var discountedTotal = subtotal;

            // Áp dụng khuyến mãi mã giảm giá (nếu có)
            if (discountPercentage > 0)
            {
                discountedTotal = discountedTotal * (1 - discountPercentage / 100);
            }

            // Áp dụng khuyến mãi toàn hệ thống (nếu có), cộng dồn với mã giảm giá
            if (systemWideDiscountPercentage > 0)
            {
                discountedTotal = discountedTotal * (1 - systemWideDiscountPercentage / 100);
            }

            ViewBag.OriginalTotal = subtotal;
            ViewBag.DiscountedTotal = discountedTotal;
            ViewBag.DiscountPercentage = discountPercentage;
            ViewBag.SystemWideDiscountPercentage = systemWideDiscountPercentage;
            ViewBag.PromoCode = promoCode;

            // Lấy danh sách phương thức thanh toán
            var paymentMethods = await _context.PaymentMethods
                .Where(p => p.IsActive)
                .ToListAsync();

            ViewBag.PaymentMethods = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(paymentMethods, "PaymentMethodID", "MethodName");

            return View(cart);
        }

        [HttpGet]
        public IActionResult GetCartCount()
        {
            try
            {
                var cart = GetCartFromSession();
                return Json(new { count = cart.Items.Sum(i => i.Quantity) });
            }
            catch (Exception)
            {
                return Json(new { count = 0 });
            }
        }

        private CartViewModel GetCartFromSession()
        {
            try
            {
                var cartJson = HttpContext.Session.GetString("Cart");
                if (string.IsNullOrEmpty(cartJson))
                {
                    return new CartViewModel();
                }

                return JsonSerializer.Deserialize<CartViewModel>(cartJson);
            }
            catch (Exception)
            {
                return new CartViewModel();
            }
        }

        private void SaveCartToSession(CartViewModel cart)
        {
            var cartJson = JsonSerializer.Serialize(cart);
            HttpContext.Session.SetString("Cart", cartJson);
        }
    }
}