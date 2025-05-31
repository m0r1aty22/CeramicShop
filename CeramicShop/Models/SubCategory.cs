using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel;

namespace CeramicShop.Models
{
    public class SubCategory
    {
        [Key]
        public int SubCategoryID { get; set; }

        [Required(ErrorMessage = "Tên danh mục con là bắt buộc")]
        [StringLength(100)]
        [DisplayName("Tên danh mục con")]
        public string SubCategoryName { get; set; }

        [DisplayName("Mô tả")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Danh mục cha là bắt buộc")]
        [DisplayName("Danh mục cha")]
        public int CategoryID { get; set; }

        [DisplayName("Kích hoạt")]
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation properties - đánh dấu là không bắt buộc
        [ForeignKey("CategoryID")]
        public virtual Category? Category { get; set; }

        public virtual ICollection<Product>? Products { get; set; }
    }
}