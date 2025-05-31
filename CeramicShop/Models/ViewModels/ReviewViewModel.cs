using System;

namespace CeramicShop.Models.ViewModels
{
    public class ReviewViewModel
    {
        public int ReviewID { get; set; }
        public string UserName { get; set; }
        public int Rating { get; set; }
        public string Comment { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
