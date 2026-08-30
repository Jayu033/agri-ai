using AgriAI.Domain.Entities;
using AgriAI.Infrastructure.Data;
using AgriAI.Infrastructure.Identity;
using AgriAI.Web.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace AgriAI.Web.Controllers
{
    public class CheckoutController : Controller
    {
        private const string CartSessionKey = "AgriAI_ShoppingCart_Session";
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public CheckoutController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        private const string CartCookieKey = "AgriAI_Cart_Cookie";

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

        // GET: Checkout?productId=1&quantity=5 OR Checkout?isCart=true
        public async Task<IActionResult> Index(int? productId, int quantity = 1, bool isCart = false)
        {
            if (quantity < 1) quantity = 1;

            // Check if farmer is attempting to buy own product
            if (User.Identity != null && User.Identity.IsAuthenticated && productId.HasValue)
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser != null)
                {
                    var singleProd = await _context.Products.FindAsync(productId.Value);
                    if (singleProd != null && !string.IsNullOrEmpty(singleProd.FarmerId) &&
                        (singleProd.FarmerId == currentUser.Id || singleProd.FarmerId == currentUser.UserName))
                    {
                        TempData["ErrorMessage"] = "⚠️ તમે તમારો પોતાનો લિસ્ટેડ પાક ખરીદી શકતા નથી / You cannot purchase your own listed produce.";
                        return RedirectToAction("Details", "Product", new { id = productId.Value });
                    }
                }
            }

            // Check if cart checkout is requested
            var cart = GetSessionCart();
            if (isCart || (!productId.HasValue && cart.Any()))
            {
                if (!cart.Any())
                {
                    TempData["ErrorMessage"] = "Your cart is empty. Please add crops to your cart before proceeding to checkout.";
                    return Redirect("/Customer/Dashboard?tab=shopTab");
                }

                decimal cartBaseTotal = cart.Sum(i => i.TotalPrice);
                decimal cartPlatformFee = Math.Round(cartBaseTotal * 0.02m, 2);

                ViewBag.IsCart = true;
                ViewBag.CartItems = cart;
                ViewBag.Product = null;
                ViewBag.Quantity = cart.Sum(i => i.Quantity);
                ViewBag.BaseTotal = cartBaseTotal;
                ViewBag.PlatformFee = cartPlatformFee;
                ViewBag.TotalAmount = (double)(cartBaseTotal + cartPlatformFee);
                return View();
            }

            // Single product direct checkout
            if (productId.HasValue)
            {
                var product = await _context.Products
                    .Include(p => p.Category)
                    .Include(p => p.Images)
                    .FirstOrDefaultAsync(p => p.Id == productId.Value);

                if (product != null)
                {
                    if (quantity > product.StockQuantity && product.StockQuantity > 0)
                    {
                        quantity = product.StockQuantity;
                    }

                    decimal baseTotal = product.Price * quantity;
                    decimal platformFee = Math.Round(baseTotal * 0.02m, 2); // 2% platform fee

                    ViewBag.IsCart = false;
                    ViewBag.CartItems = null;
                    ViewBag.Product = product;
                    ViewBag.Quantity = quantity;
                    ViewBag.BaseTotal = baseTotal;
                    ViewBag.PlatformFee = platformFee;
                    ViewBag.TotalAmount = (double)(baseTotal + platformFee);
                    return View();
                }
            }

            // Fallback if no product and empty cart
            ViewBag.IsCart = false;
            ViewBag.CartItems = null;
            ViewBag.Product = null;
            ViewBag.Quantity = 0;
            ViewBag.BaseTotal = 0m;
            ViewBag.PlatformFee = 0m;
            ViewBag.TotalAmount = 0.0;

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ProcessPayment(int? productId, int quantity = 1, bool isCart = false, string paymentMethod = "Online", string transportType = "Direct Farm Pickup", string deliveryAddress = "")
        {
            if (quantity < 1) quantity = 1;

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "guest-user";
            string generatedOtp = new Random().Next(1000, 9999).ToString();

            if (paymentMethod == "COD")
            {
                transportType = "Direct Farm Pickup";
                deliveryAddress = "Direct Farm Pickup (Gujarat Field/Godown)";
            }
            else if (transportType == "Direct Farm Pickup" || string.IsNullOrWhiteSpace(deliveryAddress))
            {
                deliveryAddress = "Direct Farm Pickup (Gujarat Field/Godown)";
            }

            decimal baseTotal = 0m;
            var cartItems = GetSessionCart();

            // Case A: Cart Multi-Item Checkout
            if (isCart || (!productId.HasValue && cartItems.Any()))
            {
                if (!cartItems.Any())
                {
                    TempData["ErrorMessage"] = "Cart is empty.";
                    return Redirect("/Customer/Dashboard?tab=cartTab");
                }

                baseTotal = cartItems.Sum(i => i.TotalPrice);
                decimal platformFee = Math.Round(baseTotal * 0.02m, 2);
                decimal grandTotal = baseTotal + platformFee;

                var order = new Order
                {
                    CustomerId = userId,
                    OrderDate = DateTime.UtcNow,
                    TotalAmount = grandTotal,
                    Status = "Requested",
                    PaymentMethod = paymentMethod == "COD" ? "Cash on Delivery" : "Online (Escrow Protected)",
                    DeliveryOtp = generatedOtp,
                    TransportType = transportType,
                    LogisticsFee = 0m,
                    PlatformFee = platformFee,
                    ShippingAddress = deliveryAddress,
                    CancellationDeadline = DateTime.UtcNow.AddHours(3)
                };

                foreach (var item in cartItems)
                {
                    order.OrderItems.Add(new OrderItem
                    {
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice
                    });

                    // Deduct stock in DB
                    var dbProd = await _context.Products.FindAsync(item.ProductId);
                    if (dbProd != null && dbProd.StockQuantity >= item.Quantity)
                    {
                        dbProd.StockQuantity -= item.Quantity;
                    }
                }

                await _context.Orders.AddAsync(order);
                await _context.SaveChangesAsync();

                // Clear session & persistent cookie cart
                HttpContext.Session.Remove(CartSessionKey);
                Response.Cookies.Delete(CartCookieKey);

                TempData["SuccessMessage"] = $"🌾 Multi-Product Order Request #{order.Id:D5} placed successfully ({cartItems.Count} crop varieties)!";
                return RedirectToAction("OrderDetails", "Customer", new { id = order.Id });
            }

            // Case B: Single-Product Checkout
            Product? product = null;
            if (productId.HasValue)
            {
                product = await _context.Products.FindAsync(productId.Value);
            }

            baseTotal = product != null ? product.Price * quantity : 0;
            decimal singlePlatformFee = Math.Round(baseTotal * 0.02m, 2);
            decimal singleGrandTotal = baseTotal + singlePlatformFee;

            var singleOrder = new Order
            {
                CustomerId = userId,
                OrderDate = DateTime.UtcNow,
                TotalAmount = singleGrandTotal,
                Status = "Requested",
                PaymentMethod = paymentMethod == "COD" ? "Cash on Delivery" : "Online (Escrow Protected)",
                DeliveryOtp = generatedOtp,
                TransportType = transportType,
                LogisticsFee = 0m,
                PlatformFee = singlePlatformFee,
                ShippingAddress = deliveryAddress,
                CancellationDeadline = DateTime.UtcNow.AddHours(3)
            };

            if (product != null)
            {
                singleOrder.OrderItems.Add(new OrderItem
                {
                    ProductId = product.Id,
                    Quantity = quantity,
                    UnitPrice = product.Price
                });

                if (product.StockQuantity >= quantity)
                {
                    product.StockQuantity -= quantity;
                }
            }

            await _context.Orders.AddAsync(singleOrder);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"🌾 Order Request #{singleOrder.Id:D5} placed successfully! The producer will review and accept your request shortly.";
            return RedirectToAction("OrderDetails", "Customer", new { id = singleOrder.Id });
        }

        [HttpPost]
        public IActionResult PaymentSuccess(string razorpay_payment_id, string razorpay_order_id, string razorpay_signature)
        {
            TempData["SuccessMessage"] = "Payment successful! Your order has been placed.";
            return RedirectToAction("Dashboard", "Customer");
        }
    }
}
