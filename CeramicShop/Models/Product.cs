using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic; // Thêm using này nếu chưa có

namespace CeramicShop.Models
{
    public class Product
    {
        [Key]
        public int ProductID { get; set; }

        [Required(ErrorMessage = "Tên sản phẩm là bắt buộc.")]
        [StringLength(200, ErrorMessage = "Tên sản phẩm không được vượt quá 200 ký tự.")]
        public string ProductName { get; set; }

        public string? Description { get; set; } // Cho phép null

        [Required(ErrorMessage = "Giá sản phẩm là bắt buộc.")]
        [Column(TypeName = "decimal(18,2)")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Giá sản phẩm phải lớn hơn 0.")]
        public decimal Price { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Số lượng tồn kho không được âm.")]
        public int StockQuantity { get; set; }

        // Sẽ được validate trong controller hoặc bằng [Range] nếu 0 không phải là giá trị hợp lệ
        // Ví dụ: [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn danh mục con.")]
        public int SubCategoryID { get; set; }

        public bool IsActive { get; set; } = true; // Mặc định là true

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; } // Có thể null, sẽ được set khi cập nhật

        // Navigation properties
        [ForeignKey("SubCategoryID")]
        public virtual SubCategory? SubCategory { get; set; } // Quan trọng: Cho phép SubCategory là null

        // Khởi tạo các collection để tránh lỗi "field is required" cho chúng
        public virtual ICollection<ProductImage> ProductImages { get; set; } = new List<ProductImage>();
        public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
        public virtual ICollection<Promotion> Promotions { get; set; } = new List<Promotion>();
        public virtual ICollection<WishlistItem> WishlistItems { get; set; } = new List<WishlistItem>();
        public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();

        // Constructor để khởi tạo các giá trị mặc định nếu bạn thích cách này
        // public Product()
        // {
        //     IsActive = true;
        //     CreatedAt = DateTime.Now;
        //     // UpdatedAt có thể để null ban đầu, hoặc set là DateTime.Now
        //     // ProductImages = new HashSet<ProductImage>(); // Hoặc List<T>
        //     // OrderDetails = new HashSet<OrderDetail>();
        //     // Promotions = new HashSet<Promotion>();
        //     // WishlistItems = new HashSet<WishlistItem>();
        //     // Reviews = new HashSet<Review>();
        // }
    }
}