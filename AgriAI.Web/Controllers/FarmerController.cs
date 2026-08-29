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
        private readonly IWebHostEnvironment _environment;

        public FarmerController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IWebHostEnvironment environment)
        {
            _context = context;
            _userManager = userManager;
            _environment = environment;
        }

        public async Task<IActionResult> Dashboard()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userName = User.Identity?.Name;
            
            // Get only crops listed by THIS logged-in farmer
            var products = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Images)
                .Where(p => p.FarmerId == userId || (!string.IsNullOrEmpty(userName) && p.FarmerId == userName))
                .ToListAsync();

            var myProductIds = products.Select(p => p.Id).ToList();

            // Query incoming orders containing crops of THIS farmer only
            var incomingOrders = myProductIds.Any() 
                ? await _context.Orders
                    .Include(o => o.OrderItems)
                        .ThenInclude(oi => oi.Product)
                            .ThenInclude(p => p.Images)
                    .Where(o => o.OrderItems.Any(oi => myProductIds.Contains(oi.ProductId)))
                    .OrderByDescending(o => o.OrderDate)
                    .ToListAsync()
                : new List<Order>();

            // Calculate total farm revenue strictly for THIS farmer's sold items
            decimal farmerRevenue = 0m;
            if (myProductIds.Any())
            {
                farmerRevenue = incomingOrders
                    .Where(o => o.Status == "Delivered & Settled" || o.Status == "Completed" || o.Status == "Ready for Pickup" || o.Status == "In Transit")
                    .SelectMany(o => o.OrderItems)
                    .Where(oi => myProductIds.Contains(oi.ProductId))
                    .Sum(oi => oi.UnitPrice * oi.Quantity);
            }

            // Load buyer details for every order
            var customerUserIds = incomingOrders.Select(o => o.CustomerId).Distinct().ToList();
            var usersList = await _userManager.Users
                .Where(u => customerUserIds.Contains(u.Id) || (u.UserName != null && customerUserIds.Contains(u.UserName)))
                .ToListAsync();

            var usersDict = new Dictionary<string, ApplicationUser>();
            foreach (var u in usersList)
            {
                usersDict[u.Id] = u;
                if (!string.IsNullOrEmpty(u.UserName)) usersDict[u.UserName] = u;
            }

            ViewBag.TotalProducts = products.Count;
            ViewBag.TotalSales = farmerRevenue;
            ViewBag.PendingOrdersCount = incomingOrders.Count(o => o.Status == "Pending" || o.Status == "Requested");
            ViewBag.Orders = incomingOrders;
            ViewBag.CustomerUsers = usersDict;
            
            return View(products);
        }

        [HttpPost]
        public async Task<IActionResult> AcceptOrder(int orderId)
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

            // Verify order belongs to this farmer
            var ownsProduct = order.OrderItems.Any(oi => oi.Product != null && (oi.Product.FarmerId == userId || (!string.IsNullOrEmpty(userName) && oi.Product.FarmerId == userName)));
            if (!ownsProduct && !User.IsInRole("Admin"))
            {
                TempData["ErrorMessage"] = "Unauthorized action on this order.";
                return RedirectToAction(nameof(Dashboard));
            }

            order.Status = "Accepted";
            order.FarmerAcceptanceAt = DateTime.UtcNow;
            order.CancellationDeadline = DateTime.UtcNow.AddHours(3);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"✅ Order #{order.Id:D5} accepted! The customer has a 3-hour review window before the order is locked for harvest dispatch.";
            return RedirectToAction(nameof(Dashboard));
        }

        [HttpPost]
        public async Task<IActionResult> DeclineOrder(int orderId, string? reason)
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

            // Verify order belongs to this farmer
            var ownsProduct = order.OrderItems.Any(oi => oi.Product != null && (oi.Product.FarmerId == userId || (!string.IsNullOrEmpty(userName) && oi.Product.FarmerId == userName)));
            if (!ownsProduct && !User.IsInRole("Admin"))
            {
                TempData["ErrorMessage"] = "Unauthorized action on this order.";
                return RedirectToAction(nameof(Dashboard));
            }

            order.Status = "Declined";
            order.IsFarmerDeclined = true;
            order.FarmerDeclineReason = string.IsNullOrWhiteSpace(reason) ? "Produce unavailable / logistics constraint" : reason.Trim();

            // Restore Stock
            foreach (var item in order.OrderItems)
            {
                if (item.Product != null)
                {
                    item.Product.StockQuantity += item.Quantity;
                }
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"❌ Order #{order.Id:D5} declined. Customer has been notified and ₹{order.TotalAmount:N2} will be refunded.";
            return RedirectToAction(nameof(Dashboard));
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

        [HttpPost]
        public async Task<IActionResult> UploadBiltyAndDispatch(int orderId, string cargoServiceName, string biltyNumber, IFormFile? biltyFile)
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

            var ownsProduct = order.OrderItems.Any(oi => oi.Product != null && (oi.Product.FarmerId == userId || (!string.IsNullOrEmpty(userName) && oi.Product.FarmerId == userName)));
            if (!ownsProduct && !User.IsInRole("Admin"))
            {
                TempData["ErrorMessage"] = "Unauthorized action on this order.";
                return RedirectToAction(nameof(Dashboard));
            }

            if (biltyFile != null && biltyFile.Length > 0)
            {
                var uploadsDir = Path.Combine(_environment.WebRootPath, "uploads", "bilty");
                if (!Directory.Exists(uploadsDir))
                {
                    Directory.CreateDirectory(uploadsDir);
                }

                var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(biltyFile.FileName)}";
                var filePath = Path.Combine(uploadsDir, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await biltyFile.CopyToAsync(stream);
                }

                order.BiltyImageUrl = $"/uploads/bilty/{fileName}";
            }

            order.CargoServiceName = string.IsNullOrWhiteSpace(cargoServiceName) ? "GSRTC ST Bus Parcel" : cargoServiceName.Trim();
            order.BiltyNumber = string.IsNullOrWhiteSpace(biltyNumber) ? ("LR-" + new Random().Next(10000, 99999)) : biltyNumber.Trim();
            order.Status = "In Transit";
            order.DispatchedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"🚚 Bilty #{order.BiltyNumber} uploaded! Order #{order.Id:D5} marked as Dispatched via {order.CargoServiceName}.";
            return RedirectToAction(nameof(Dashboard));
        }

        public IActionResult AddProduct()
        {
            return RedirectToAction("Create", "Product");
        }
    }
}
