using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CeramicShop.Models
{
    public class ProductImage
    {
        [Key] // Đánh dấu đây là khóa chính
        [Column("ImageID")] // Map với cột ImageID trong database
        public int ProductImageID { get; set; }

        public int ProductID { get; set; }

        [Required, StringLength(255)]
        public string ImagePath { get; set; }

        public bool IsMainImage { get; set; }

        // Navigation properties
        [ForeignKey("ProductID")]
        public virtual Product Product { get; set; }
    }
}