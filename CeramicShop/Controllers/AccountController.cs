using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CeramicShop.Data;
using CeramicShop.Models;
using System.Security.Cryptography;
using System.Text;
using CeramicShop.Services;
using CeramicShop.Models.ViewModels;
using CeramicShop.Services;

namespace CeramicShop.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;

        public AccountController(ApplicationDbContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        public IActionResult Login(string returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string username, string password, string returnUrl, bool rememberMe = false)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ModelState.AddModelError("", "Tên đăng nhập và mật khẩu là bắt buộc.");
                return View();
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == username);
            if (user == null || !VerifyPassword(password, user.Password))
            {
                ModelState.AddModelError("", "Tên đăng nhập hoặc mật khẩu không hợp lệ.");
                return View();
            }

            // --- Bước gửi OTP qua email ---
            var otp = GenerateOTP();
            HttpContext.Session.SetString($"OTP_{user.Email}", otp);
            HttpContext.Session.SetString($"OTPExpiry_{user.Email}", DateTime.Now.AddMinutes(15).ToString());
            HttpContext.Session.SetString("LoginUserName", user.UserName);
            HttpContext.Session.SetString("LoginRememberMe", rememberMe.ToString());
            HttpContext.Session.SetString("LoginReturnUrl", returnUrl ?? "");

            var subject = "Mã xác thực đăng nhập - 2FA";
            var message = $@"
        <html>
        <body>
            <p>Chào {user.UserName},</p>
            <p>Mã xác thực đăng nhập của bạn là:</p>
            <h2>{otp}</h2>
            <p>Mã này có hiệu lực trong 15 phút. Nếu bạn không yêu cầu, vui lòng bỏ qua email này.</p>
        </body>
        </html>";

            try
            {
                await _emailService.SendEmailAsync(user.Email, subject, message);
                TempData["SuccessMessage"] = "Mã OTP đã được gửi đến email của bạn. Vui lòng kiểm tra hộp thư và nhập mã OTP để đăng nhập.";
                return RedirectToAction("VerifyLoginOTP");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Lỗi gửi mã OTP qua email. Vui lòng thử lại.");
                return View();
            }
        }
        [HttpGet]
        public IActionResult VerifyLoginOTP()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("LoginUserName")))
            {
                // Nếu chưa có username trong session thì redirect về login
                return RedirectToAction("Login");
            }

            return View();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyLoginOTP(string otpInput)
        {
            var username = HttpContext.Session.GetString("LoginUserName");
            var rememberMeString = HttpContext.Session.GetString("LoginRememberMe");
            var returnUrl = HttpContext.Session.GetString("LoginReturnUrl");
            bool rememberMe = bool.TryParse(rememberMeString, out var rm) && rm;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(otpInput))
            {
                ModelState.AddModelError("", "Mã OTP là bắt buộc.");
                return View();
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == username);
            if (user == null)
            {
                TempData["ErrorMessage"] = "Phiên đăng nhập không hợp lệ. Vui lòng đăng nhập lại.";
                return RedirectToAction("Login");
            }

            var storedOTP = HttpContext.Session.GetString($"OTP_{user.Email}");
            var expiryString = HttpContext.Session.GetString($"OTPExpiry_{user.Email}");

            if (string.IsNullOrEmpty(storedOTP) || string.IsNullOrEmpty(expiryString) ||
                !DateTime.TryParse(expiryString, out var expiry) || expiry < DateTime.Now)
            {
                TempData["ErrorMessage"] = "Mã OTP đã hết hạn hoặc không hợp lệ. Vui lòng đăng nhập lại để lấy mã mới.";
                return RedirectToAction("Login");
            }

            if (otpInput != storedOTP)
            {
                ModelState.AddModelError("", "Mã OTP không chính xác. Vui lòng thử lại.");
                return View();
            }

            // Xác thực thành công, đăng nhập người dùng
            var claims = new[]
            {
                new Claim(ClaimTypes.Name, user.UserName),
                new Claim(ClaimTypes.NameIdentifier, user.UserID.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role)
    };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = rememberMe,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(rememberMe ? 30 : 1)
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            // Xoá session OTP và login tạm thời
            HttpContext.Session.Remove($"OTP_{user.Email}");
            HttpContext.Session.Remove($"OTPExpiry_{user.Email}");
            HttpContext.Session.Remove("LoginUserName");
            HttpContext.Session.Remove("LoginRememberMe");
            HttpContext.Session.Remove("LoginReturnUrl");

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Home");
        }

        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(User user, string confirmPassword)
        {
            if (string.IsNullOrEmpty(user.UserName) || string.IsNullOrEmpty(user.Password) ||
                string.IsNullOrEmpty(user.Email) || string.IsNullOrEmpty(user.FullName))
            {
                ModelState.AddModelError("", "Tất cả các trường là bắt buộc.");
                return View(user);
            }

            if (user.Password != confirmPassword)
            {
                ModelState.AddModelError("", "Mật khẩu không khớp.");
                return View(user);
            }

            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.UserName == user.UserName || u.Email == user.Email);
            if (existingUser != null)
            {
                ModelState.AddModelError("", "Tên đăng nhập hoặc email đã tồn tại.");
                return View(user);
            }

            // Mã hóa mật khẩu trước khi lưu
            user.Password = HashPassword(user.Password);
            user.Role = "Customer";
            user.CreatedAt = DateTime.Now;

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var claims = new[]
            {
                new Claim(ClaimTypes.Name, user.UserName),
                new Claim(ClaimTypes.NameIdentifier, user.UserID.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role)
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = false,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(1)
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            return RedirectToAction("Index", "Home");
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        public async Task<IActionResult> CheckUsernameAvailability(string username)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == username);
            return Json(user == null);
        }

        [HttpPost]
        public async Task<IActionResult> CheckEmailAvailability(string email)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            return Json(user == null);
        }

        [HttpGet]
        public async Task<IActionResult> GetUserInfo()
        {
            if (!User.Identity.IsAuthenticated)
            {
                return Json(null);
            }

            var username = User.Identity.Name;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == username);

            if (user == null)
            {
                return Json(null);
            }

            return Json(new
            {
                fullName = user.FullName,
                email = user.Email,
                phone = user.Phone,
                address = user.Address
            });
        }

        [HttpGet]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> Profile()
        {
            var username = User.Identity.Name;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == username);

            if (user == null)
            {
                return RedirectToAction("Login");
            }

            // Lấy danh sách đơn hàng gần đây
            var recentOrders = await _context.Orders
                .Include(o => o.PaymentMethod)
                .Where(o => o.UserID == user.UserID)
                .OrderByDescending(o => o.CreatedAt)
                .Select(o => new OrderViewModel
                {
                    OrderID = o.OrderID,
                    TotalAmount = o.TotalAmount,
                    OrderStatus = o.OrderStatus,
                    CreatedAt = o.CreatedAt,
                    PaymentMethod = o.PaymentMethod != null ? o.PaymentMethod.MethodName : "N/A"
                })
                .Take(5)
                .ToListAsync();

            ViewBag.RecentOrders = recentOrders;

            return View(user);
        }

        [HttpPost]
        [Microsoft.AspNetCore.Authorization.Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(ProfileUpdateViewModel viewModel)
        {
            var username = User.Identity.Name;
            var userToUpdate = await _context.Users.FirstOrDefaultAsync(u => u.UserName == username);

            if (userToUpdate == null)
            {
                return RedirectToAction("Login");
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray()
                );
                foreach (var error in errors)
                {
                    System.Diagnostics.Debug.WriteLine($"Field: {error.Key}, Errors: {string.Join(", ", error.Value)}");
                }

                // Tải lại danh sách đơn hàng gần đây
                var recentOrders = await _context.Orders
                    .Include(o => o.PaymentMethod)
                    .Where(o => o.UserID == userToUpdate.UserID)
                    .OrderByDescending(o => o.CreatedAt)
                    .Select(o => new OrderViewModel
                    {
                        OrderID = o.OrderID,
                        TotalAmount = o.TotalAmount,
                        OrderStatus = o.OrderStatus,
                        CreatedAt = o.CreatedAt,
                        PaymentMethod = o.PaymentMethod != null ? o.PaymentMethod.MethodName : "N/A"
                    })
                    .Take(5)
                    .ToListAsync();
                ViewBag.RecentOrders = recentOrders;

                ViewBag.ErrorMessage = "Dữ liệu không hợp lệ. Vui lòng kiểm tra lại các trường đã nhập.";
                return View("Profile", userToUpdate);
            }

            // Kiểm tra nếu Email bị thay đổi và email mới đã tồn tại ở user khác
            if (userToUpdate.Email != viewModel.Email &&
                await _context.Users.AnyAsync(u => u.Email == viewModel.Email && u.UserID != userToUpdate.UserID))
            {
                ModelState.AddModelError("Email", "Địa chỉ email này đã được sử dụng bởi một tài khoản khác.");
                var recentOrdersForEmailError = await _context.Orders
                    .Include(o => o.PaymentMethod)
                    .Where(o => o.UserID == userToUpdate.UserID)
                    .OrderByDescending(o => o.CreatedAt)
                    .Select(o => new OrderViewModel
                    {
                        OrderID = o.OrderID,
                        TotalAmount = o.TotalAmount,
                        OrderStatus = o.OrderStatus,
                        CreatedAt = o.CreatedAt,
                        PaymentMethod = o.PaymentMethod != null ? o.PaymentMethod.MethodName : "N/A"
                    })
                    .Take(5).ToListAsync();
                ViewBag.RecentOrders = recentOrdersForEmailError;
                ViewBag.ErrorMessage = "Địa chỉ email này đã được sử dụng bởi một tài khoản khác.";
                return View("Profile", userToUpdate);
            }

            try
            {
                userToUpdate.FullName = viewModel.FullName;
                userToUpdate.Email = viewModel.Email;
                userToUpdate.Phone = viewModel.Phone;
                userToUpdate.Address = viewModel.Address;

                await _context.SaveChangesAsync();

                var recentOrders = await _context.Orders
                    .Include(o => o.PaymentMethod)
                    .Where(o => o.UserID == userToUpdate.UserID)
                    .OrderByDescending(o => o.CreatedAt)
                    .Select(o => new OrderViewModel
                    {
                        OrderID = o.OrderID,
                        TotalAmount = o.TotalAmount,
                        OrderStatus = o.OrderStatus,
                        CreatedAt = o.CreatedAt,
                        PaymentMethod = o.PaymentMethod != null ? o.PaymentMethod.MethodName : "N/A"
                    })
                    .Take(5)
                    .ToListAsync();
                ViewBag.RecentOrders = recentOrders;

                ViewBag.SuccessMessage = "Hồ sơ đã được cập nhật thành công.";
                return View("Profile", userToUpdate);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating profile: {ex.Message}");
                var recentOrdersEx = await _context.Orders
                    .Include(o => o.PaymentMethod)
                    .Where(o => o.UserID == userToUpdate.UserID)
                    .OrderByDescending(o => o.CreatedAt)
                    .Select(o => new OrderViewModel
                    {
                        OrderID = o.OrderID,
                        TotalAmount = o.TotalAmount,
                        OrderStatus = o.OrderStatus,
                        CreatedAt = o.CreatedAt,
                        PaymentMethod = o.PaymentMethod != null ? o.PaymentMethod.MethodName : "N/A"
                    })
                    .Take(5)
                    .ToListAsync();
                ViewBag.RecentOrders = recentOrdersEx;
                ViewBag.ErrorMessage = "Lỗi khi cập nhật hồ sơ: " + ex.Message;
                return View("Profile", userToUpdate);
            }
        }

        [HttpPost]
        [Microsoft.AspNetCore.Authorization.Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            var username = User.Identity.Name;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == username);

            if (user == null)
            {
                return RedirectToAction("Login");
            }

            // Lấy danh sách đơn hàng gần đây
            var recentOrders = await _context.Orders
                .Include(o => o.PaymentMethod) // Thêm dòng này để tránh lỗi NullReferenceException
                .Where(o => o.UserID == user.UserID)
                .OrderByDescending(o => o.CreatedAt)
                .Select(o => new OrderViewModel
                {
                    OrderID = o.OrderID,
                    TotalAmount = o.TotalAmount,
                    OrderStatus = o.OrderStatus,
                    CreatedAt = o.CreatedAt,
                    PaymentMethod = o.PaymentMethod != null ? o.PaymentMethod.MethodName : "N/A"
                })
                .Take(5)
                .ToListAsync();

            ViewBag.RecentOrders = recentOrders;

            if (!VerifyPassword(currentPassword, user.Password))
            {
                ViewBag.ErrorMessage = "Mật khẩu hiện tại không đúng.";
                return View("Profile", user);
            }

            if (newPassword != confirmPassword)
            {
                ViewBag.ErrorMessage = "Mật khẩu mới không khớp.";
                return View("Profile", user);
            }

            user.Password = HashPassword(newPassword);
            _context.Entry(user).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            ViewBag.SuccessMessage = "Mật khẩu đã được thay đổi thành công.";
            return View("Profile", user);
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
            if (user == null)
            {
                TempData["ErrorMessage"] = "Email không tồn tại trong hệ thống.";
                return View(model);
            }

            var otp = GenerateOTP();
            HttpContext.Session.SetString($"OTP_{model.Email}", otp);
            HttpContext.Session.SetString($"OTPExpiry_{model.Email}", DateTime.Now.AddMinutes(15).ToString());

            var subject = "Mã xác nhận đặt lại mật khẩu";
            var message = $@"
                <html>
                <head>
                    <style>
                        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                        .header {{ background-color: #0f07be; color: white; padding: 10px; text-align: center; }}
                        .content {{ padding: 20px; border: 1px solid #ddd; }}
                        .otp {{ font-size: 24px; font-weight: bold; text-align: center; margin: 20px 0; letter-spacing: 5px; }}
                        .footer {{ text-align: center; margin-top: 20px; font-size: 12px; color: #777; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <h2>CeramicShop- Đặt lại mật khẩu</h2>
                        </div>
                        <div class='content'>
                            <p>Xin chào,</p>
                            <p>Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản của bạn. Vui lòng sử dụng mã OTP dưới đây để xác minh:</p>
                            <div class='otp'>{otp}</div>
                            <p>Mã này có hiệu lực trong 15 phút. Nếu bạn không yêu cầu đặt lại mật khẩu, vui lòng bỏ qua email này.</p>
                            <p>Trân trọng,<br>Đội ngũ CeramicShop</p>
                        </div>
                        <div class='footer'>
                            <p>Email này được gửi tự động, vui lòng không trả lời.</p>
                        </div>
                    </div>
                </body>
                </html>
            ";

            try
            {
                await _emailService.SendEmailAsync(model.Email, subject, message);
                var verifyModel = new VerifyOTPViewModel { Email = model.Email };
                TempData["SuccessMessage"] = "Mã OTP đã được gửi đến email của bạn.";
                return View("VerifyOTP", verifyModel);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi gửi email: {ex.Message}";
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult VerifyOTP(VerifyOTPViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var storedOTP = HttpContext.Session.GetString($"OTP_{model.Email}");
            var expiryString = HttpContext.Session.GetString($"OTPExpiry_{model.Email}");

            if (string.IsNullOrEmpty(storedOTP) || string.IsNullOrEmpty(expiryString))
            {
                TempData["ErrorMessage"] = "Mã OTP không hợp lệ hoặc đã hết hạn. Vui lòng yêu cầu mã mới.";
                return View(model);
            }

            if (!DateTime.TryParse(expiryString, out var expiry) || expiry < DateTime.Now)
            {
                TempData["ErrorMessage"] = "Mã OTP đã hết hạn. Vui lòng yêu cầu mã mới.";
                HttpContext.Session.Remove($"OTP_{model.Email}");
                HttpContext.Session.Remove($"OTPExpiry_{model.Email}");
                return View(model);
            }

            if (model.OTP != storedOTP)
            {
                TempData["ErrorMessage"] = "Mã OTP không chính xác. Vui lòng thử lại.";
                return View(model);
            }

            var resetModel = new ResetPasswordViewModel
            {
                Email = model.Email,
                OTP = model.OTP
            };

            return View("ResetPassword", resetModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var storedOTP = HttpContext.Session.GetString($"OTP_{model.Email}");
            var expiryString = HttpContext.Session.GetString($"OTPExpiry_{model.Email}");

            if (string.IsNullOrEmpty(storedOTP) || string.IsNullOrEmpty(expiryString) ||
                model.OTP != storedOTP || !DateTime.TryParse(expiryString, out var expiry) || expiry < DateTime.Now)
            {
                TempData["ErrorMessage"] = "Phiên làm việc đã hết hạn. Vui lòng yêu cầu mã OTP mới.";
                return RedirectToAction("ForgotPassword");
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
            if (user == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy tài khoản với email này.";
                return RedirectToAction("ForgotPassword");
            }

            user.Password = HashPassword(model.NewPassword);
            _context.Entry(user).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            HttpContext.Session.Remove($"OTP_{model.Email}");
            HttpContext.Session.Remove($"OTPExpiry_{model.Email}");

            TempData["SuccessMessage"] = "Mật khẩu đã được đặt lại thành công. Vui lòng đăng nhập với mật khẩu mới.";
            return RedirectToAction("Login");
        }

        private string HashPassword(string password)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }

        private bool VerifyPassword(string inputPassword, string storedPassword)
        {
            string hashedInput = HashPassword(inputPassword);
            return hashedInput.Equals(storedPassword);
        }

        private string GenerateOTP()
        {
            Random random = new Random();
            return random.Next(100000, 999999).ToString();
        }
    }
}