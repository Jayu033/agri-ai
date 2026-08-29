using AgriAI.Infrastructure.Data;
using AgriAI.Infrastructure.Identity;
using AgriAI.Web.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AgriAI.Web.Controllers
{
    public class CartController : Controller
    {
        private const string CartSessionKey = "AgriAI_ShoppingCart_Session";
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public CartController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        private const string CartCookieKey = "AgriAI_Cart_Cookie";

        // Helper: Get cart from session (or fallback to persistent cookie)
        private List<CartItemViewModel> GetSessionCart()
        {
            var cartJson = HttpContext.Session.GetString(CartSessionKey);
            if (string.IsNullOrEmpty(cartJson) && HttpContext.Request.Cookies.TryGetValue(CartCookieKey, out var cookieJson))
            {
                cartJson = cookieJson;
                if (!string.IsNullOrEmpty(cartJson))
                {
                    HttpContext.Session.SetString(CartSessionKey, cartJson);
                }
            }

            if (string.IsNullOrEmpty(cartJson))
            {
                return new List<CartItemViewModel>();
            }

            try
            {
                return JsonSerializer.Deserialize<List<CartItemViewModel>>(cartJson) ?? new List<CartItemViewModel>();
            }
            catch
            {
                return new List<CartItemViewModel>();
            }
        }

        // Helper: Save cart to session AND persistent cookie
        private void SaveSessionCart(List<CartItemViewModel> cart)
        {
            var cartJson = JsonSerializer.Serialize(cart);
            HttpContext.Session.SetString(CartSessionKey, cartJson);

            Response.Cookies.Append(CartCookieKey, cartJson, new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddDays(7),
                IsEssential = true,
                SameSite = SameSiteMode.Lax,
                HttpOnly = false
            });
        }

        [HttpGet]
        public IActionResult Index()
        {
            return Redirect("/Customer/Dashboard?tab=cartTab");
        }

        [HttpGet]
        public IActionResult GetCartCount()
        {
            var cart = GetSessionCart();
            int count = cart.Sum(i => i.Quantity);
            return Json(new { count });
        }

        [HttpGet]
        public IActionResult GetCart()
        {
            var cart = GetSessionCart();
            var summary = new CartSummaryViewModel
            {
                Items = cart
            };
            return Json(new
            {
                success = true,
                items = cart,
                totalItemsCount = summary.TotalItemsCount,
                baseTotal = summary.BaseTotal,
                platformFee = summary.PlatformFee,
                grandTotal = summary.GrandTotal
            });
        }

        [HttpPost]
        public async Task<IActionResult> AddToCart(int productId, int quantity = 1)
        {
            if (quantity < 1) quantity = 1;

            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Id == productId);

            if (product == null)
            {
                return Json(new { success = false, message = "Product not found." });
            }

            if (product.StockQuantity <= 0)
            {
                return Json(new { success = false, message = "Sorry, this product is currently out of stock." });
            }

            var cart = GetSessionCart();
            var existingItem = cart.FirstOrDefault(i => i.ProductId == productId);

            if (existingItem != null)
            {
                int newQty = existingItem.Quantity + quantity;
                if (newQty > product.StockQuantity)
                {
                    newQty = product.StockQuantity;
                }
                existingItem.Quantity = newQty;
                existingItem.StockQuantity = product.StockQuantity;
                existingItem.UnitPrice = product.Price;
            }
            else
            {
                if (quantity > product.StockQuantity)
                {
                    quantity = product.StockQuantity;
                }

                string? farmerName = null;
                if (!string.IsNullOrEmpty(product.FarmerId))
                {
                    var farmer = await _userManager.FindByIdAsync(product.FarmerId) ?? await _userManager.FindByNameAsync(product.FarmerId);
                    if (farmer != null)
                    {
                        farmerName = (!string.IsNullOrEmpty(farmer.FirstName) || !string.IsNullOrEmpty(farmer.LastName))
                            ? $"{farmer.FirstName} {farmer.LastName}".Trim()
                            : farmer.UserName;
                    }
                }

                cart.Add(new CartItemViewModel
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    ImageUrl = product.Images?.FirstOrDefault()?.ImageUrl,
                    CategoryName = product.Category?.Name ?? "Farm Produce",
                    UnitPrice = product.Price,
                    Quantity = quantity,
                    StockQuantity = product.StockQuantity,
                    FarmerId = product.FarmerId,
                    FarmerName = farmerName ?? "Verified Gujarat Producer"
                });
            }

            SaveSessionCart(cart);

            int totalCount = cart.Sum(i => i.Quantity);
            return Json(new
            {
                success = true,
                count = totalCount,
                message = $"🌾 {quantity} Kg of {product.Name} added to your cart!"
            });
        }

        [HttpPost]
        public IActionResult UpdateQuantity(int productId, int quantity)
        {
            var cart = GetSessionCart();
            var item = cart.FirstOrDefault(i => i.ProductId == productId);

            if (item != null)
            {
                if (quantity <= 0)
                {
                    cart.Remove(item);
                }
                else
                {
                    if (item.StockQuantity > 0 && quantity > item.StockQuantity)
                    {
                        quantity = item.StockQuantity;
                    }
                    item.Quantity = quantity;
                }
                SaveSessionCart(cart);
            }

            var summary = new CartSummaryViewModel { Items = cart };
            return Json(new
            {
                success = true,
                items = cart,
                totalItemsCount = summary.TotalItemsCount,
                baseTotal = summary.BaseTotal,
                platformFee = summary.PlatformFee,
                grandTotal = summary.GrandTotal
            });
        }

        [HttpPost]
        public IActionResult RemoveItem(int productId)
        {
            var cart = GetSessionCart();
            var item = cart.FirstOrDefault(i => i.ProductId == productId);

            if (item != null)
            {
                cart.Remove(item);
                SaveSessionCart(cart);
            }

            var summary = new CartSummaryViewModel { Items = cart };
            return Json(new
            {
                success = true,
                items = cart,
                totalItemsCount = summary.TotalItemsCount,
                baseTotal = summary.BaseTotal,
                platformFee = summary.PlatformFee,
                grandTotal = summary.GrandTotal,
                message = "Item removed from cart."
            });
        }

        [HttpPost]
        public IActionResult ClearCart()
        {
            HttpContext.Session.Remove(CartSessionKey);
            Response.Cookies.Delete(CartCookieKey);
            return Json(new { success = true, count = 0, message = "Cart cleared successfully." });
        }
    }
}
