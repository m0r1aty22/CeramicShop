using System;
using System.Collections.Generic;
using System.Linq;

namespace CeramicShop.Models.ViewModels
{
    public class ProductViewModel
    {
        public int ProductID { get; set; }
        public string ProductName { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public int StockQuantity { get; set; }
        public string MainImagePath { get; set; }
        public List<string> AdditionalImages { get; set; }
        public string CategoryName { get; set; }
        public string SubCategoryName { get; set; }
        public int SubCategoryID { get; set; }
        public decimal? DiscountPercentage { get; set; }
        public bool IsInUserWishlist { get; set; }
        public DateTime? AddedToWishlistAt { get; set; } // Added for wishlist functionality
        public DateTime CreatedAt { get; set; }

        // Thay đổi từ expression-bodied property sang full property với backing field
        private decimal? _discountedPrice;
        public decimal? DiscountedPrice
        {
            get
            {
                if (_discountedPrice.HasValue)
                    return _discountedPrice;

                if (DiscountPercentage.HasValue && DiscountPercentage.Value > 0 && DiscountPercentage.Value <= 100)
                {
                    return Math.Round(Price * (1 - (DiscountPercentage.Value / 100m)), 2); // Sử dụng 100m và làm tròn
                }
                return null; // Trả về null nếu không có giảm giá hợp lệ
            }
            set { _discountedPrice = value; }
        }

        public decimal EffectivePriceForFilterAndSort
        {
            get { return DiscountedPrice ?? Price; }
        }

        // Additional properties for product details
        public List<string> Features { get; set; } = new List<string>();
        public Dictionary<string, string> Specifications { get; set; } = new Dictionary<string, string>();

        private List<ReviewViewModel> _reviews = new List<ReviewViewModel>();
        public List<ReviewViewModel> Reviews
        {
            get { return _reviews; }
            set { _reviews = value; }
        }

        private double _averageRating;
        public double AverageRating
        {
            get
            {
                if (_averageRating > 0)
                    return _averageRating;

                return Reviews.Count > 0 ? Reviews.Average(r => r.Rating) : 0;
            }
            set { _averageRating = value; }
        }
        public bool HasSpecificPromotion { get; set; }
    }
}
