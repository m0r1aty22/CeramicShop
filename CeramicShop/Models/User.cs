using System.ComponentModel.DataAnnotations;

namespace CeramicShop.Models
{
    public class User
    {
        [Key]
        public int UserID { get; set; }

        [Required, StringLength(50)]
        public string UserName { get; set; }

        [Required, StringLength(100)]
        public string Password { get; set; }

        [Required, StringLength(100), EmailAddress]
        public string Email { get; set; }

        [Required, StringLength(100)]
        public string FullName { get; set; }

        [StringLength(20)]
        public string Phone { get; set; }

        public string Address { get; set; }

        [StringLength(20)]
        public string Role { get; set; } = "Customer";

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation properties
        public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

        public virtual ICollection<WishlistItem> WishlistItems { get; set; } = new List<WishlistItem>();

        public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();
    }
}