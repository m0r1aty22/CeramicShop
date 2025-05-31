using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CeramicShop.Models
{
    public class Order
    {
        [Key]
        public int OrderID { get; set; }

        [Required]
        public int UserID { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [Required]
        public int PaymentMethodID { get; set; }

        [StringLength(50)]
        public string OrderStatus { get; set; } = "Pending"; // Default value

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.Now; // Default value

        public DateTime? UpdatedAt { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; } = string.Empty; // Default value

        [Required]
        [StringLength(500)]
        public string ShippingAddress { get; set; } = string.Empty; // Default value

        // Navigation properties
        [ForeignKey("UserID")]
        public virtual User User { get; set; }

        [ForeignKey("PaymentMethodID")]
        public virtual PaymentMethod PaymentMethod { get; set; }

        public virtual ICollection<OrderDetail> OrderDetails { get; set; }
    }
}
