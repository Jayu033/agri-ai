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

            var ownsProduct = order.OrderItems.Any(oi => oi.Product != null && (oi.Product.FarmerId == userId || (!string.IsNullOrEmpty(userName) && oi.Product.FarmerId == userName)));
            if (!ownsProduct && !User.IsInRole("Admin"))
            {
                TempData["ErrorMessage"] = "Unauthorized access to order details.";
                return RedirectToAction(nameof(Dashboard));
            }

            // Load buyer profile
            ApplicationUser? customerUser = null;
            if (!string.IsNullOrEmpty(order.CustomerId))
            {
                customerUser = await _userManager.FindByIdAsync(order.CustomerId) ?? await _userManager.FindByNameAsync(order.CustomerId);
            }

            ViewBag.CustomerUser = customerUser;
            return View(order);
        }

        private IActionResult RedirectToFarmerOrder(int orderId, string? returnUrl = null)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            var referer = Request.Headers["Referer"].ToString();
            if (!string.IsNullOrEmpty(referer) && referer.Contains("OrderDetails", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction(nameof(OrderDetails), new { id = orderId });
            }
            return RedirectToAction(nameof(Dashboard));
        }

        [HttpPost]
        public async Task<IActionResult> AcceptOrder(int orderId, string? returnUrl = null)
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
                return RedirectToFarmerOrder(orderId, returnUrl);
            }

            // Verify order belongs to this farmer
            var ownsProduct = order.OrderItems.Any(oi => oi.Product != null && (oi.Product.FarmerId == userId || (!string.IsNullOrEmpty(userName) && oi.Product.FarmerId == userName)));
            if (!ownsProduct && !User.IsInRole("Admin"))
            {
                TempData["ErrorMessage"] = "Unauthorized action on this order.";
                return RedirectToFarmerOrder(orderId, returnUrl);
            }

            order.Status = "Accepted";
            order.FarmerAcceptanceAt = DateTime.UtcNow;
            order.CancellationDeadline = DateTime.UtcNow.AddHours(3);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"✅ Order #{order.Id:D5} accepted! The customer has a 3-hour review window before the order is locked for harvest dispatch.";
            return RedirectToFarmerOrder(orderId, returnUrl);
        }

        [HttpPost]
        public async Task<IActionResult> DeclineOrder(int orderId, string? reason, string? returnUrl = null)
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
                return RedirectToFarmerOrder(orderId, returnUrl);
            }

            // Verify order belongs to this farmer
            var ownsProduct = order.OrderItems.Any(oi => oi.Product != null && (oi.Product.FarmerId == userId || (!string.IsNullOrEmpty(userName) && oi.Product.FarmerId == userName)));
            if (!ownsProduct && !User.IsInRole("Admin"))
            {
                TempData["ErrorMessage"] = "Unauthorized action on this order.";
                return RedirectToFarmerOrder(orderId, returnUrl);
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
            return RedirectToFarmerOrder(orderId, returnUrl);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateOrderStatus(int orderId, string status, string? returnUrl = null)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order != null)
            {
                order.Status = status;
                order.CancellationDeadline = DateTime.UtcNow; // Lock review window immediately on dispatch / ready
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"✅ Order #{order.Id:D5} status marked as '{status}' successfully!";
            }
            return RedirectToFarmerOrder(orderId, returnUrl);
        }

        [HttpPost]
        public async Task<IActionResult> VerifyDeliveryOtp(int orderId, string enteredOtp, string? returnUrl = null)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order == null)
            {
                TempData["ErrorMessage"] = "Order not found.";
                return RedirectToFarmerOrder(orderId, returnUrl);
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

            return RedirectToFarmerOrder(orderId, returnUrl);
        }

        [HttpPost]
        public async Task<IActionResult> UploadBiltyAndDispatch(int orderId, string cargoServiceName, string biltyNumber, IFormFile? biltyFile, string? returnUrl = null)
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
                return RedirectToFarmerOrder(orderId, returnUrl);
            }

            var ownsProduct = order.OrderItems.Any(oi => oi.Product != null && (oi.Product.FarmerId == userId || (!string.IsNullOrEmpty(userName) && oi.Product.FarmerId == userName)));
            if (!ownsProduct && !User.IsInRole("Admin"))
            {
                TempData["ErrorMessage"] = "Unauthorized action on this order.";
                return RedirectToFarmerOrder(orderId, returnUrl);
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
            order.CancellationDeadline = DateTime.UtcNow; // Lock review window immediately on dispatch

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"🚚 Bilty #{order.BiltyNumber} uploaded! Order #{order.Id:D5} marked as Dispatched via {order.CargoServiceName}.";
            return RedirectToFarmerOrder(orderId, returnUrl);
        }

        public IActionResult AddProduct()
        {
            return RedirectToAction("Create", "Product");
        }

        // GET: Farmer/EditProduct/5
        [HttpGet]
        public async Task<IActionResult> EditProduct(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userName = User.Identity?.Name;

            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
            {
                TempData["ErrorMessage"] = "Harvest produce not found.";
                return RedirectToAction(nameof(Dashboard));
            }

            // Ensure the farmer owns this product or is Admin
            if (!User.IsInRole("Admin"))
            {
                if (product.FarmerId != userId && product.FarmerId != userName)
                {
                    TempData["ErrorMessage"] = "Unauthorized. You can only edit your own listed produce.";
                    return RedirectToAction(nameof(Dashboard));
                }
            }

            var categories = await _context.Categories.ToListAsync();
            ViewData["CategoryId"] = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(categories, "Id", "Name", product.CategoryId);
            return View(product);
        }

        // POST: Farmer/EditProduct/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProduct(int id, [Bind("Id,Name,Description,Price,StockQuantity,CategoryId,FarmerId")] Product product, IFormFile? imageFile)
        {
            if (id != product.Id)
            {
                return NotFound();
            }

            var existingProduct = await _context.Products
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (existingProduct == null)
            {
                TempData["ErrorMessage"] = "Harvest produce not found.";
                return RedirectToAction(nameof(Dashboard));
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userName = User.Identity?.Name;

            if (!User.IsInRole("Admin"))
            {
                if (existingProduct.FarmerId != userId && existingProduct.FarmerId != userName)
                {
                    TempData["ErrorMessage"] = "Unauthorized. You can only edit your own listed produce.";
                    return RedirectToAction(nameof(Dashboard));
                }
            }

            ModelState.Remove("Category");
            ModelState.Remove("Images");
            ModelState.Remove("Reviews");
            ModelState.Remove("FarmerId");

            if (ModelState.IsValid)
            {
                try
                {
                    existingProduct.Name = product.Name;
                    existingProduct.Description = product.Description;
                    existingProduct.Price = product.Price;
                    existingProduct.StockQuantity = product.StockQuantity;
                    existingProduct.CategoryId = product.CategoryId;

                    if (imageFile != null && imageFile.Length > 0)
                    {
                        var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "products");
                        if (!Directory.Exists(uploadsFolder))
                        {
                            Directory.CreateDirectory(uploadsFolder);
                        }

                        var uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(imageFile.FileName);
                        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await imageFile.CopyToAsync(fileStream);
                        }

                        var existingImage = existingProduct.Images.FirstOrDefault();
                        if (existingImage != null)
                        {
                            existingImage.ImageUrl = "/images/products/" + uniqueFileName;
                        }
                        else
                        {
                            existingProduct.Images.Add(new ProductImage
                            {
                                ImageUrl = "/images/products/" + uniqueFileName,
                                IsPrimary = true
                            });
                        }
                    }

                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = $"🌾 Harvest produce '{existingProduct.Name}' updated successfully!";
                    return RedirectToAction(nameof(Dashboard));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Error updating product: " + ex.Message);
                }
            }

            var categories = await _context.Categories.ToListAsync();
            ViewData["CategoryId"] = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(categories, "Id", "Name", product.CategoryId);
            return View(product);
        }
    }
}
