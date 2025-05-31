using System.ComponentModel.DataAnnotations;

namespace CeramicShop.Models.ViewModels
{
    public class PaymentMethodCreateViewModel
    {
        [Required(ErrorMessage = "Tên phương thức là bắt buộc.")]
        [StringLength(100)]
        public string MethodName { get; set; }

        public bool IsActive { get; set; } = true;
    }
}