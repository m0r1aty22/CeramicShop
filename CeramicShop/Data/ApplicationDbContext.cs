using Microsoft.EntityFrameworkCore;
using CeramicShop.Models; // Đảm bảo namespace này khớp với nơi bạn định nghĩa các model
using Microsoft.EntityFrameworkCore.SqlServer;


namespace CeramicShop.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<SubCategory> SubCategories { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<ProductImage> ProductImages { get; set; }
        public DbSet<Promotion> Promotions { get; set; }
        public DbSet<PaymentMethod> PaymentMethods { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderDetail> OrderDetails { get; set; }
        public DbSet<WishlistItem> WishlistItems { get; set; }
        public DbSet<Review> Reviews { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlServer("Data Source=103.82.36.41,1433;Initial Catalog=CeramicShop;Persist Security Info=True;User ID=sa;Password=123321Es;Trust Server Certificate=True");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Cấu hình kiểu dữ liệu decimal cho DiscountPercentage
            modelBuilder.Entity<Promotion>()
                .Property(p => p.DiscountPercentage)
                .HasColumnType("decimal(5, 2)");

            // Cấu hình cột tính toán cho OrderDetails.TotalPrice
            //modelBuilder.Entity<OrderDetail>()
               // .Property(od => od.TotalPrice)
               // .HasComputedColumnSql("[Quantity] * [UnitPrice]");

            // Cấu hình chỉ mục duy nhất cho UserName và Email
            modelBuilder.Entity<User>()
                .HasIndex(u => u.UserName)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();
            modelBuilder.Entity<Category>().ToTable("Categories");
            // Cấu hình mối quan hệ và hành vi xóa
            modelBuilder.Entity<Category>()
                .HasMany(c => c.SubCategories)
                .WithOne(s => s.Category)
                .HasForeignKey(s => s.CategoryID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SubCategory>()
                .HasMany(s => s.Products)
                .WithOne(p => p.SubCategory)
                .HasForeignKey(p => p.SubCategoryID)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Product>()
                .HasMany(p => p.ProductImages)
                .WithOne(i => i.Product)
                .HasForeignKey(i => i.ProductID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Product>()
                .HasMany(p => p.Promotions)
                .WithOne(pr => pr.Product)
                .HasForeignKey(pr => pr.ProductID)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<User>()
                .HasMany(u => u.Orders)
                .WithOne(o => o.User)
                .HasForeignKey(o => o.UserID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PaymentMethod>()
                .HasMany(pm => pm.Orders)
                .WithOne(o => o.PaymentMethod)
                .HasForeignKey(o => o.PaymentMethodID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Order>()
                .HasMany(o => o.OrderDetails)
                .WithOne(od => od.Order)
                .HasForeignKey(od => od.OrderID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Product>()
                .HasMany(p => p.OrderDetails)
                .WithOne(od => od.Product)
                .HasForeignKey(od => od.ProductID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<User>()
                .HasMany(u => u.WishlistItems)
                .WithOne(w => w.User)
                .HasForeignKey(w => w.UserID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Product>()
                .HasMany(p => p.WishlistItems)
                .WithOne(w => w.Product)
                .HasForeignKey(w => w.ProductID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<User>()
                .HasMany(u => u.Reviews)
                .WithOne(r => r.User)
                .HasForeignKey(r => r.UserID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Product>()
                .HasMany(p => p.Reviews)
                .WithOne(r => r.Product)
                .HasForeignKey(r => r.ProductID)
                .OnDelete(DeleteBehavior.Cascade);

            // Cấu hình giá trị mặc định
            modelBuilder.Entity<Category>()
                .Property(c => c.IsActive)
                .HasDefaultValue(true);

            modelBuilder.Entity<Category>()
                .Property(c => c.CreatedAt)
                .HasDefaultValueSql("GETDATE()");

            modelBuilder.Entity<Order>()
                .Property(o => o.OrderStatus)
                .HasDefaultValue("Pending");

            modelBuilder.Entity<Order>()
                .Property(o => o.CreatedAt)
                .HasDefaultValueSql("GETDATE()");

            modelBuilder.Entity<PaymentMethod>()
                .Property(pm => pm.IsActive)
                .HasDefaultValue(true);

            modelBuilder.Entity<ProductImage>()
                .Property(pi => pi.IsMainImage)
                .HasDefaultValue(false);

            modelBuilder.Entity<Product>()
                .Property(p => p.StockQuantity)
                .HasDefaultValue(0);

            modelBuilder.Entity<Product>()
                .Property(p => p.CreatedAt)
                .HasDefaultValueSql("GETDATE()");

            modelBuilder.Entity<Product>()
                .Property(p => p.IsActive)
                .HasDefaultValue(true);

            modelBuilder.Entity<Promotion>()
                .Property(pr => pr.IsActive)
                .HasDefaultValue(true);

            modelBuilder.Entity<Review>()
                .Property(r => r.CreatedAt)
                .HasDefaultValueSql("GETDATE()");

            modelBuilder.Entity<SubCategory>()
                .Property(sc => sc.IsActive)
                .HasDefaultValue(true);

            modelBuilder.Entity<SubCategory>()
                .Property(sc => sc.CreatedAt)
                .HasDefaultValueSql("GETDATE()");

            modelBuilder.Entity<User>()
                .Property(u => u.Role)
                .HasDefaultValue("Customer");

            modelBuilder.Entity<User>()
                .Property(u => u.CreatedAt)
                .HasDefaultValueSql("GETDATE()");

            modelBuilder.Entity<WishlistItem>()
                .Property(w => w.AddedAt)
                .HasDefaultValueSql("GETDATE()");

            // Cấu hình ràng buộc CHECK cho Rating trong Reviews
            modelBuilder.Entity<Review>()
                .Property(r => r.Rating)
                .IsRequired()
                .HasAnnotation("CheckConstraint", "Rating >= 1 AND Rating <= 5");
            
        }
    }
}