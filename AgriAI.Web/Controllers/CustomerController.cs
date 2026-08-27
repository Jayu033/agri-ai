using AgriAI.Domain.Entities;
using AgriAI.Infrastructure.Data;
using AgriAI.Infrastructure.Identity;
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
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            ViewBag.Orders = customerOrders;
            ViewBag.TotalOrdersCount = customerOrders.Count;
            ViewBag.TotalSpent = customerOrders.Sum(o => o.TotalAmount);

            return View(products);
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
            return Json(new { success = true, redirectUrl = Url.Action("OrderDetails", new { id = orderId }) });
        }

        [HttpGet]
        public async Task<IActionResult> OrderDetails(int id)
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

            return View(order);
        }
    }
}
