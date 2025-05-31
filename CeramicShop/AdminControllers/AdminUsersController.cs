using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CeramicShop.Data;
using CeramicShop.Models;
using Microsoft.AspNetCore.Authorization;
using System.Linq;
using System.Threading.Tasks;
using CeramicShop.Models.ViewModels;

namespace CeramicShop.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("AdminUser")]
    public class AdminUsersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminUsersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /AdminUser
        public async Task<IActionResult> Index(string searchString)
        {
            var users = _context.Users.AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                users = users.Where(u =>
                    u.FullName.Contains(searchString) ||
                    u.Email.Contains(searchString) ||
                    u.UserName.Contains(searchString));
            }

            ViewBag.SearchString = searchString;
            return View(await users.OrderByDescending(u => u.CreatedAt).ToListAsync());
        }

        // GET: /AdminUser/Details/5
        [HttpGet("Details/{id}")]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var user = await _context.Users
                .Include(u => u.Orders)
                .Include(u => u.WishlistItems)
                .Include(u => u.Reviews)
                .FirstOrDefaultAsync(m => m.UserID == id);

            if (user == null) return NotFound();

            return View(user);
        }

        // GET: /AdminUser/Edit/5
        [HttpGet("Edit/{id}")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            var vm = new UserEditViewModel
            {
                UserID = user.UserID,
                FullName = user.FullName,
                Email = user.Email,
                Phone = user.Phone,
                Address = user.Address,
                Role = user.Role,
                UserName = user.UserName
            };

            return View(vm);
        }

        // POST: /AdminUser/Edit/5
        [HttpPost("Edit/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, UserEditViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Dữ liệu không hợp lệ. Vui lòng kiểm tra lại.";
                return View(vm);
            }

            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy người dùng.";
                return RedirectToAction(nameof(Index));
            }

            user.FullName = vm.FullName;
            user.Email = vm.Email;
            user.Phone = vm.Phone;
            user.Address = vm.Address;
            user.Role = vm.Role;

            try
            {
                _context.Update(user);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Cập nhật người dùng thành công.";
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                TempData["ErrorMessage"] = "Lỗi khi cập nhật người dùng.";
                return View(vm);
            }
        }

        // GET: /AdminUser/Delete/5
        [HttpGet("Delete/{id}")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var user = await _context.Users.FirstOrDefaultAsync(m => m.UserID == id);
            if (user == null) return NotFound();

            return View(user);
        }

        // POST: /AdminUser/Delete/5
        [HttpPost("Delete/{id}"), ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                _context.Users.Remove(user);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Tài khoản đã được xóa thành công.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
