using System;

namespace CeramicShop.Models.ViewModels
{
    public class WishlistItemViewModel
    {
        public int WishlistItemID { get; set; }
        public int ProductID { get; set; }
        public string ProductName { get; set; }
        public decimal Price { get; set; }
        public string MainImagePath { get; set; }
        public DateTime AddedAt { get; set; }
        public int StockQuantity { get; set; }
        public decimal DiscountPercentage { get; set; }

        private decimal? _discountedPrice;
        public decimal? DiscountedPrice
        {
            get
            {
                if (_discountedPrice.HasValue)
                    return _discountedPrice;

                return DiscountPercentage > 0
                    ? Math.Round(Price - (Price * DiscountPercentage / 100), 2)
                    : null;
            }
            set { _discountedPrice = value; }
        }
    }
}
