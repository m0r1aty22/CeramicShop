using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CeramicShop.Models
{
    public class Promotion
    {
        [Key]
        public int PromotionID { get; set; }

        [StringLength(100)]
        public string PromotionName { get; set; }

        public string Description { get; set; }

        [Column(TypeName = "decimal(5, 2)")]
        public decimal DiscountPercentage { get; set; }

        public int? ProductID { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public string? PromoCode { get; set; }

        public bool IsActive { get; set; } = true;

        // Navigation properties
        [ForeignKey("ProductID")]
        public virtual Product Product { get; set; }
    }
}
