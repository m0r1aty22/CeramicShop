using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CeramicShop.Models
{
    public class PaymentMethod
    {
        [Key]
        public int PaymentMethodID { get; set; }

        [Required]
        [StringLength(100)]
        public string MethodName { get; set; }

        public bool IsActive { get; set; } = true;

        // Navigation property
        public virtual ICollection<Order> Orders { get; set; }
    }
}
