using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CeramicShop.Data;
using CeramicShop.Models;
using CeramicShop.Models.ViewModels;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace CeramicShop.Controllers
{
    public class WishlistController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<WishlistController> _logger;

        public WishlistController(ApplicationDbContext context, ILogger<WishlistController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [Authorize] // Yêu cầu đăng nhập để xem danh sách yêu thích
        public async Task<IActionResult> Index()
        {
            var username = User.Identity.Name;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == username);

            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var wishlistItems = await _context.WishlistItems
                .Where(w => w.UserID == user.UserID)
                .Include(w => w.Product)
                .ThenInclude(p => p.SubCategory)
                .ThenInclude(s => s.Category)
                .Select(w => new ProductViewModel
                {
                    ProductID = w.ProductID,
                    ProductName = w.Product.ProductName,
                    Price = w.Product.Price,
                    MainImagePath = _context.ProductImages
                        .Where(i => i.ProductID == w.ProductID && i.IsMainImage)
                        .Select(i => i.ImagePath)
                        .FirstOrDefault() ?? "/Images/no-image.jpg",
                    AddedToWishlistAt = w.AddedAt,
                    StockQuantity = w.Product.StockQuantity,
                    CategoryName = w.Product.SubCategory.Category.CategoryName,
                    SubCategoryName = w.Product.SubCategory.SubCategoryName,
                    DiscountPercentage = _context.Promotions
                        .Where(p => p.ProductID == w.ProductID &&
                                    p.IsActive &&
                                    DateTime.Now >= p.StartDate &&
                                    DateTime.Now <= p.EndDate)
                        .OrderByDescending(p => p.DiscountPercentage)
                        .Select(p => (decimal?)p.DiscountPercentage)
                        .FirstOrDefault(),

                    HasSpecificPromotion = _context.Promotions.Any(p =>
                        p.ProductID == w.ProductID &&
                        p.IsActive &&
                        p.StartDate <= DateTime.Now &&
                        p.EndDate >= DateTime.Now)
                })
                .ToListAsync();

            // Tính giá sau khuyến mãi cho mỗi sản phẩm
            foreach (var item in wishlistItems)
            {
                if (item.HasSpecificPromotion && item.DiscountPercentage.HasValue)
                {
                    item.DiscountedPrice = item.Price - (item.Price * item.DiscountPercentage.Value / 100);
                }
            }

            return View(wishlistItems);
        }

        [HttpPost]
        // [Authorize] // Bỏ Authorize ở đây để có thể kiểm tra và trả về redirectToLogin
        public async Task<IActionResult> ToggleWishlist(int productId)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return Json(new { success = false, redirectToLogin = true, message = "Bạn cần đăng nhập để quản lý danh sách yêu thích." });
            }

            var username = User.Identity.Name;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == username);

            if (user == null)
            {
                // Trường hợp hiếm khi đã IsAuthenticated nhưng không tìm thấy user
                _logger.LogWarning("Authenticated user '{Username}' not found in database during ToggleWishlist.", username);
                return Json(new { success = false, message = "Không thể xác thực người dùng." });
            }

            var product = await _context.Products.FindAsync(productId);
            if (product == null || !product.IsActive)
            {
                return Json(new { success = false, message = "Sản phẩm không tồn tại hoặc đã ngừng kinh doanh." });
            }

            var wishlistItem = await _context.WishlistItems
                                .FirstOrDefaultAsync(w => w.UserID == user.UserID && w.ProductID == productId);

            bool itemAdded;
            string notificationMessage;

            try
            {
                if (wishlistItem == null)
                {
                    // Thêm vào wishlist
                    var newItem = new WishlistItem
                    {
                        UserID = user.UserID,
                        ProductID = productId,
                        AddedAt = DateTime.UtcNow // Sử dụng UtcNow cho nhất quán
                    };
                    _context.WishlistItems.Add(newItem);
                    await _context.SaveChangesAsync();
                    itemAdded = true;
                    notificationMessage = $"Đã thêm {product.ProductName} vào danh sách yêu thích.";
                }
                else
                {
                    // Xóa khỏi wishlist
                    _context.WishlistItems.Remove(wishlistItem);
                    await _context.SaveChangesAsync();
                    itemAdded = false;
                    notificationMessage = $"Đã xóa {product.ProductName} khỏi danh sách yêu thích.";
                }

                // Lấy số lượng sản phẩm trong wishlist hiện tại để cập nhật UI nếu cần
                int wishlistCount = await _context.WishlistItems.CountAsync(w => w.UserID == user.UserID);

                return Json(new
                {
                    success = true,
                    added = itemAdded, // true nếu sản phẩm giờ đây nằm trong wishlist, false nếu không
                    productName = product.ProductName,
                    wishlistCount = wishlistCount,
                    message = notificationMessage // Client có thể dùng message này hoặc tự tạo
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi toggle sản phẩm {ProductId} trong danh sách yêu thích của người dùng {UserId}", productId, user.UserID);
                return Json(new { success = false, message = "Đã có lỗi xảy ra, vui lòng thử lại." });
            }
        }

        // Action AddToWishlist cũ có thể được giữ lại nếu bạn có nơi nào đó gọi nó với logic cụ thể
        // nhưng các nút wishlist chung nên gọi ToggleWishlist.
        // Nếu bạn không dùng AddToWishlist cũ nữa, có thể xóa hoặc comment nó.
        [HttpPost]
        public async Task<IActionResult> AddToWishlist(int productId) // Giữ lại action này nếu cần
        {
            if (!User.Identity.IsAuthenticated)
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập để thêm sản phẩm vào danh sách yêu thích.", redirectToLogin = true });
            }

            var username = User.Identity.Name;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == username);

            if (user == null)
            {
                return Json(new { success = false, message = "Không tìm thấy thông tin người dùng." });
            }

            var product = await _context.Products.FindAsync(productId);
            if (product == null || !product.IsActive)
            {
                return Json(new { success = false, message = "Sản phẩm không tồn tại hoặc đã bị vô hiệu hóa." });
            }

            var existingItem = await _context.WishlistItems
                .FirstOrDefaultAsync(w => w.UserID == user.UserID && w.ProductID == productId);

            if (existingItem != null)
            {
                // Logic cũ: Trả về alreadyExists = true
                return Json(new
                {
                    success = true, // Vẫn là success vì sản phẩm đã có, không phải lỗi
                    message = $"{product.ProductName} đã có trong danh sách yêu thích của bạn.",
                    added = true, // Vẫn là true vì nó đang ở trong wishlist
                    alreadyExists = true, // Cờ để client biết
                    productName = product.ProductName
                });
            }
            else
            {
                try
                {
                    var wishlistItem = new WishlistItem { UserID = user.UserID, ProductID = productId, AddedAt = DateTime.UtcNow };
                    _context.WishlistItems.Add(wishlistItem);
                    await _context.SaveChangesAsync();
                    int wishlistCount = await _context.WishlistItems.CountAsync(w => w.UserID == user.UserID);
                    return Json(new
                    {
                        success = true,
                        message = $"Đã thêm {product.ProductName} vào danh sách yêu thích.",
                        added = true,
                        alreadyExists = false,
                        productName = product.ProductName,
                        wishlistCount = wishlistCount
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi thêm sản phẩm {ProductId} vào danh sách yêu thích (action AddToWishlist) của người dùng {UserId}", productId, user.UserID);
                    return Json(new { success = false, message = "Lỗi khi thêm sản phẩm vào danh sách yêu thích." });
                }
            }
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> RemoveFromWishlist(int productId)
        {
            var username = User.Identity.Name;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == username);

            if (user == null)
            {
                return Json(new { success = false, message = "Không tìm thấy thông tin người dùng." });
            }

            var wishlistItem = await _context.WishlistItems
                .FirstOrDefaultAsync(w => w.ProductID == productId && w.UserID == user.UserID);

            if (wishlistItem == null)
            {
                return Json(new { success = false, message = "Không tìm thấy sản phẩm trong danh sách yêu thích." });
            }

            var product = await _context.Products.FindAsync(productId);
            string productName = product != null ? product.ProductName : "Sản phẩm";

            try
            {
                _context.WishlistItems.Remove(wishlistItem);
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = $"Đã xóa {productName} khỏi danh sách yêu thích.",
                    productName = productName
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xóa sản phẩm {ProductId} khỏi danh sách yêu thích của người dùng {UserId}", productId, user.UserID);
                return Json(new { success = false, message = "Lỗi khi xóa sản phẩm khỏi danh sách yêu thích." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> IsInWishlist(int productId)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return Json(new { inWishlist = false });
            }

            var username = User.Identity.Name;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == username);

            if (user == null)
            {
                return Json(new { inWishlist = false });
            }

            var exists = await _context.WishlistItems
                .AnyAsync(w => w.UserID == user.UserID && w.ProductID == productId);

            return Json(new { inWishlist = exists });
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> MoveToCart(int productId)
        {
            var username = User.Identity.Name;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == username);

            if (user == null)
            {
                return Json(new { success = false, message = "Không tìm thấy thông tin người dùng." });
            }

            var wishlistItem = await _context.WishlistItems
                .Include(w => w.Product)
                .FirstOrDefaultAsync(w => w.ProductID == productId && w.UserID == user.UserID);

            if (wishlistItem == null)
            {
                return Json(new { success = false, message = "Không tìm thấy sản phẩm trong danh sách yêu thích." });
            }

            // Kiểm tra sản phẩm còn hàng không
            if (wishlistItem.Product.StockQuantity <= 0)
            {
                return Json(new { success = false, message = "Sản phẩm đã hết hàng." });
            }

            try
            {
                // Gọi CartController.AddToCart để thêm sản phẩm vào giỏ hàng
                var cartController = new CartController(_context);
                cartController.ControllerContext = ControllerContext;
                var result = await cartController.AddToCart(wishlistItem.ProductID, 1) as JsonResult;

                if (result != null)
                {
                    var data = result.Value as dynamic;
                    if (data != null && data.success == true)
                    {
                        // Xóa sản phẩm khỏi wishlist
                        _context.WishlistItems.Remove(wishlistItem);
                        await _context.SaveChangesAsync();

                        return Json(new
                        {
                            success = true,
                            message = $"Đã chuyển {wishlistItem.Product.ProductName} vào giỏ hàng.",
                            cartCount = data.cartCount,
                            productName = wishlistItem.Product.ProductName
                        });
                    }
                }

                return Json(new { success = false, message = "Không thể thêm sản phẩm vào giỏ hàng." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi chuyển sản phẩm {ProductId} từ danh sách yêu thích vào giỏ hàng của người dùng {UserId}", productId, user.UserID);
                return Json(new { success = false, message = "Lỗi khi chuyển sản phẩm vào giỏ hàng." });
            }
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> ClearWishlist()
        {
            var username = User.Identity.Name;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == username);

            if (user == null)
            {
                return Json(new { success = false, message = "Không tìm thấy thông tin người dùng." });
            }

            try
            {
                var wishlistItems = await _context.WishlistItems
                    .Where(w => w.UserID == user.UserID)
                    .ToListAsync();

                if (wishlistItems.Any())
                {
                    _context.WishlistItems.RemoveRange(wishlistItems);
                    await _context.SaveChangesAsync();

                    return Json(new
                    {
                        success = true,
                        message = "Đã xóa tất cả sản phẩm khỏi danh sách yêu thích."
                    });
                }
                else
                {
                    return Json(new { success = false, message = "Danh sách yêu thích đã trống." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xóa tất cả sản phẩm khỏi danh sách yêu thích của người dùng {UserId}", user.UserID);
                return Json(new { success = false, message = "Lỗi khi xóa tất cả sản phẩm khỏi danh sách yêu thích." });
            }
        }
    }
}
