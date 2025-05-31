using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CeramicShop.Data;
using CeramicShop.Models;
using CeramicShop.Models.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace CeramicShop.Controllers
{
    public class ProductController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ProductController> _logger;

        public ProductController(ApplicationDbContext context, ILogger<ProductController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> Index(int? categoryId, int? subCategoryId, string searchTerm, string sortOrder, decimal? minPrice, decimal? maxPrice, int page = 1, string viewMode = "grid")
        {
            _logger.LogInformation("Product/Index called with categoryId: {categoryId}, subCategoryId: {subCategoryId}, searchTerm: {searchTerm}, sortOrder: {sortOrder}, minPrice: {minPrice}, maxPrice: {maxPrice}, page: {page}, viewMode: {viewMode}",
               categoryId, subCategoryId, searchTerm, sortOrder, minPrice, maxPrice, page, viewMode);

            int pageSize = 12;

            IQueryable<Product> baseProductQuery = _context.Products
                                       .Where(p => p.IsActive)
                                       .Include(p => p.SubCategory)
                                           .ThenInclude(s => s.Category)
                                       .Include(p => p.ProductImages);
            // Không .AsQueryable() ở đây nữa vì sẽ Select vào ViewModel

            if (categoryId.HasValue)
            {
                baseProductQuery = baseProductQuery.Where(p => p.SubCategory.CategoryID == categoryId.Value);
            }
            // ViewBag.CategoryId = categoryId; // Sẽ gán sau

            if (subCategoryId.HasValue)
            {
                baseProductQuery = baseProductQuery.Where(p => p.SubCategoryID == subCategoryId.Value);
            }
            // ViewBag.SubCategoryId = subCategoryId; // Sẽ gán sau

            if (!string.IsNullOrEmpty(searchTerm))
            {
                baseProductQuery = baseProductQuery.Where(p => p.ProductName.Contains(searchTerm) || (p.Description != null && p.Description.Contains(searchTerm)));
            }
            // ViewBag.SearchTerm = searchTerm; // Sẽ gán sau


            // Lấy UserID để kiểm tra Wishlist
            List<int> userWishlistProductIds = new List<int>();
            if (User.Identity.IsAuthenticated)
            {
                var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (int.TryParse(userIdString, out int currentUserId))
                {
                    userWishlistProductIds = await _context.WishlistItems
                                                .Where(w => w.UserID == currentUserId)
                                                .Select(w => w.ProductID)
                                                .ToListAsync();
                }
            }

            var allPotentialProductsViewModel = await baseProductQuery
                .Select(p => new ProductViewModel
                {
                    ProductID = p.ProductID,
                    ProductName = p.ProductName,
                    Description = p.Description,
                    Price = p.Price, // Giá gốc
                    StockQuantity = p.StockQuantity, // Thêm nếu cần
                    MainImagePath = p.ProductImages.FirstOrDefault(pi => pi.IsMainImage).ImagePath ?? "/Images/no-image.jpg",
                    CategoryName = p.SubCategory.Category.CategoryName,
                    SubCategoryName = p.SubCategory.SubCategoryName,
                    SubCategoryID = p.SubCategoryID,
                    // Thêm CreatedAt nếu bạn muốn sắp xếp theo ngày tạo
                    // CreatedAt = p.CreatedAt,
                    DiscountPercentage = _context.Promotions
                        .Where(promo => promo.ProductID == p.ProductID &&
                                        promo.IsActive && DateTime.Now >= promo.StartDate && DateTime.Now <= promo.EndDate)
                        .OrderByDescending(promo => promo.DiscountPercentage)
                        .Select(promo => (decimal?)promo.DiscountPercentage)
                        .FirstOrDefault(),

                    HasSpecificPromotion = _context.Promotions.Any(pr =>
                        pr.ProductID == p.ProductID &&
                        pr.IsActive && pr.StartDate <= DateTime.Now && pr.EndDate >= DateTime.Now),
                    IsInUserWishlist = userWishlistProductIds.Contains(p.ProductID)
                    // DiscountedPrice và EffectivePriceForFilterAndSort sẽ được tính tự động trong ProductViewModel
                })
                .ToListAsync(); // Lấy về bộ nhớ để lọc và sắp xếp giá hiệu lực

            IEnumerable<ProductViewModel> filteredProductsViewModel = allPotentialProductsViewModel;

            // Lọc theo giá hiệu lực
            if (minPrice.HasValue)
            {
                filteredProductsViewModel = filteredProductsViewModel.Where(pvm => pvm.EffectivePriceForFilterAndSort >= minPrice.Value);
            }
            if (maxPrice.HasValue && maxPrice.Value > 0) // Giữ lại kiểm tra maxPrice.Value <= 10000000 nếu cần
            {
                filteredProductsViewModel = filteredProductsViewModel.Where(pvm => pvm.EffectivePriceForFilterAndSort <= maxPrice.Value);
            }

            // Sắp xếp
            switch (sortOrder?.ToLower())
            {
                case "price_asc":
                    filteredProductsViewModel = filteredProductsViewModel.OrderBy(pvm => pvm.EffectivePriceForFilterAndSort)
                                                                       .ThenByDescending(pvm => pvm.ProductID); // Thêm sắp xếp phụ
                    break;
                case "price_desc":
                    filteredProductsViewModel = filteredProductsViewModel.OrderByDescending(pvm => pvm.EffectivePriceForFilterAndSort)
                                                                         .ThenByDescending(pvm => pvm.ProductID);
                    break;
                case "name_asc":
                    filteredProductsViewModel = filteredProductsViewModel.OrderBy(pvm => pvm.ProductName)
                                                                         .ThenByDescending(pvm => pvm.ProductID);
                    break;
                case "name_desc":
                    filteredProductsViewModel = filteredProductsViewModel.OrderByDescending(pvm => pvm.ProductName)
                                                                           .ThenByDescending(pvm => pvm.ProductID);
                    break;
                default: // Mặc định sắp xếp theo ProductID giảm dần (tương đương mới nhất nếu ID tăng dần)
                         // Hoặc theo CreatedAt nếu bạn thêm nó vào ViewModel
                    filteredProductsViewModel = filteredProductsViewModel.OrderByDescending(pvm => pvm.ProductID);
                    break;
            }

            var totalItems = filteredProductsViewModel.Count(); // Đếm sau khi đã lọc
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            if (page < 1) page = 1;
            if (page > totalPages && totalPages > 0) page = totalPages;

            var productList = filteredProductsViewModel
                                .Skip((page - 1) * pageSize)
                                .Take(pageSize)
                                .ToList();

            decimal viewMinPriceForSlider = 0;
            decimal viewMaxPriceForSlider = 10000000;

            // Lấy min/max từ các sản phẩm *trước khi* người dùng áp dụng bộ lọc giá (tức là từ allPotentialProductsViewModel)
            // để thanh trượt luôn phản ánh phạm vi giá của các sản phẩm có thể hiển thị sau khi lọc category/sub/searchTerm.
            if (allPotentialProductsViewModel.Any())
            {
                viewMinPriceForSlider = allPotentialProductsViewModel.Min(pvm => pvm.EffectivePriceForFilterAndSort);
                viewMaxPriceForSlider = allPotentialProductsViewModel.Max(pvm => pvm.EffectivePriceForFilterAndSort);
            }
            else if (await _context.Products.AnyAsync()) // Fallback nếu không có sản phẩm nào sau khi lọc cơ bản
            {
                // Tạm thời lấy giá gốc từ DB nếu không có sản phẩm nào trong allPotentialProductsViewModel
                var allDbProductsPrices = await _context.Products.Select(p => p.Price).ToListAsync();
                if (allDbProductsPrices.Any())
                {
                    viewMinPriceForSlider = allDbProductsPrices.Min();
                    viewMaxPriceForSlider = allDbProductsPrices.Max();
                }
            }

            // Giá trị sẽ hiển thị trong input, ưu tiên giá trị người dùng đã nhập
            decimal currentMinPriceToDisplay = minPrice ?? viewMinPriceForSlider;
            decimal currentMaxPriceToDisplay = maxPrice ?? viewMaxPriceForSlider;

            // Làm tròn để hiển thị nếu là số nguyên (bỏ .00)
            if (currentMinPriceToDisplay == Math.Floor(currentMinPriceToDisplay))
                ViewBag.MinPrice = (long)currentMinPriceToDisplay;
            else
                ViewBag.MinPrice = currentMinPriceToDisplay;

            if (currentMaxPriceToDisplay == Math.Floor(currentMaxPriceToDisplay))
                ViewBag.MaxPrice = (long)currentMaxPriceToDisplay;
            else
                ViewBag.MaxPrice = currentMaxPriceToDisplay;


            // Gán các giá trị ViewBag khác
            ViewBag.CategoryId = categoryId;
            ViewBag.SubCategoryId = subCategoryId;
            ViewBag.SearchTerm = searchTerm;
            ViewBag.CurrentSort = sortOrder;
            ViewBag.MinPriceQuery = minPrice; // Giữ giá trị filter gốc cho các link/form
            ViewBag.MaxPriceQuery = maxPrice;

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.Categories = await _context.Categories.AsNoTracking().ToListAsync();
            ViewBag.SubCategories = await _context.SubCategories.AsNoTracking().OrderBy(s => s.CategoryID).ToListAsync(); // Có thể tối ưu chỉ lấy subcat của cat hiện tại
            ViewBag.ViewMode = string.IsNullOrEmpty(viewMode) ? "grid" : viewMode.ToLower();

            return View(productList);
        }

        public async Task<IActionResult> Details(int id)
        {
            var product = await _context.Products
                .AsNoTracking()
                .Include(p => p.SubCategory)
                    .ThenInclude(s => s.Category)
                .Include(p => p.ProductImages)
                .FirstOrDefaultAsync(p => p.ProductID == id && p.IsActive);

            if (product == null)
            {
                return NotFound();
            }

            bool isInUserWishlist = false;
            if (User.Identity.IsAuthenticated)
            {
                var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (int.TryParse(userIdString, out int currentUserId))
                {
                    isInUserWishlist = await _context.WishlistItems
                                            .AnyAsync(w => w.UserID == currentUserId && w.ProductID == id);
                }
            }

            var viewModel = new ProductViewModel
            {
                ProductID = product.ProductID,
                ProductName = product.ProductName,
                Description = product.Description,
                Price = product.Price,
                StockQuantity = product.StockQuantity,
                SubCategoryID = product.SubCategoryID,
                SubCategoryName = product.SubCategory?.SubCategoryName,
                CategoryName = product.SubCategory?.Category?.CategoryName,
                MainImagePath = product.ProductImages.FirstOrDefault(i => i.IsMainImage)?.ImagePath ?? "/Images/no-image.jpg",
                AdditionalImages = product.ProductImages.Where(i => !i.IsMainImage).Select(i => i.ImagePath).ToList(),
                Features = new List<string>
                {
                    "Sản phẩm chính hãng", "Bảo hành 12 tháng", "Giao hàng miễn phí", "Đổi trả trong 7 ngày"
                },
                Specifications = new Dictionary<string, string>
                {
                    { "Thương hiệu", "CeramicShop Brand" }, { "Xuất xứ", "Việt Nam" }, { "Bảo hành", "12 tháng" }
                },
                IsInUserWishlist = isInUserWishlist
            };

            var promotion = await _context.Promotions
                .AsNoTracking()
                .Where(p => (p.ProductID == id || p.ProductID == null) &&
                           p.IsActive && DateTime.Now >= p.StartDate && DateTime.Now <= p.EndDate)
                .OrderByDescending(p => p.DiscountPercentage)
                .Select(p => p.DiscountPercentage)
                .FirstOrDefaultAsync();

            if (promotion > 0)
            {
                viewModel.DiscountPercentage = promotion;
            }

            viewModel.Reviews = await _context.Reviews
                .AsNoTracking()
                .Where(r => r.ProductID == id)
                .OrderByDescending(r => r.CreatedAt)
                .Join(_context.Users.AsNoTracking(),
                    r => r.UserID,
                    u => u.UserID,
                    (r, u) => new ReviewViewModel
                    {
                        ReviewID = r.ReviewID,
                        UserName = u.FullName,
                        Rating = r.Rating,
                        Comment = r.Comment,
                        CreatedAt = r.CreatedAt
                    })
                .ToListAsync();

            ViewBag.RelatedProducts = await _context.Products
                .AsNoTracking()
                .Where(p => p.SubCategoryID == product.SubCategoryID && p.ProductID != id && p.IsActive)
                .OrderByDescending(p => p.CreatedAt)
                .Take(4)
                .Select(p => new ProductViewModel
                {
                    ProductID = p.ProductID,
                    ProductName = p.ProductName,
                    Price = p.Price,
                    MainImagePath = p.ProductImages.FirstOrDefault(i => i.IsMainImage).ImagePath ?? "/Images/no-image.jpg",
                    DiscountPercentage = _context.Promotions
                                           .Where(promo => (promo.ProductID == p.ProductID || promo.ProductID == null) &&
                                                           promo.IsActive && DateTime.Now >= promo.StartDate && DateTime.Now <= promo.EndDate)
                                           .OrderByDescending(promo => promo.DiscountPercentage)
                                           .Select(promo => (decimal?)promo.DiscountPercentage)
                                           .FirstOrDefault(),
                    IsInUserWishlist = User.Identity.IsAuthenticated ?
                               _context.WishlistItems.Any(w => w.UserID == int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)) && w.ProductID == p.ProductID) :
                               false
                })
                .ToListAsync();

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddReview(int productId, int rating, string comment)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return Json(new { success = false, message = "Bạn cần đăng nhập để đánh giá sản phẩm." });
            }

            if (rating < 1 || rating > 5)
            {
                return Json(new { success = false, message = "Đánh giá không hợp lệ." });
            }

            if (string.IsNullOrWhiteSpace(comment))
            {
                return Json(new { success = false, message = "Vui lòng nhập nội dung đánh giá." });
            }

            // Kiểm tra sản phẩm có tồn tại không
            var product = await _context.Products.FindAsync(productId);
            if (product == null || !product.IsActive)
            {
                return Json(new { success = false, message = "Sản phẩm không tồn tại hoặc đã bị vô hiệu hóa." });
            }

            // Lấy UserID từ Claims
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
            {
                return Json(new { success = false, message = "Không thể xác định người dùng." });
            }

            // Kiểm tra xem người dùng đã đánh giá sản phẩm này chưa
            var existingReview = await _context.Reviews
                .FirstOrDefaultAsync(r => r.ProductID == productId && r.UserID == userId);

            try
            {
                if (existingReview != null)
                {
                    // Cập nhật đánh giá hiện có
                    existingReview.Rating = rating;
                    existingReview.Comment = comment;
                    existingReview.CreatedAt = DateTime.Now;
                    _context.Entry(existingReview).State = EntityState.Modified;
                }
                else
                {
                    // Tạo đánh giá mới
                    var review = new Review
                    {
                        ProductID = productId,
                        UserID = userId,
                        Rating = rating,
                        Comment = comment,
                        CreatedAt = DateTime.Now
                    };
                    _context.Reviews.Add(review);
                }

                await _context.SaveChangesAsync();

                // Lấy danh sách đánh giá mới nhất
                var reviews = await _context.Reviews
                    .AsNoTracking()
                    .Where(r => r.ProductID == productId)
                    .OrderByDescending(r => r.CreatedAt)
                    .Join(_context.Users.AsNoTracking(),
                        r => r.UserID,
                        u => u.UserID,
                        (r, u) => new ReviewViewModel
                        {
                            ReviewID = r.ReviewID,
                            UserName = u.FullName,
                            Rating = r.Rating,
                            Comment = r.Comment,
                            CreatedAt = r.CreatedAt
                        })
                    .ToListAsync();

                // Tính trung bình đánh giá
                var averageRating = await _context.Reviews
                    .Where(r => r.ProductID == productId)
                    .AverageAsync(r => (decimal)r.Rating);

                return Json(new
                {
                    success = true,
                    message = existingReview != null ? "Đánh giá của bạn đã được cập nhật thành công." : "Đánh giá của bạn đã được gửi thành công.",
                    reviews = reviews,
                    averageRating = Math.Round(averageRating, 1)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding/updating review for product {ProductId} by user {UserId}", productId, userId);
                return Json(new { success = false, message = "Lỗi khi gửi đánh giá: " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetReviews(int productId)
        {
            var reviews = await _context.Reviews
                .AsNoTracking()
                .Where(r => r.ProductID == productId)
                .OrderByDescending(r => r.CreatedAt)
                .Join(_context.Users.AsNoTracking(),
                    r => r.UserID,
                    u => u.UserID,
                    (r, u) => new ReviewViewModel
                    {
                        ReviewID = r.ReviewID,
                        UserName = u.FullName,
                        Rating = r.Rating,
                        Comment = r.Comment,
                        CreatedAt = r.CreatedAt
                    })
                .ToListAsync();

            // Tính trung bình đánh giá
            decimal averageRating = 0;
            if (reviews.Any())
            {
                averageRating = reviews.Average(r => (decimal)r.Rating);
            }

            ViewBag.AverageRating = Math.Round(averageRating, 1);
            ViewBag.ReviewCount = reviews.Count;

            return PartialView("_ReviewsPartial", reviews);
        }
    }
}
