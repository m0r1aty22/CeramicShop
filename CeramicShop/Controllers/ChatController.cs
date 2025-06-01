using CeramicShop.Data;
using Markdig;
using Microsoft.AspNetCore.Mvc;

namespace CeramicShop.Controllers
{
    public class ChatController : Controller
    {
        private readonly ApplicationDbContext _context;
        public ChatController(ApplicationDbContext context)
        {
            _context = context;
        }
        public class ChatRequest
        {
            public string userinput { get; set; }
        }

        public class AIResponse
        {
            public string reply { get; set; }
        }
        [HttpPost]
        public async Task<JsonResult> GeminiChat([FromBody] ChatRequest request)
        {
            try
            {
                var systemPrompt = "Bạn là 1 trợ lý của trang web CeramicShop, " +
                    "bạn sẽ trả lời các câu hỏi của người dùng về sản phẩm gốm sứ, " +
                    "bao gồm thông tin về sản phẩm, giá cả, cách sử dụng và bảo quản. " +
                    "Khi trả lời, hãy định dạng câu trả lời bằng Markdown để nội dung rõ ràng, dễ đọc. " +
                    "Đặc biệt, danh sách sản phẩm nên được trình bày theo dạng danh sách đánh dấu (* hoặc -), " +
                    "các tiêu đề sản phẩm nên được **in đậm**, giá nên được in nghiêng hoặc thêm dấu phân tách dễ nhìn. " +
                    "Hãy trả lời một cách thân thiện và chuyên nghiệp. ";

                var products = _context.Products
                    .Select(p => new { p.ProductName, p.Description, p.Price, p.StockQuantity })
                    .ToList();

                var categories = _context.Categories
                    .Select(c => new { c.CategoryName })
                    .ToList();

                var subcategories = _context.SubCategories
                    .Select(sc => new { sc.SubCategoryName })
                    .ToList();

                var productInfo = string.Join("\n", products.Select(p => $"- **{p.ProductName}**: {p.Description} (Giá: *{p.Price}đ*) (Tồn kho: {p.StockQuantity})"));
                var categoryInfo = string.Join(", ", categories.Select(c => c.CategoryName));
                var subcategoryInfo = string.Join(", ", subcategories.Select(sc => sc.SubCategoryName));

                var fullContext = $"Danh mục: {categoryInfo}\nLoại phụ: {subcategoryInfo}\nSản phẩm:\n{productInfo}";

                var fullPrompt = $"{systemPrompt}\n\nThông tin:\n{fullContext}\n\nCâu hỏi: {request.userinput}";

                using var httpClient = new HttpClient();
                var url = "http://103.82.36.41:5000/api/getChat";

                var data = new { userinput = fullPrompt };

                var response = await httpClient.PostAsJsonAsync(url, data);

                if (!response.IsSuccessStatusCode)
                {
                    return Json(new { success = false, message = "AI server error" });
                }

                var result = await response.Content.ReadFromJsonAsync<AIResponse>(); // tới đây là có data

                if (result == null || string.IsNullOrEmpty(result.reply))
                {
                    return Json(new { success = false, message = "Invalid response from AI server" });
                }

                string markdownResponse = result.reply;

                string htmlResponse = Markdown.ToHtml(markdownResponse);

                return Json(new { success = true, html = htmlResponse });
            }
            catch (Exception ex)
            {
                // Log ex.Message nếu có hệ thống log
                return Json(new { success = false, message = "Exception: " + ex.Message });
            }
        }

    }
}
