using AgriAI.Domain.Entities;
using AgriAI.Infrastructure.Data;
using AgriAI.Infrastructure.Identity;
using AgriAI.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AgriAI.Web.Controllers
{
    [Authorize]
    public class CustomerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public CustomerController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Dashboard()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userName = User.Identity?.Name;

            var products = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Images)
                .ToListAsync();

            // Load real customer orders
            var customerOrders = await _context.Orders
                .Where(o => o.CustomerId == userId || o.CustomerId == userName || o.CustomerId == "guest-user")
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                        .ThenInclude(p => p.Images)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                        .ThenInclude(p => p.Category)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            var activeOrders = customerOrders
                .Where(o => o.Status == "Requested" || o.Status == "Accepted" || o.Status == "Ready for Pickup" || o.Status == "In Transit")
                .ToList();

            var completedOrders = customerOrders
                .Where(o => o.Status.Contains("Delivered") || o.Status.Contains("Settled") || o.Status == "Cancelled" || o.Status == "Declined")
                .ToList();

            int totalKgPurchased = customerOrders
                .Where(o => o.Status != "Cancelled" && o.Status != "Declined")
                .SelectMany(o => o.OrderItems)
                .Sum(oi => oi.Quantity);

            decimal totalSpent = customerOrders
                .Where(o => o.Status != "Cancelled" && o.Status != "Declined")
                .Sum(o => o.TotalAmount);

            // Load shopping cart items from session or persistent cookie
            var cartJson = HttpContext.Session.GetString("AgriAI_ShoppingCart_Session");
            if (string.IsNullOrEmpty(cartJson) && HttpContext.Request.Cookies.TryGetValue("AgriAI_Cart_Cookie", out var cookieJson))
            {
                cartJson = cookieJson;
                if (!string.IsNullOrEmpty(cartJson))
                {
                    HttpContext.Session.SetString("AgriAI_ShoppingCart_Session", cartJson);
                }
            }

            var cartItems = new List<CartItemViewModel>();
            if (!string.IsNullOrEmpty(cartJson))
            {
                try
                {
                    cartItems = System.Text.Json.JsonSerializer.Deserialize<List<CartItemViewModel>>(cartJson) ?? new List<CartItemViewModel>();
                }
                catch { }
            }
            var cartSummary = new CartSummaryViewModel { Items = cartItems };

            ViewBag.Orders = customerOrders;
            ViewBag.ActiveOrders = activeOrders;
            ViewBag.CompletedOrders = completedOrders;
            ViewBag.TotalOrdersCount = customerOrders.Count;
            ViewBag.ActiveOrdersCount = activeOrders.Count;
            ViewBag.CompletedOrdersCount = completedOrders.Count;
            ViewBag.TotalSpent = totalSpent;
            ViewBag.TotalKgPurchased = totalKgPurchased;
            ViewBag.CartSummary = cartSummary;

            return View(products);
        }

        [HttpGet]
        public async Task<IActionResult> TotalPurchases()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userName = User.Identity?.Name;

            var customerOrders = await _context.Orders
                .Where(o => o.CustomerId == userId || o.CustomerId == userName || o.CustomerId == "guest-user")
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                        .ThenInclude(p => p.Images)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                        .ThenInclude(p => p.Category)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            var successfulOrders = customerOrders
                .Where(o => o.Status != "Cancelled" && o.Status != "Declined")
                .ToList();

            decimal totalSpent = successfulOrders.Sum(o => o.TotalAmount);
            int totalKg = successfulOrders.SelectMany(o => o.OrderItems).Sum(oi => oi.Quantity);
            decimal estimatedSavings = Math.Round(totalSpent * 0.18m, 2); // 18% direct-from-farm APMC savings

            ViewBag.TotalSpent = totalSpent;
            ViewBag.TotalKg = totalKg;
            ViewBag.EstimatedSavings = estimatedSavings;
            ViewBag.SuccessfulOrdersCount = successfulOrders.Count;
            ViewBag.TotalOrdersCount = customerOrders.Count;
            ViewBag.AllOrders = customerOrders;

            return View(customerOrders);
        }

        [HttpPost]
        public async Task<IActionResult> VerifySecurityGate(int orderId, string password)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Json(new { success = false, message = "User session expired. Please log in again." });
            }

            var isPasswordValid = await _userManager.CheckPasswordAsync(user, password);
            if (!isPasswordValid)
            {
                return Json(new { success = false, message = "❌ Incorrect password! Please enter your valid account password to unlock confidential order details & OTP." });
            }

            HttpContext.Session.SetString($"Unlocked_Order_{orderId}", "true");
            return Json(new { success = true, redirectUrl = Url.Action("OrderDetails", new { id = orderId, from = "active" }) });
        }

        [HttpGet]
        public async Task<IActionResult> OrderDetails(int id, string? from, string? returnUrl)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userName = User.Identity?.Name;

            var order = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                        .ThenInclude(p => p.Images)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                        .ThenInclude(p => p.Category)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
            {
                TempData["ErrorMessage"] = "Order not found.";
                return RedirectToAction(nameof(Dashboard));
            }

            // Verify order ownership
            if (!User.IsInRole("Admin") && order.CustomerId != userId && order.CustomerId != userName && order.CustomerId != "guest-user")
            {
                TempData["ErrorMessage"] = "You do not have authorization to view this order.";
                return RedirectToAction(nameof(Dashboard));
            }

            // Load Farmer contact info if associated
            var farmerId = order.OrderItems.FirstOrDefault()?.Product?.FarmerId;
            ApplicationUser? farmerUser = null;
            if (!string.IsNullOrEmpty(farmerId))
            {
                farmerUser = await _userManager.FindByIdAsync(farmerId) ?? await _userManager.FindByNameAsync(farmerId);
            }
            ViewBag.FarmerUser = farmerUser;
            ViewBag.From = from;
            ViewBag.ReturnUrl = returnUrl;

            return View(order);
        }

        [HttpPost]
        public async Task<IActionResult> CancelOrder(int orderId, string? reason)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userName = User.Identity?.Name;

            var order = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
            {
                TempData["ErrorMessage"] = "Order not found.";
                return RedirectToAction(nameof(Dashboard));
            }

            // Verify order ownership
            if (!User.IsInRole("Admin") && order.CustomerId != userId && order.CustomerId != userName && order.CustomerId != "guest-user")
            {
                TempData["ErrorMessage"] = "You do not have authorization to cancel this order.";
                return RedirectToAction(nameof(Dashboard));
            }

            // Cancellation is allowed if status is "Requested" OR ("Accepted" and within the 3-hour cancellation window)
            bool isWindowOpen = order.Status == "Requested" || 
                (order.Status == "Accepted" && (!order.CancellationDeadline.HasValue || DateTime.UtcNow <= order.CancellationDeadline.Value));

            if (!isWindowOpen)
            {
                TempData["ErrorMessage"] = "❌ Cancellation window has expired! This order is locked for harvest and dispatch.";
                return RedirectToAction("OrderDetails", new { id = orderId });
            }

            order.Status = "Cancelled";
            order.IsCustomerCancelled = true;
            order.CustomerCancelReason = string.IsNullOrWhiteSpace(reason) ? "Cancelled by buyer within review window" : reason.Trim();

            // Restore Stock
            foreach (var item in order.OrderItems)
            {
                if (item.Product != null)
                {
                    item.Product.StockQuantity += item.Quantity;
                }
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"🚫 Order #{order.Id:D5} has been cancelled successfully. 100% refund of ₹{order.TotalAmount:N2} will be credited back instantly.";
            return RedirectToAction("OrderDetails", new { id = orderId });
        }
    }
}
