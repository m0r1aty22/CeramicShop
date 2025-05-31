using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CeramicShop.Data;
using CeramicShop.Models;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

namespace CeramicShop.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminReviewController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminReviewController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: AdminReview
        public async Task<IActionResult> Index(string searchString, int? rating, int? productId)
        {
            var reviews = _context.Reviews
                .Include(r => r.Product)
                .Include(r => r.User)
                .AsQueryable();

            // Lọc theo từ khóa tìm kiếm
            if (!string.IsNullOrEmpty(searchString))
            {
                reviews = reviews.Where(r => r.Comment.Contains(searchString) ||
                                           r.Product.ProductName.Contains(searchString) ||
                                           r.User.FullName.Contains(searchString) ||
                                           r.User.Email.Contains(searchString));
            }

            // Lọc theo đánh giá
            if (rating.HasValue)
            {
                reviews = reviews.Where(r => r.Rating == rating.Value);
            }

            // Lọc theo sản phẩm
            if (productId.HasValue)
            {
                reviews = reviews.Where(r => r.ProductID == productId.Value);
            }

            // Lấy danh sách sản phẩm cho dropdown
            ViewBag.Products = await _context.Products
                .Where(p => p.IsActive)
                .OrderBy(p => p.ProductName)
                .Select(p => new { p.ProductID, p.ProductName })
                .ToListAsync();

            ViewBag.SearchString = searchString;
            ViewBag.Rating = rating;
            ViewBag.ProductId = productId;

            return View(await reviews.OrderByDescending(r => r.CreatedAt).ToListAsync());
        }

        // GET: AdminReview/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var review = await _context.Reviews
                .Include(r => r.Product)
                .Include(r => r.User)
                .FirstOrDefaultAsync(m => m.ReviewID == id);

            if (review == null)
            {
                return NotFound();
            }

            return View(review);
        }

        // GET: AdminReview/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var review = await _context.Reviews
                .Include(r => r.Product)
                .Include(r => r.User)
                .FirstOrDefaultAsync(m => m.ReviewID == id);

            if (review == null)
            {
                return NotFound();
            }

            return View(review);
        }

        // POST: AdminReview/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var review = await _context.Reviews.FindAsync(id);
            if (review != null)
            {
                _context.Reviews.Remove(review);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Đánh giá đã được xóa thành công.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
