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
    public class FarmerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public FarmerController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Dashboard()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userName = User.Identity?.Name;
            
            // Get all products (or products for this farmer)
            var products = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Images)
                .ToListAsync();

            // Query incoming orders for farm produce
            var incomingOrders = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                        .ThenInclude(p => p.Images)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            // Load buyer details for every order
            var customerUserIds = incomingOrders.Select(o => o.CustomerId).Distinct().ToList();
            var usersList = await _userManager.Users
                .Where(u => customerUserIds.Contains(u.Id) || customerUserIds.Contains(u.UserName))
                .ToListAsync();

            var usersDict = new Dictionary<string, ApplicationUser>();
            foreach (var u in usersList)
            {
                usersDict[u.Id] = u;
                if (!string.IsNullOrEmpty(u.UserName)) usersDict[u.UserName] = u;
            }

            ViewBag.TotalProducts = products.Count;
            ViewBag.TotalSales = incomingOrders.Sum(o => o.TotalAmount);
            ViewBag.PendingOrdersCount = incomingOrders.Count(o => o.Status == "Pending");
            ViewBag.Orders = incomingOrders;
            ViewBag.CustomerUsers = usersDict;
            
            return View(products);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateOrderStatus(int orderId, string status)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order != null)
            {
                order.Status = status;
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Order #{order.Id:D5} status marked as '{status}' successfully!";
            }
            return RedirectToAction(nameof(Dashboard));
        }

        [HttpPost]
        public async Task<IActionResult> VerifyDeliveryOtp(int orderId, string enteredOtp)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order == null)
            {
                TempData["ErrorMessage"] = "Order not found.";
                return RedirectToAction(nameof(Dashboard));
            }

            if (!string.IsNullOrEmpty(order.DeliveryOtp) && order.DeliveryOtp.Trim() == enteredOtp?.Trim())
            {
                order.Status = "Delivered & Settled";
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"🔐 OTP Verified successfully! Order #{order.Id:D5} completed and ₹{order.TotalAmount:N2} credited to farmer.";
            }
            else
            {
                TempData["ErrorMessage"] = "❌ Invalid Delivery OTP! Please check the 4-digit code in the customer's dashboard.";
            }

            return RedirectToAction(nameof(Dashboard));
        }

        public IActionResult AddProduct()
        {
            return RedirectToAction("Create", "Product");
        }
    }
}
