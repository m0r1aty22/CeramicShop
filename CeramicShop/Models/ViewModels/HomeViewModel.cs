using System.Collections.Generic;

namespace CeramicShop.Models.ViewModels
{
    public class HomeViewModel
    {
        public List<Category> Categories { get; set; }
        public List<ProductViewModel> FeaturedProducts { get; set; }
        public List<ProductViewModel> NewArrivals { get; set; }
        public Dictionary<int, List<SubCategory>> SubCategoriesByCategory { get; set; }
        public List<int> UserWishlistProductIds { get; internal set; }
    }
}