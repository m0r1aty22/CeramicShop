using System.ComponentModel.DataAnnotations;

namespace CeramicShop.Models.ViewModels
{
    public class UserEditViewModel
    {
        public int UserID { get; set; }

        [Required(ErrorMessage = "Họ tên không được bỏ trống")]
        [StringLength(100)]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Email không được bỏ trống")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string Email { get; set; }

        [StringLength(20)]
        public string Phone { get; set; }

        public string Address { get; set; }

        [Required(ErrorMessage = "Vai trò không được bỏ trống")]
        public string Role { get; set; }

        public string UserName { get; set; }  // readonly hiển thị
    }
}
