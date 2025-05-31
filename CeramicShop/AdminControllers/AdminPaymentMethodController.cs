using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CeramicShop.Data;
using CeramicShop.Models;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using CeramicShop.Models.ViewModels;

namespace CeramicShop.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminPaymentMethodController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminPaymentMethodController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: AdminPaymentMethod
        public async Task<IActionResult> Index(string searchString)
        {
            var paymentMethods = from p in _context.PaymentMethods
                                 select p;

            if (!string.IsNullOrEmpty(searchString))
            {
                paymentMethods = paymentMethods.Where(p => p.MethodName.Contains(searchString));
            }

            ViewBag.SearchString = searchString;
            return View(await paymentMethods.ToListAsync());
        }

        // GET: AdminPaymentMethod/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var paymentMethod = await _context.PaymentMethods
                .FirstOrDefaultAsync(m => m.PaymentMethodID == id);
            if (paymentMethod == null)
            {
                return NotFound();
            }

            return View(paymentMethod);
        }

        // GET: AdminPaymentMethod/Create
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PaymentMethodCreateViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Dữ liệu không hợp lệ. Vui lòng kiểm tra lại.";
                return View(vm);
            }

            var paymentMethod = new PaymentMethod
            {
                MethodName = vm.MethodName,
                IsActive = vm.IsActive
            };

            _context.PaymentMethods.Add(paymentMethod);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Phương thức thanh toán đã được thêm thành công.";
            return RedirectToAction(nameof(Index));
        }



        // GET: AdminPaymentMethod/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var paymentMethod = await _context.PaymentMethods.FindAsync(id);
            if (paymentMethod == null)
            {
                return NotFound();
            }

            return View(paymentMethod);
        }

        // POST: AdminPaymentMethod/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("PaymentMethodID,MethodName,IsActive")] PaymentMethod paymentMethod)
        {
            if (id != paymentMethod.PaymentMethodID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
                foreach (var kvp in ModelState)
                {
                    foreach (var error in kvp.Value.Errors)
                    {
                        Console.WriteLine($"Field: {kvp.Key} | Error: {error.ErrorMessage}");
                        TempData["ErrorMessage"] = $"Field: {kvp.Key} | Error: {error.ErrorMessage}";
                    }
                }

            {
                try
                {
                    _context.Update(paymentMethod);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Phương thức thanh toán đã được cập nhật thành công.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PaymentMethodExists(paymentMethod.PaymentMethodID))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }

                return RedirectToAction(nameof(Index));
            }

            TempData["ErrorMessage"] = "Dữ liệu không hợp lệ.";
            return View(paymentMethod);
        }


        // GET: AdminPaymentMethod/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var paymentMethod = await _context.PaymentMethods
                .FirstOrDefaultAsync(m => m.PaymentMethodID == id);
            if (paymentMethod == null)
            {
                return NotFound();
            }

            return View(paymentMethod);
        }

        // POST: AdminPaymentMethod/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var paymentMethod = await _context.PaymentMethods.FindAsync(id);

            // Kiểm tra xem phương thức thanh toán có đang được sử dụng không
            var isInUse = await _context.Orders.AnyAsync(o => o.PaymentMethodID == id);

            if (isInUse)
            {
                TempData["ErrorMessage"] = "Không thể xóa phương thức thanh toán này vì đang được sử dụng trong đơn hàng.";
                return RedirectToAction(nameof(Index));
            }

            if (paymentMethod != null)
            {
                _context.PaymentMethods.Remove(paymentMethod);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Phương thức thanh toán đã được xóa thành công.";
            }

            return RedirectToAction(nameof(Index));
        }

        private bool PaymentMethodExists(int id)
        {
            return _context.PaymentMethods.Any(e => e.PaymentMethodID == id);
        }
    }
}
