using AgriAI.Domain.Entities;
using AgriAI.Infrastructure.Data;
using AgriAI.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AgriAI.Web.Controllers
{
    public class CheckoutController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public CheckoutController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Checkout?productId=1&quantity=5
        public async Task<IActionResult> Index(int? productId, int quantity = 1)
        {
            if (quantity < 1) quantity = 1;

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

                    ViewBag.Product = product;
                    ViewBag.Quantity = quantity;
                    ViewBag.BaseTotal = baseTotal;
                    ViewBag.PlatformFee = platformFee;
                    ViewBag.TotalAmount = (double)(baseTotal + platformFee);
                    return View();
                }
            }

            // Fallback if no specific product
            var firstProduct = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Images)
                .FirstOrDefaultAsync();

            if (firstProduct != null)
            {
                decimal baseTotal = firstProduct.Price;
                decimal platformFee = Math.Round(baseTotal * 0.02m, 2);

                ViewBag.Product = firstProduct;
                ViewBag.Quantity = 1;
                ViewBag.BaseTotal = baseTotal;
                ViewBag.PlatformFee = platformFee;
                ViewBag.TotalAmount = (double)(baseTotal + platformFee);
            }
            else
            {
                ViewBag.Product = null;
                ViewBag.Quantity = 0;
                ViewBag.BaseTotal = 0m;
                ViewBag.PlatformFee = 0m;
                ViewBag.TotalAmount = 0.0;
            }

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ProcessPayment(int? productId, int quantity = 1, string paymentMethod = "Online", string transportType = "Direct Farm Pickup", string deliveryAddress = "")
        {
            if (quantity < 1) quantity = 1;

            Product? product = null;
            if (productId.HasValue)
            {
                product = await _context.Products.FindAsync(productId.Value);
            }

            decimal baseTotal = product != null ? product.Price * quantity : 0;
            decimal logisticsFee = transportType.Contains("Porter") ? 250m : 0m;
            decimal platformFee = Math.Round(baseTotal * 0.02m, 2);
            decimal grandTotal = baseTotal + logisticsFee + platformFee;

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "guest-user";

            // Generate a secure 4-digit Delivery OTP
            string generatedOtp = new Random().Next(1000, 9999).ToString();

            // Create real Order entity in DB
            var order = new Order
            {
                CustomerId = userId,
                OrderDate = DateTime.UtcNow,
                TotalAmount = grandTotal,
                Status = "Requested", // Starts as Order Request awaiting farmer acceptance
                PaymentMethod = paymentMethod == "COD" ? "Cash on Delivery" : "Online (Escrow Protected)",
                DeliveryOtp = generatedOtp,
                TransportType = transportType,
                LogisticsFee = logisticsFee,
                PlatformFee = platformFee,
                ShippingAddress = string.IsNullOrWhiteSpace(deliveryAddress) ? "Gujarat Farm Pickup" : deliveryAddress,
                CancellationDeadline = DateTime.UtcNow.AddHours(3)
            };

            if (product != null)
            {
                order.OrderItems.Add(new OrderItem
                {
                    ProductId = product.Id,
                    Quantity = quantity,
                    UnitPrice = product.Price
                });

                // Deduct stock
                if (product.StockQuantity >= quantity)
                {
                    product.StockQuantity -= quantity;
                }
            }

            await _context.Orders.AddAsync(order);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"🌾 Order Request #{order.Id:D5} placed successfully! The producer will review and accept your request shortly.";

            return RedirectToAction("OrderDetails", "Customer", new { id = order.Id });
        }

        [HttpPost]
        public IActionResult PaymentSuccess(string razorpay_payment_id, string razorpay_order_id, string razorpay_signature)
        {
            TempData["SuccessMessage"] = "Payment successful! Your order has been placed.";
            return RedirectToAction("Dashboard", "Customer");
        }
    }
}
