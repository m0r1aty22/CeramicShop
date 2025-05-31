using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
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
    public class AdminPromotionController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminPromotionController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: AdminPromotion
        public async Task<IActionResult> Index(string searchString, string isActive, DateTime? startDateFilter, DateTime? endDateFilter)
        {
            var promotions = _context.Promotions
                .Include(p => p.Product)
                .AsQueryable();

            // Lọc theo từ khóa tìm kiếm
            if (!string.IsNullOrEmpty(searchString))
            {
                promotions = promotions.Where(p => p.PromotionName.Contains(searchString) ||
                                                 p.Description.Contains(searchString) ||
                                                 p.PromoCode.Contains(searchString) ||
                                                 (p.Product != null && p.Product.ProductName.Contains(searchString)));
            }

            // Lọc theo trạng thái
            bool? isActiveBool = null;
            if (isActive == "true") isActiveBool = true;
            else if (isActive == "false") isActiveBool = false;

            if (isActiveBool.HasValue)
            {
                promotions = promotions.Where(p => p.IsActive == isActiveBool.Value);
            }

            if (startDateFilter.HasValue)
            {
                promotions = promotions.Where(p => p.StartDate >= startDateFilter.Value || p.EndDate >= startDateFilter.Value);
            }

            if (endDateFilter.HasValue)
            {
                var endOfDayFilter = endDateFilter.Value.Date.AddDays(1).AddTicks(-1);
                promotions = promotions.Where(p => p.EndDate <= endDateFilter || p.StartDate <= endOfDayFilter);
            }

            if (startDateFilter.HasValue && endDateFilter.HasValue)
            {
                var endOfDayForEndDateFilter = endDateFilter.Value.Date.AddDays(1).AddTicks(-1);
                promotions = promotions.Where(p => p.StartDate <= endOfDayForEndDateFilter && p.EndDate >= startDateFilter.Value);
            }
            else if (startDateFilter.HasValue) // Chỉ có ngày bắt đầu lọc
            {
                promotions = promotions.Where(p => p.EndDate >= startDateFilter.Value);
            }
            else if (endDateFilter.HasValue) // Chỉ có ngày kết thúc lọc
            {
                var endOfDayForEndDateFilter = endDateFilter.Value.Date.AddDays(1).AddTicks(-1);
                promotions = promotions.Where(p => p.StartDate <= endOfDayForEndDateFilter);
            }

            ViewBag.SearchString = searchString;
            ViewBag.IsActive = isActiveBool;
            ViewBag.StartDateFilter = startDateFilter?.ToString("yyyy-MM-dd");
            ViewBag.EndDateFilter = endDateFilter?.ToString("yyyy-MM-dd");

            return View(await promotions.OrderByDescending(p => p.StartDate).ToListAsync());
        }

        // GET: AdminPromotion/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var promotion = await _context.Promotions
                .Include(p => p.Product)
                .FirstOrDefaultAsync(m => m.PromotionID == id);

            if (promotion == null)
            {
                return NotFound();
            }

            return View(promotion);
        }

        // GET: AdminPromotion/Create
        public async Task<IActionResult> Create()
        {
            var products = await _context.Products
                .Where(p => p.IsActive)
                .OrderBy(p => p.ProductName)
                .ToListAsync();

            ViewBag.Products = new SelectList(products, "ProductID", "ProductName");

            // Mặc định ngày bắt đầu là hôm nay, ngày kết thúc là 30 ngày sau
            ViewBag.DefaultStartDate = DateTime.Now.ToString("yyyy-MM-dd");
            ViewBag.DefaultEndDate = DateTime.Now.AddDays(30).ToString("yyyy-MM-dd");

            return View();
        }

        // POST: AdminPromotion/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PromotionCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                foreach (var kvp in ModelState)
                {
                    foreach (var error in kvp.Value.Errors)
                    {
                        TempData["ErrorMessage"] = $"Field: {kvp.Key} | Error: {error.ErrorMessage}";
                    }
                }

                var productList = await _context.Products
                    .Where(p => p.IsActive)
                    .OrderBy(p => p.ProductName)
                    .ToListAsync();

                ViewBag.Products = new SelectList(productList, "ProductID", "ProductName", model.ProductID);
                return View(model);
            }

            // Kiểm tra mã khuyến mãi trùng
            if (!string.IsNullOrEmpty(model.PromoCode))
            {
                var existingPromo = await _context.Promotions
                    .FirstOrDefaultAsync(p => p.PromoCode == model.PromoCode);

                if (existingPromo != null)
                {
                    ModelState.AddModelError("PromoCode", "Mã khuyến mãi này đã tồn tại.");
                    var productList = await _context.Products
                        .Where(p => p.IsActive)
                        .OrderBy(p => p.ProductName)
                        .ToListAsync();
                    ViewBag.Products = new SelectList(productList, "ProductID", "ProductName", model.ProductID);
                    return View(model);
                }
            }

            var promotion = new Promotion
            {
                PromotionName = model.PromotionName,
                ProductID = model.ProductID,
                DiscountPercentage = model.DiscountPercentage,
                IsActive = model.IsActive,
                PromoCode = model.PromoCode,
                StartDate = model.StartDate,
                EndDate = model.EndDate,
                Description = model.Description
            };

            _context.Add(promotion);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "✅ Tạo khuyến mãi thành công.";
            return RedirectToAction(nameof(Index));
        }



        // GET: AdminPromotion/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var promotion = await _context.Promotions.FindAsync(id);
            if (promotion == null)
                return NotFound();

            var vm = new PromotionCreateViewModel
            {
                PromotionID = promotion.PromotionID,
                PromotionName = promotion.PromotionName,
                ProductID = promotion.ProductID,
                DiscountPercentage = promotion.DiscountPercentage,
                PromoCode = promotion.PromoCode,
                StartDate = promotion.StartDate,
                EndDate = promotion.EndDate,
                Description = promotion.Description,
                IsActive = promotion.IsActive
            };

            var products = await _context.Products
                .Where(p => p.IsActive)
                .OrderBy(p => p.ProductName)
                .ToListAsync();

            ViewBag.Products = new SelectList(products, "ProductID", "ProductName", vm.ProductID);
            return View(vm);
        }


        // POST: AdminPromotion/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(PromotionCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Dữ liệu không hợp lệ.";
                var products = await _context.Products.Where(p => p.IsActive).ToListAsync();
                ViewBag.Products = new SelectList(products, "ProductID", "ProductName", model.ProductID);
                return View(model);
            }

            var promotion = await _context.Promotions.FindAsync(model.PromotionID);
            if (promotion == null)
                return NotFound();

            // Kiểm tra mã trùng (trừ chính nó)
            if (!string.IsNullOrEmpty(model.PromoCode))
            {
                var existingPromo = await _context.Promotions
                    .FirstOrDefaultAsync(p => p.PromoCode == model.PromoCode && p.PromotionID != model.PromotionID);

                if (existingPromo != null)
                {
                    ModelState.AddModelError("PromoCode", "Mã khuyến mãi này đã tồn tại.");
                    var products = await _context.Products.Where(p => p.IsActive).ToListAsync();
                    ViewBag.Products = new SelectList(products, "ProductID", "ProductName", model.ProductID);
                    return View(model);
                }
            }

            // Cập nhật
            promotion.PromotionName = model.PromotionName;
            promotion.ProductID = model.ProductID;
            promotion.DiscountPercentage = model.DiscountPercentage;
            promotion.PromoCode = model.PromoCode;
            promotion.StartDate = model.StartDate;
            promotion.EndDate = model.EndDate;
            promotion.Description = model.Description;
            promotion.IsActive = model.IsActive;

            _context.Update(promotion);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Khuyến mãi đã được cập nhật.";
            return RedirectToAction(nameof(Index));
        }


        // GET: AdminPromotion/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var promotion = await _context.Promotions
                .Include(p => p.Product)
                .FirstOrDefaultAsync(m => m.PromotionID == id);

            if (promotion == null)
            {
                return NotFound();
            }

            return View(promotion);
        }

        // POST: AdminPromotion/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var promotion = await _context.Promotions.FindAsync(id);
            if (promotion != null)
            {
                _context.Promotions.Remove(promotion);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Khuyến mãi đã được xóa thành công.";
            }

            return RedirectToAction(nameof(Index));
        }

        private bool PromotionExists(int id)
        {
            return _context.Promotions.Any(e => e.PromotionID == id);
        }
    }
}
