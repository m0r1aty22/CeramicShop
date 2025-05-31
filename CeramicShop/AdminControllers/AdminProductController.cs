// CeramicShop/Controllers/AdminProductController.cs

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using CeramicShop.Data; // Hoặc namespace đúng của bạn
using CeramicShop.Models; // Hoặc namespace đúng của bạn
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace CeramicShop.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminProductController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _hostEnvironment;
        private readonly ILogger<AdminProductController> _logger;

        public AdminProductController(ApplicationDbContext context, IWebHostEnvironment hostEnvironment, ILogger<AdminProductController> logger)
        {
            _context = context;
            _hostEnvironment = hostEnvironment;
            _logger = logger;
        }

        // GET: AdminProduct
        public async Task<IActionResult> Index(string searchString, int? categoryId, int? subCategoryId, decimal? minPrice, decimal? maxPrice, bool? isActiveFilter)
        {
            _logger.LogInformation("AdminProduct/Index called. Filters - Search: {Search}, Category: {Category}, SubCategory: {SubCategory}, MinPrice: {MinPrice}, MaxPrice: {MaxPrice}, IsActive: {IsActive}",
                searchString, categoryId, subCategoryId, minPrice, maxPrice, isActiveFilter);

            var productsQuery = _context.Products
                .Include(p => p.SubCategory)
                    .ThenInclude(s => s.Category)
                .Include(p => p.ProductImages)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                productsQuery = productsQuery.Where(p => p.ProductName.Contains(searchString) || (p.Description != null && p.Description.Contains(searchString)));
            }
            if (categoryId.HasValue)
            {
                productsQuery = productsQuery.Where(p => p.SubCategory.CategoryID == categoryId.Value);
            }
            if (subCategoryId.HasValue)
            {
                // Product.SubCategoryID là int (non-nullable)
                productsQuery = productsQuery.Where(p => p.SubCategoryID == subCategoryId.Value);
            }
            if (minPrice.HasValue)
            {
                productsQuery = productsQuery.Where(p => p.Price >= minPrice.Value);
            }
            if (maxPrice.HasValue)
            {
                productsQuery = productsQuery.Where(p => p.Price <= maxPrice.Value);
            }
            if (isActiveFilter.HasValue)
            {
                // Product.IsActive bây giờ là bool (non-nullable)
                // isActiveFilter vẫn là bool? (nullable)
                productsQuery = productsQuery.Where(p => p.IsActive == isActiveFilter.Value);
            }

            // Category.IsActive bây giờ là bool (non-nullable)
            ViewBag.Categories = await _context.Categories
                                            .Where(c => c.IsActive) // Không cần "== true" nữa
                                            .OrderBy(c => c.CategoryName)
                                            .ToListAsync();
            // SubCategory.IsActive bây giờ là bool (non-nullable)
            ViewBag.SubCategories = await _context.SubCategories
                                            .Where(s => s.IsActive) // Không cần "== true" nữa
                                            .OrderBy(s => s.SubCategoryName)
                                            .ToListAsync();

            ViewBag.SearchString = searchString;
            ViewBag.CategoryId = categoryId;
            ViewBag.SubCategoryId = subCategoryId;
            ViewBag.MinPrice = minPrice;
            ViewBag.MaxPrice = maxPrice;
            ViewBag.IsActiveFilter = isActiveFilter; // Giữ nguyên là bool? để View biết người dùng có chọn filter không

            // Product.CreatedAt bây giờ là DateTime (non-nullable)
            var result = await productsQuery
                                .OrderByDescending(p => p.CreatedAt) // Sắp xếp trực tiếp, không cần .HasValue
                                .ToListAsync();

            return View(result);
        }

        // GET: AdminProduct/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }
            var product = await _context.Products
                .Include(p => p.SubCategory)
                    .ThenInclude(s => s.Category)
                .Include(p => p.ProductImages)
                .FirstOrDefaultAsync(m => m.ProductID == id);
            if (product == null)
            {
                return NotFound();
            }
            return View(product);
        }

        // Ví dụ trong Create (GET):
        public async Task<IActionResult> Create()
        {
            ViewBag.Categories = await _context.Categories
                                        .Where(c => c.IsActive) // Sửa ở đây
                                        .OrderBy(c => c.CategoryName)
                                        .ToListAsync();
            ViewBag.SubCategories = new SelectList(Enumerable.Empty<SubCategory>(), "SubCategoryID", "SubCategoryName");
            var model = new Product();
            return View(model);
        }

        // POST: AdminProduct/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ProductName,Description,Price,StockQuantity,SubCategoryID,IsActive")] Product product, List<IFormFile> productImages)
        {
            // 1. Kiểm tra ràng buộc SubCategoryID (nếu cần)
            if (product.SubCategoryID == 0) // Giả sử 0 nghĩa là chưa chọn
            {
                ModelState.AddModelError("SubCategoryID", "Vui lòng chọn danh mục con.");
            }


            if (ModelState.IsValid)
            {
                product.CreatedAt = DateTime.Now;
                product.UpdatedAt = DateTime.Now;

                product.Price = Math.Round(product.Price / 1000) * 1000;
                _context.Add(product);
                await _context.SaveChangesAsync(); 

                bool imageUploadErrorOccurred = false;
                if (productImages != null && productImages.Count > 0 && productImages.Any(f => f != null && f.Length > 0))
                {
                    string uploadsFolder = Path.Combine(_hostEnvironment.WebRootPath, "Images");
                    if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                    for (int i = 0; i < productImages.Count; i++)
                    {
                        var file = productImages[i];
                        if (file != null && file.Length > 0)
                        {
                            try
                            {
                                // Tạo tên file duy nhất và an toàn
                                string originalFileName = Path.GetFileNameWithoutExtension(file.FileName);
                                string fileExtension = Path.GetExtension(file.FileName);
                                string safeOriginalFileName = System.Text.RegularExpressions.Regex.Replace(originalFileName, @"[^A-Za-z0-9_.-]", "");
                                if (string.IsNullOrWhiteSpace(safeOriginalFileName)) safeOriginalFileName = "image";
                                string fileName = $"{safeOriginalFileName}_{Guid.NewGuid().ToString().Substring(0, 8)}{fileExtension}".Replace(" ", "_");

                                string filePath = Path.Combine(uploadsFolder, fileName);
                                using (var fileStream = new FileStream(filePath, FileMode.Create))
                                {
                                    await file.CopyToAsync(fileStream);
                                }
                                var productImage = new ProductImage
                                {
                                    ProductID = product.ProductID,
                                    ImagePath = fileName,
                                    IsMainImage = (i == 0) 
                                };
                                _context.ProductImages.Add(productImage);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, $"Lỗi khi tải lên ảnh: {file.FileName} cho sản phẩm mới.");
                                ModelState.AddModelError("productImages", $"Lỗi khi tải lên ảnh '{file.FileName}'. Vui lòng thử lại.");
                                imageUploadErrorOccurred = true;
                            }
                        }
                    }

                    if (!imageUploadErrorOccurred)
                    {
                        await _context.SaveChangesAsync(); // Lưu các ProductImage
                        TempData["SuccessMessage"] = "Sản phẩm đã được tạo thành công.";
                        return RedirectToAction(nameof(Index));
                    }
                }
                else
                {
                    if (ModelState.ErrorCount == 0)
                    {
                        TempData["SuccessMessage"] = "Sản phẩm đã được tạo thành công (không có hình ảnh).";
                        return RedirectToAction(nameof(Index));
                    }
                }
            }

            // ---- XỬ LÝ KHI MODELSTATE KHÔNG HỢP LỆ (hoặc lỗi upload ảnh làm ModelState không hợp lệ) ----
            _logger.LogWarning("ModelState không hợp lệ khi tạo sản phẩm. Lỗi: {Errors}",
                string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));

            // 2. Chuẩn bị lại ViewBag.Categories
            ViewBag.Categories = await _context.Categories
                                        .Where(c => c.IsActive) // Model Category.IsActive là bool
                                        .OrderBy(c => c.CategoryName)
                                        .ToListAsync();

            // 3. Chuẩn bị lại ViewBag.SubCategories và ViewBag.SelectedCategoryID
            if (product.SubCategoryID != 0) // Nếu người dùng đã chọn một SubCategory
            {
                var selectedSubCategory = await _context.SubCategories
                                                .AsNoTracking() // Không cần theo dõi entity này
                                                .Include(s => s.Category) // Quan trọng: Nạp Category để có CategoryID
                                                .FirstOrDefaultAsync(s => s.SubCategoryID == product.SubCategoryID);
                if (selectedSubCategory != null)
                {
                    // Lưu CategoryID của SubCategory đã chọn để select lại dropdown Category cha
                    ViewBag.SelectedCategoryID = selectedSubCategory.CategoryID;

                    // Tải lại danh sách SubCategory cho Category cha đó
                    var subCategoriesForSelectedCategory = await _context.SubCategories
                                                                .Where(s => s.CategoryID == selectedSubCategory.CategoryID && s.IsActive) // Model SubCategory.IsActive là bool
                                                                .OrderBy(s => s.SubCategoryName)
                                                                .ToListAsync();
                    ViewBag.SubCategories = new SelectList(subCategoriesForSelectedCategory, "SubCategoryID", "SubCategoryName", product.SubCategoryID);
                }
                else
                {
                    // SubCategoryID được gửi lên không tồn tại (ít khi xảy ra nếu không can thiệp client-side)
                    _logger.LogWarning($"Khi ModelState không hợp lệ, SubCategoryID '{product.SubCategoryID}' trong model không tìm thấy SubCategory tương ứng trong DB.");
                    ViewBag.SelectedCategoryID = null; // Không xác định được Category cha
                    ViewBag.SubCategories = new SelectList(Enumerable.Empty<SubCategory>(), "SubCategoryID", "SubCategoryName");
                }
            }
            else // Nếu người dùng chưa chọn SubCategory (product.SubCategoryID là 0)
            {
                ViewBag.SelectedCategoryID = null; // Không có Category cha nào được chọn
                ViewBag.SubCategories = new SelectList(Enumerable.Empty<SubCategory>(), "SubCategoryID", "SubCategoryName");
            }

            return View(product);
        }

        // GET: AdminProduct/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                _logger.LogWarning("Edit GET: id null");
                return NotFound();
            }

            var product = await _context.Products
                .Include(p => p.ProductImages)
                .Include(p => p.SubCategory)
                    .ThenInclude(s => s.Category)
                .FirstOrDefaultAsync(p => p.ProductID == id);

            if (product == null)
            {
                _logger.LogWarning("Edit GET: Không tìm thấy sản phẩm ID {0}", id);
                return NotFound();
            }

            if (product != null)
            {
                // Làm tròn giá trị Price về số nguyên gần nhất là bội số của 1000
                // Hoặc bạn có thể quyết định làm tròn lên hay xuống tùy theo logic nghiệp vụ
                product.Price = Math.Round(product.Price / 1000) * 1000;
                // Hoặc nếu bạn muốn luôn bỏ phần thập phân và đảm bảo là số nguyên:
                // product.Price = Math.Floor(product.Price);
            }

            ViewBag.Categories = await _context.Categories
                .Where(c => c.IsActive)
                .OrderBy(c => c.CategoryName)
                .ToListAsync();

            if (product.SubCategory != null)
            {
                var categoryId = product.SubCategory.CategoryID;
                ViewBag.SelectedCategoryID = categoryId;

                var subCategories = await _context.SubCategories
                    .Where(s => s.CategoryID == categoryId && s.IsActive)
                    .OrderBy(s => s.SubCategoryName)
                    .ToListAsync();

                ViewBag.SubCategories = new SelectList(subCategories, "SubCategoryID", "SubCategoryName", product.SubCategoryID);
            }
            else
            {
                ViewBag.SelectedCategoryID = null;
                ViewBag.SubCategories = new SelectList(Enumerable.Empty<SubCategory>(), "SubCategoryID", "SubCategoryName");
            }

            return View(product);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
    int id,
    [Bind("ProductID,ProductName,Description,Price,StockQuantity,SubCategoryID,IsActive")]
    Product product,
    List<IFormFile> newImages,
    List<int> imagesToDelete,
    int? mainImageIdCurrent)
        {
            if (id != product.ProductID) return BadRequest();

            // 1) Tải sản phẩm + images
            var existing = await _context.Products
                .Include(p => p.ProductImages)
                .FirstOrDefaultAsync(p => p.ProductID == id);
            if (existing == null) return NotFound();

            // 2) Cập nhật thông tin cơ bản
            existing.ProductName = product.ProductName;
            existing.Description = product.Description;
            existing.Price = product.Price;
            existing.StockQuantity = product.StockQuantity;
            existing.SubCategoryID = product.SubCategoryID;
            existing.IsActive = product.IsActive;

            // 3) Xoá ảnh nếu có
            if (imagesToDelete != null && imagesToDelete.Any())
            {
                foreach (var imgId in imagesToDelete)
                {
                    var img = existing.ProductImages.FirstOrDefault(pi => pi.ProductImageID == imgId);
                    if (img != null)
                    {
                        var path = Path.Combine(_hostEnvironment.WebRootPath, "Images", img.ImagePath);
                        if (System.IO.File.Exists(path))
                            System.IO.File.Delete(path);
                        _context.ProductImages.Remove(img);
                    }
                }
            }

            // 4) Thêm ảnh mới nếu có
            if (newImages != null && newImages.Any())
            {
                var uploads = Path.Combine(_hostEnvironment.WebRootPath, "Images");
                Directory.CreateDirectory(uploads);
                foreach (var file in newImages.Where(f => f.Length > 0))
                {
                    var baseName = Path.GetFileNameWithoutExtension(file.FileName);
                    var ext = Path.GetExtension(file.FileName);
                    var finalName = $"{Guid.NewGuid():N}_{baseName}{ext}";
                    var savePath = Path.Combine(uploads, finalName);
                    using var stream = new FileStream(savePath, FileMode.Create);
                    await file.CopyToAsync(stream);

                    existing.ProductImages.Add(new ProductImage
                    {
                        ImagePath = finalName,
                        IsMainImage = false,      // set ảnh chính bên dưới
                        ProductID = existing.ProductID
                    });
                }
            }

            // 5) Cập nhật ảnh chính
            if (mainImageIdCurrent.HasValue && mainImageIdCurrent.Value > 0)
            {
                foreach (var img in existing.ProductImages)
                    img.IsMainImage = (img.ProductImageID == mainImageIdCurrent.Value);
            }
            else
            {
                // Nếu không chọn, đặt image đầu tiên làm chính
                var first = existing.ProductImages.OrderBy(pi => pi.ProductImageID).FirstOrDefault();
                if (first != null)
                    first.IsMainImage = true;
            }

            // 6) Lưu tất cả thay đổi
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Cập nhật sản phẩm thành công!";
            return RedirectToAction(nameof(Index));
        }




        // GET: AdminProduct/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var product = await _context.Products
                .Include(p => p.SubCategory).ThenInclude(s => s.Category)
                .Include(p => p.ProductImages)
                .FirstOrDefaultAsync(m => m.ProductID == id);
            if (product == null) return NotFound();
            return View(product);
        }

        // POST: AdminProduct/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var isInOrder = await _context.OrderDetails.AnyAsync(od => od.ProductID == id);
            if (isInOrder)
            {
                TempData["ErrorMessage"] = "Không thể xóa sản phẩm vì đã có trong đơn hàng. Hãy đánh dấu là không hoạt động.";
                return RedirectToAction(nameof(Index));
            }
            var product = await _context.Products.Include(p => p.ProductImages).FirstOrDefaultAsync(p => p.ProductID == id);
            if (product != null)
            {
                foreach (var image in product.ProductImages)
                {
                    try
                    {
                        var imagePath = Path.Combine(_hostEnvironment.WebRootPath, image.ImagePath.TrimStart('/'));
                        if (System.IO.File.Exists(imagePath)) System.IO.File.Delete(imagePath);
                    }
                    catch (Exception ex) { _logger.LogError(ex, $"Lỗi xóa file ảnh: {image.ImagePath}"); }
                }
                _context.ProductImages.RemoveRange(product.ProductImages);

                var reviews = await _context.Reviews.Where(r => r.ProductID == id).ToListAsync();
                if (reviews.Any()) _context.Reviews.RemoveRange(reviews);
                var promotions = await _context.Promotions.Where(promo => promo.ProductID == id).ToListAsync();
                if (promotions.Any()) _context.Promotions.RemoveRange(promotions);
                var wishlistItems = await _context.WishlistItems.Where(w => w.ProductID == id).ToListAsync();
                if (wishlistItems.Any()) _context.WishlistItems.RemoveRange(wishlistItems);

                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Sản phẩm '{product.ProductName}' đã xóa.";
            }
            else
            {
                TempData["ErrorMessage"] = "Không tìm thấy sản phẩm để xóa.";
            }
            return RedirectToAction(nameof(Index));
        }

        // POST: AdminProduct/SetMainImage/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetMainImage(int id, int imageId) // id là ProductID
        {
            var productImages = await _context.ProductImages.Where(pi => pi.ProductID == id).ToListAsync();
            if (!productImages.Any())
            {
                TempData["ErrorMessage"] = "Sản phẩm không có hình ảnh nào để đặt làm ảnh chính.";
                return RedirectToAction(nameof(Edit), new { id = id });
            }

            bool imageFoundAndSetAsMain = false;
            foreach (var image in productImages)
            {
                image.IsMainImage = (image.ProductImageID == imageId);
                if (image.IsMainImage) imageFoundAndSetAsMain = true;
            }

            // Nếu imageId được cung cấp không hợp lệ hoặc không tìm thấy trong danh sách ảnh của sản phẩm,
            // và sản phẩm có ảnh, thì đặt ảnh đầu tiên làm ảnh chính để đảm bảo luôn có 1 ảnh chính.
            if (!imageFoundAndSetAsMain && productImages.Any())
            {
                // Kiểm tra xem imageId có tồn tại trong danh sách ảnh của sản phẩm không
                var targetImage = productImages.FirstOrDefault(pi => pi.ProductImageID == imageId);
                if (targetImage != null)
                {
                    targetImage.IsMainImage = true; // Đặt ảnh được chỉ định làm chính
                }
                else
                {
                    productImages.First().IsMainImage = true; // Nếu imageId không hợp lệ, đặt ảnh đầu tiên làm chính
                    TempData["WarningMessage"] = "Ảnh được chọn không hợp lệ, ảnh đầu tiên đã được đặt làm ảnh chính.";
                }
            }

            await _context.SaveChangesAsync();
            if (!TempData.ContainsKey("WarningMessage")) // Chỉ hiển thị success nếu không có warning
            {
                TempData["SuccessMessage"] = "Đã cập nhật ảnh chính.";
            }
            return RedirectToAction(nameof(Edit), new { id = id });
        }

        // Tương tự cho GetSubCategories (AJAX):
        [HttpGet]
        public async Task<JsonResult> GetSubCategories(int categoryId)
        {
            // SubCategory.IsActive là bool
            var subCategories = await _context.SubCategories
                .Where(s => s.CategoryID == categoryId && s.IsActive) // Không cần "== true"
                .OrderBy(s => s.SubCategoryName)
                .Select(s => new { id = s.SubCategoryID, name = s.SubCategoryName })
                .ToListAsync();
            return Json(subCategories);
        }
        private bool ProductExists(int id)
        {
            return _context.Products.Any(e => e.ProductID == id);
        }
    }
}