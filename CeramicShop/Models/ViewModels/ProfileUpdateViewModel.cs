// CeramicShop/Models/ViewModels/ProfileUpdateViewModel.cs
using System.ComponentModel.DataAnnotations;

namespace CeramicShop.Models.ViewModels
{
    public class ProfileUpdateViewModel
    {
        [Required(ErrorMessage = "Họ và tên là bắt buộc.")]
        [StringLength(100, ErrorMessage = "Họ và tên không được vượt quá 100 ký tự.")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Email là bắt buộc.")]
        [EmailAddress(ErrorMessage = "Địa chỉ email không hợp lệ.")]
        [StringLength(100, ErrorMessage = "Email không được vượt quá 100 ký tự.")]
        public string Email { get; set; }

        [StringLength(15, ErrorMessage = "Số điện thoại không được vượt quá 15 ký tự.")]
        //[Phone(ErrorMessage = "Số điện thoại không hợp lệ.")] // Có thể thêm nếu muốn validation chặt chẽ hơn
        public string Phone { get; set; }

        [StringLength(255, ErrorMessage = "Địa chỉ không được vượt quá 255 ký tự.")]
        public string Address { get; set; }
    }
}