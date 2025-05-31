using System;
using System.ComponentModel.DataAnnotations;

namespace CeramicShop.Models.ViewModels
{
    public class PromotionCreateViewModel
    {
        public int? PromotionID { get; set; }

        [StringLength(100)]
        public string PromotionName { get; set; }

        public string Description { get; set; }

        [Range(0, 100)]
        [Display(Name = "Phần trăm giảm giá (%)")]
        public decimal DiscountPercentage { get; set; }

        public int? ProductID { get; set; }

        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime EndDate { get; set; }

        public string? PromoCode { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
