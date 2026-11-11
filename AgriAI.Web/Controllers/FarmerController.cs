using AgriAI.Web.Services;
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
    public class FarmerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _environment;
        private readonly IRazorpayPaymentService _razorpayService;
        private readonly IWhatsAppNotificationService _whatsAppService;
        private readonly ILogger<FarmerController> _logger;

        public FarmerController(
            ApplicationDbContext context, 
            UserManager<ApplicationUser> userManager, 
            IWebHostEnvironment environment, 
            IRazorpayPaymentService razorpayService,
            IWhatsAppNotificationService whatsAppService,
            ILogger<FarmerController> logger)
        {
            _context = context;
            _userManager = userManager;
            _environment = environment;
            _razorpayService = razorpayService;
            _whatsAppService = whatsAppService;
            _logger = logger;
        }

        public async Task<IActionResult> Dashboard(string? tab)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userName = User.Identity?.Name;

            var currentUser = await _userManager.GetUserAsync(User);
            ViewBag.ProfilePicturePath = currentUser?.ProfilePicturePath;
            ViewBag.ActiveTab = string.IsNullOrEmpty(tab) ? "overview" : tab.Trim();
            
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
                    .Include(o => o.Payment)
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

            var soldQuantities = incomingOrders
                .Where(o => o.Status != "Cancelled" && o.Status != "Declined")
                .SelectMany(o => o.OrderItems)
                .Where(oi => myProductIds.Contains(oi.ProductId))
                .GroupBy(oi => oi.ProductId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

            ViewBag.SoldQuantities = soldQuantities;
            ViewBag.TotalProducts = products.Count;
            ViewBag.TotalSales = farmerRevenue;
            ViewBag.PendingOrdersCount = incomingOrders.Count(o => o.Status == "Pending" || o.Status == "Requested");
            ViewBag.Orders = incomingOrders;
            ViewBag.CustomerUsers = usersDict;

            // Load orders PLACED BY this farmer (Sourced / Purchased from other farmers)
            var purchasedOrders = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                        .ThenInclude(p => p.Images)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                        .ThenInclude(p => p.Category)
                .Include(o => o.Payment)
                .Where(o => o.CustomerId == userId || (!string.IsNullOrEmpty(userName) && o.CustomerId == userName))
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            var sellerFarmerIds = purchasedOrders
                .SelectMany(o => o.OrderItems)
                .Select(oi => oi.Product?.FarmerId)
                .Where(fId => !string.IsNullOrEmpty(fId))
                .Distinct()
                .ToList();

            var sellerUsersList = await _userManager.Users
                .Where(u => sellerFarmerIds.Contains(u.Id) || (u.UserName != null && sellerFarmerIds.Contains(u.UserName)))
                .ToListAsync();

            var sellerDict = new Dictionary<string, ApplicationUser>();
            foreach (var u in sellerUsersList)
            {
                sellerDict[u.Id] = u;
                if (!string.IsNullOrEmpty(u.UserName)) sellerDict[u.UserName] = u;
            }

            // Session Cart
            var cartSessionJson = HttpContext.Session.GetString("AgriAI_ShoppingCart_Session");
            if (string.IsNullOrEmpty(cartSessionJson) && HttpContext.Request.Cookies.TryGetValue("AgriAI_Cart_Cookie", out var cookieJson))
            {
                cartSessionJson = cookieJson;
            }

            var cartItems = new List<CartItemViewModel>();
            if (!string.IsNullOrEmpty(cartSessionJson))
            {
                try
                {
                    cartItems = System.Text.Json.JsonSerializer.Deserialize<List<CartItemViewModel>>(cartSessionJson) ?? new List<CartItemViewModel>();
                }
                catch { }
            }

            var cartSummary = new CartSummaryViewModel { Items = cartItems };

            ViewBag.PurchasedOrders = purchasedOrders;
            ViewBag.SellerUsers = sellerDict;
            ViewBag.CartSummary = cartSummary;
            
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
                .Include(o => o.Payment)
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
                .Include(o => o.Payment)
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
            order.FarmerAcceptanceAt ??= DateTime.UtcNow;
            if (!order.CancellationDeadline.HasValue)
            {
                order.CancellationDeadline = order.OrderDate.AddHours(3);
            }
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"✅ Order #{order.Id:D5} confirmed! 3-hour harvest review period active.";
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

            // Strict 3-Hour window check: once 3 hours pass, cancellation is permanently locked
            if (order.CancellationDeadline.HasValue && DateTime.UtcNow > order.CancellationDeadline.Value)
            {
                TempData["ErrorMessage"] = "❌ The 3-hour cancellation window has expired! This order is locked for harvest fulfillment.";
                return RedirectToFarmerOrder(orderId, returnUrl);
            }

            order.Status = "Declined";
            order.IsFarmerDeclined = true;
            order.FarmerDeclineReason = string.IsNullOrWhiteSpace(reason) ? "Produce unavailable / harvest constraint" : reason.Trim();

            // Process Razorpay Escrow Refund if paid online
            if (order.Payment != null && !string.IsNullOrEmpty(order.Payment.RazorpayPaymentId))
            {
                var (refundOk, refundId, refundErr) = await _razorpayService.RefundPaymentAsync(order.Payment.RazorpayPaymentId, order.TotalAmount);
                if (refundOk)
                {
                    order.Payment.Status = "Refunded";
                }
            }

            // Restore Stock
            foreach (var item in order.OrderItems)
            {
                if (item.Product != null)
                {
                    item.Product.StockQuantity += item.Quantity;
                }
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"❌ Order #{order.Id:D5} cancelled/declined. Customer notified and ₹{order.TotalAmount:N2} will be refunded.";
            return RedirectToFarmerOrder(orderId, returnUrl);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateOrderStatus(int orderId, string status, string? returnUrl = null)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order != null)
            {
                order.Status = status;
                order.CancellationDeadline = DateTime.UtcNow; // Lock review window immediately on dispatch / ready
                await _context.SaveChangesAsync();

                // Trigger automated WhatsApp notification when produce is ready or dispatched
                if (status.Contains("Ready", StringComparison.OrdinalIgnoreCase))
                {
                    await TriggerProduceReadyWhatsAppAlertAsync(order.Id);
                }

                TempData["SuccessMessage"] = $"✅ Order #{order.Id:D5} status marked as '{status}' successfully! WhatsApp notification sent to customer.";
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

            // Trigger automated WhatsApp dispatch alert to buyer
            await TriggerProduceDispatchedWhatsAppAlertAsync(order.Id);

            TempData["SuccessMessage"] = $"🚚 Bilty #{order.BiltyNumber} uploaded! Order #{order.Id:D5} marked as Dispatched via {order.CargoServiceName}. WhatsApp notification sent to customer.";
            return RedirectToFarmerOrder(orderId, returnUrl);
        }

        private async Task TriggerProduceReadyWhatsAppAlertAsync(int orderId)
        {
            try
            {
                _logger.LogInformation("🚀 [Farmer WhatsApp] Processing Produce Ready alert for Order #{OrderId}", orderId);

                var order = await _context.Orders
                    .Include(o => o.OrderItems)
                        .ThenInclude(oi => oi.Product)
                    .FirstOrDefaultAsync(o => o.Id == orderId);

                if (order == null || string.IsNullOrEmpty(order.CustomerId)) return;

                var buyerUser = await _userManager.FindByIdAsync(order.CustomerId) ?? await _userManager.FindByNameAsync(order.CustomerId);
                var buyerPhone = buyerUser?.PhoneNumber ?? "";
                var buyerName = $"{buyerUser?.FirstName} {buyerUser?.LastName}".Trim();
                if (string.IsNullOrEmpty(buyerName)) buyerName = "Customer";

                // Resolve farmer profile from order crop or logged in farmer
                var firstProd = order.OrderItems.FirstOrDefault()?.Product;
                var farmerId = firstProd?.FarmerId;
                ApplicationUser? farmerUser = null;
                if (!string.IsNullOrEmpty(farmerId))
                {
                    farmerUser = await _userManager.FindByIdAsync(farmerId) ?? await _userManager.FindByNameAsync(farmerId);
                }
                if (farmerUser == null && User != null)
                {
                    farmerUser = await _userManager.GetUserAsync(User);
                }

                var farmerName = $"{farmerUser?.FirstName} {farmerUser?.LastName}".Trim();
                if (string.IsNullOrEmpty(farmerName)) farmerName = "Farmer";
                var farmerPhone = farmerUser?.PhoneNumber ?? "";

                var pickupAddress = !string.IsNullOrWhiteSpace(farmerUser?.Address)
                    ? $"{farmerUser.Address}, {farmerUser.City}"
                    : (farmerUser?.City ?? "Gujarat Farm Location");

                if (!string.IsNullOrEmpty(buyerPhone))
                {
                    var res = await _whatsAppService.SendProduceReadyNotificationAsync(order, buyerPhone, buyerName, farmerName, farmerPhone, pickupAddress, buyerUser?.Email ?? "");
                    _logger.LogInformation("✅ [Farmer WhatsApp] Produce Ready alert sent to Buyer {Phone}. Success={Success}, MsgId={MsgId}, Err={Err}", 
                        buyerPhone, res.Success, res.MessageId, res.ErrorMessage);
                }
                else
                {
                    _logger.LogWarning("⚠️ [Farmer WhatsApp] Buyer {BuyerId} has no phone number. Skipping Produce Ready WhatsApp alert.", order.CustomerId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [Farmer WhatsApp] Failed to send Produce Ready WhatsApp alert for Order #{OrderId}", orderId);
            }
        }

        private async Task TriggerProduceDispatchedWhatsAppAlertAsync(int orderId)
        {
            try
            {
                _logger.LogInformation("🚀 [Farmer WhatsApp] Processing Produce Dispatched alert for Order #{OrderId}", orderId);

                var order = await _context.Orders
                    .Include(o => o.OrderItems)
                        .ThenInclude(oi => oi.Product)
                    .FirstOrDefaultAsync(o => o.Id == orderId);

                if (order == null || string.IsNullOrEmpty(order.CustomerId)) return;

                var buyerUser = await _userManager.FindByIdAsync(order.CustomerId) ?? await _userManager.FindByNameAsync(order.CustomerId);
                var buyerPhone = buyerUser?.PhoneNumber ?? "";
                var buyerName = $"{buyerUser?.FirstName} {buyerUser?.LastName}".Trim();
                if (string.IsNullOrEmpty(buyerName)) buyerName = "Customer";

                // Resolve farmer profile
                var firstProd = order.OrderItems.FirstOrDefault()?.Product;
                var farmerId = firstProd?.FarmerId;
                ApplicationUser? farmerUser = null;
                if (!string.IsNullOrEmpty(farmerId))
                {
                    farmerUser = await _userManager.FindByIdAsync(farmerId) ?? await _userManager.FindByNameAsync(farmerId);
                }
                if (farmerUser == null && User != null)
                {
                    farmerUser = await _userManager.GetUserAsync(User);
                }

                var farmerName = $"{farmerUser?.FirstName} {farmerUser?.LastName}".Trim();
                if (string.IsNullOrEmpty(farmerName)) farmerName = "Farmer";
                var farmerPhone = farmerUser?.PhoneNumber ?? "";

                if (!string.IsNullOrEmpty(buyerPhone))
                {
                    var res = await _whatsAppService.SendProduceDispatchedNotificationAsync(
                        order, 
                        buyerPhone, 
                        buyerName, 
                        farmerName, 
                        farmerPhone, 
                        order.CargoServiceName ?? "GSRTC ST Bus Parcel", 
                        order.BiltyNumber ?? "LR-Direct",
                        buyerUser?.Email ?? "");

                    _logger.LogInformation("✅ [Farmer WhatsApp] Produce Dispatched alert sent to Buyer {Phone}. Success={Success}, MsgId={MsgId}, Err={Err}", 
                        buyerPhone, res.Success, res.MessageId, res.ErrorMessage);
                }
                else
                {
                    _logger.LogWarning("⚠️ [Farmer WhatsApp] Buyer {BuyerId} has no phone number. Skipping Produce Dispatched WhatsApp alert.", order.CustomerId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [Farmer WhatsApp] Failed to send Produce Dispatched WhatsApp alert for Order #{OrderId}", orderId);
            }
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

            var categories = await _context.Categories.OrderBy(c => c.Id).ToListAsync();
            ViewData["CategoryId"] = GetCategorySelectList(categories, product.CategoryId);
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

            var categories = await _context.Categories.OrderBy(c => c.Id).ToListAsync();
            ViewData["CategoryId"] = GetCategorySelectList(categories, product.CategoryId);
            return View(product);
        }
        private Microsoft.AspNetCore.Mvc.Rendering.SelectList GetCategorySelectList(IEnumerable<AgriAI.Domain.Entities.Category> categories, int? selectedId = null)
        {
            var items = categories.Select(c => new
            {
                Id = c.Id,
                DisplayName = $"{GetCategoryEmoji(c.Name)} {c.Name}"
            });
            return new Microsoft.AspNetCore.Mvc.Rendering.SelectList(items, "Id", "DisplayName", selectedId);
        }

        private static string GetCategoryEmoji(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "📦";
            var lower = name.ToLower();
            if (lower.Contains("veg")) return "🥬";
            if (lower.Contains("fruit")) return "🍎";
            if (lower.Contains("grain") || lower.Contains("cereal")) return "🌾";
            if (lower.Contains("pulse")) return "🌱";
            if (lower.Contains("oilseed")) return "🥜";
            if (lower.Contains("spice") || lower.Contains("jeera")) return "🌿";
            if (lower.Contains("dairy")) return "🥛";
            if (lower.Contains("seed") || lower.Contains("fertilizer")) return "🌻";
            return "📦";
        }
    }
}
