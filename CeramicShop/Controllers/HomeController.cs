using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CeramicShop.Data;
using CeramicShop.Models;
using CeramicShop.Models.ViewModels; // << इंश्योर THIS IS PRESENT
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace CeramicShop.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<IActionResult> Index(int? categoryId, int? subCategoryId, string sortOrder, decimal? minPrice, decimal? maxPrice, string searchTerm = "")
        {
            _logger.LogInformation("Home/Index called with categoryId: {categoryId}, subCategoryId: {subCategoryId}, sortOrder: {sortOrder}, minPrice: {minPrice}, maxPrice: {maxPrice}, searchTerm: {searchTerm}",
                categoryId, subCategoryId, sortOrder, minPrice, maxPrice, searchTerm);

            try
            {
                var categories = await _context.Categories.AsNoTracking().ToListAsync();
                var allSubCategories = await _context.SubCategories.AsNoTracking().OrderBy(s => s.CategoryID).ToListAsync();

                var subCategoriesByCategory = allSubCategories
                    .GroupBy(s => s.CategoryID)
                    .ToDictionary(g => g.Key, g => g.ToList());

                List<int> userWishlistProductIds = new List<int>();
                if (User.Identity.IsAuthenticated)
                {
                    var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.UserName == User.Identity.Name);
                    if (currentUser != null)
                    {
                        userWishlistProductIds = await _context.WishlistItems
                                                    .Where(w => w.UserID == currentUser.UserID)
                                                    .Select(w => w.ProductID)
                                                    .ToListAsync();
                    }
                }

                // =======================================================================
                // VỊ TRÍ SỬA ĐỔI 1: XÂY DỰNG TRUY VẤN CƠ SỞ VÀ LẤY VIEWMODEL
                // =======================================================================
                IQueryable<Product> baseProductQuery = _context.Products
                                            .Where(p => p.IsActive)
                                            .Include(p => p.SubCategory)
                                                .ThenInclude(s => s.Category)
                                            .Include(p => p.ProductImages);

                // Áp dụng bộ lọc category/subcategory (nếu có) TRƯỚC KHI lấy về ViewModel
                if (categoryId.HasValue)
                {
                    baseProductQuery = baseProductQuery.Where(p => p.SubCategory.CategoryID == categoryId.Value);
                }
                else if (subCategoryId.HasValue)
                {
                    baseProductQuery = baseProductQuery.Where(p => p.SubCategoryID == subCategoryId.Value);
                }
                // Bỏ qua searchTerm cho các section trên trang chủ

                // Lấy danh sách các sản phẩm tiềm năng và chuyển đổi sang ProductViewModel
                // ProductViewModel của bạn cần có thuộc tính DiscountedPrice và EffectivePriceForFilterAndSort
                var allPotentialProductsViewModel = await baseProductQuery
                    .Select(p => new ProductViewModel
                    {
                        ProductID = p.ProductID,
                        ProductName = p.ProductName,
                        Description = p.Description, // Thêm nếu cần cho ViewModel
                        Price = p.Price,             // Giá gốc
                        StockQuantity = p.StockQuantity, // Thêm nếu cần
                        MainImagePath = p.ProductImages.FirstOrDefault(pi => pi.IsMainImage).ImagePath ?? "/Images/no-image.jpg",
                        // AdditionalImages = p.ProductImages.Where(pi => !pi.IsMainImage).Select(pi => pi.ImagePath).ToList(), // Nếu cần
                        CategoryName = p.SubCategory.Category.CategoryName,
                        SubCategoryName = p.SubCategory.SubCategoryName,
                        SubCategoryID = p.SubCategoryID, // Thêm nếu cần
                        DiscountPercentage = _context.Promotions
                            .Where(promo => promo.ProductID == p.ProductID &&
                                            promo.IsActive && DateTime.Now >= promo.StartDate && DateTime.Now <= promo.EndDate)
                            .OrderByDescending(promo => promo.DiscountPercentage)
                            .Select(promo => (decimal?)promo.DiscountPercentage)
                            .FirstOrDefault(),

                        HasSpecificPromotion = _context.Promotions.Any(pr =>
                            pr.ProductID == p.ProductID &&
                            pr.IsActive && pr.StartDate <= DateTime.Now && pr.EndDate >= DateTime.Now),

                        IsInUserWishlist = userWishlistProductIds.Contains(p.ProductID),
                        // Thêm CreatedAt nếu bạn muốn sắp xếp "New Arrivals" chính xác theo ngày tạo
                        CreatedAt = p.CreatedAt
                    })
                    .ToListAsync(); // Lấy dữ liệu về bộ nhớ để tính toán giá hiệu lực


                // =======================================================================
                // VỊ TRÍ SỬA ĐỔI 2: LỌC GIÁ VÀ SẮP XẾP TRÊN VIEWMODEL
                // =======================================================================
                IEnumerable<ProductViewModel> finalFilteredProducts = allPotentialProductsViewModel;

                // Lọc theo giá hiệu lực (DiscountedPrice nếu có, ngược lại là Price)
                if (minPrice.HasValue)
                {
                    // Đảm bảo ProductViewModel có thuộc tính EffectivePriceForFilterAndSort
                    // (get { return DiscountedPrice ?? Price; })
                    finalFilteredProducts = finalFilteredProducts.Where(pvm => pvm.EffectivePriceForFilterAndSort >= minPrice.Value);
                }
                if (maxPrice.HasValue && maxPrice.Value > 0)
                {
                    finalFilteredProducts = finalFilteredProducts.Where(pvm => pvm.EffectivePriceForFilterAndSort <= maxPrice.Value);
                }

                // Sắp xếp dựa trên giá hiệu lực nếu được yêu cầu, hoặc các tiêu chí khác
                switch (sortOrder?.ToLower())
                {
                    case "price_asc":
                        finalFilteredProducts = finalFilteredProducts.OrderBy(pvm => pvm.EffectivePriceForFilterAndSort);
                        break;
                    case "price_desc":
                        finalFilteredProducts = finalFilteredProducts.OrderByDescending(pvm => pvm.EffectivePriceForFilterAndSort);
                        break;
                    case "name_asc":
                        finalFilteredProducts = finalFilteredProducts.OrderBy(pvm => pvm.ProductName);
                        break;
                    case "name_desc":
                        finalFilteredProducts = finalFilteredProducts.OrderByDescending(pvm => pvm.ProductName);
                        break;
                    default:
                        // Nếu bạn đã thêm CreatedAt vào ProductViewModel:
                        // finalFilteredProducts = finalFilteredProducts.OrderByDescending(pvm => pvm.CreatedAt);
                        // Hoặc sắp xếp theo ProductID nếu không có CreatedAt:
                        finalFilteredProducts = finalFilteredProducts.OrderByDescending(pvm => pvm.ProductID);
                        break;
                }

                // Chuyển IEnumerable thành List sau khi đã lọc và sắp xếp
                var productsToShow = finalFilteredProducts.ToList();


                // =======================================================================
                // VỊ TRÍ SỬA ĐỔI 3: CHIA SẢN PHẨM CHO FEATURED VÀ NEW ARRIVALS
                // =======================================================================
                // Logic chia sản phẩm này cần phù hợp với yêu cầu của bạn
                // Ví dụ: Featured có thể là một tập con được đánh dấu riêng hoặc có tiêu chí khác
                // New Arrivals nên được lấy từ 'productsToShow' đã sắp xếp theo tiêu chí mới nhất (ví dụ: ProductID DESC)

                // Tạm thời lấy từ danh sách đã lọc và sắp xếp ở trên
                var featuredProducts = productsToShow.Take(8).ToList();

                // Đối với New Arrivals, nếu bạn muốn chúng thực sự là "mới nhất"
                // và danh sách `productsToShow` không được sắp xếp theo ngày tạo ở bước trước,
                // bạn cần sắp xếp lại hoặc có một logic riêng.
                // Ví dụ, nếu `productsToShow` được sắp xếp theo giá, thì `Take(4)` sẽ không phải là sản phẩm mới nhất.
                // Nếu bạn đã thêm CreatedAt vào ProductViewModel và sắp xếp mặc định theo CreatedAt:
                var newArrivals = productsToShow.Take(4).ToList(); // Nếu mặc định là newest
                // Hoặc nếu sắp xếp mặc định không phải newest, bạn cần sắp xếp lại:
                // var newArrivals = productsToShow.OrderByDescending(pvm => pvm.CreatedAt ?? DateTime.MinValue).Take(4).ToList();


                // =======================================================================
                // VỊ TRÍ SỬA ĐỔI 4: LOGIC XÁC ĐỊNH MIN/MAX PRICE CHO THANH TRƯỢT VIEW
                // =======================================================================
                decimal viewMinPriceForSlider = 0;
                decimal viewMaxPriceForSlider = 10000000;

                // Lấy min/max từ các sản phẩm *trước khi* người dùng áp dụng bộ lọc giá (tức là từ allPotentialProductsViewModel)
                // để thanh trượt luôn phản ánh phạm vi giá của các sản phẩm có thể hiển thị sau khi lọc category/sub.
                if (allPotentialProductsViewModel.Any())
                {
                    viewMinPriceForSlider = allPotentialProductsViewModel.Min(pvm => pvm.EffectivePriceForFilterAndSort);
                    viewMaxPriceForSlider = allPotentialProductsViewModel.Max(pvm => pvm.EffectivePriceForFilterAndSort);
                }
                else if (await _context.Products.AnyAsync()) // Fallback nếu không có sản phẩm nào sau khi lọc category/sub
                {
                    // Cần tính toán lại effective price cho toàn bộ, việc này phức tạp.
                    // Đơn giản nhất là lấy min/max giá gốc từ DB.
                    viewMinPriceForSlider = await _context.Products.MinAsync(p => p.Price);
                    viewMaxPriceForSlider = await _context.Products.MaxAsync(p => p.Price);
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


                ViewBag.CurrentSort = sortOrder;
                ViewBag.CategoryIdHome = categoryId;
                ViewBag.SubCategoryIdHome = subCategoryId;
                ViewBag.SearchTerm = searchTerm;
                ViewBag.MinPriceQuery = minPrice; // Giữ giá trị filter gốc cho các link
                ViewBag.MaxPriceQuery = maxPrice;

                var viewModel = new HomeViewModel
                {
                    Categories = categories,
                    SubCategoriesByCategory = subCategoriesByCategory,
                    FeaturedProducts = featuredProducts,
                    NewArrivals = newArrivals,
                    UserWishlistProductIds = userWishlistProductIds
                };
                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi không xác định trong HomeController.Index");
                return View("Error", new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
            }
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult About()
        {
            return View();
        }

        public IActionResult Contact()
        {
            return View();
        }

        public IActionResult ShippingPolicy()
        {
            return View();
        }

        public IActionResult ReturnPolicy()
        {
            return View();
        }

        public IActionResult Terms()
        {
            return View();
        }

        public IActionResult PrivacyPolicy()
        {
            return View();
        }

        public IActionResult FAQ()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}