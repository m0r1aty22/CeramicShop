using System.ComponentModel.DataAnnotations;

namespace CeramicShop.Models
{
    public class Category
    {
        [Key]
        public int CategoryID { get; set; }

        [Required, StringLength(100)]
        public string CategoryName { get; set; }

        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation properties
        public virtual ICollection<SubCategory> SubCategories { get; set; } = new List<SubCategory>();
    }
}