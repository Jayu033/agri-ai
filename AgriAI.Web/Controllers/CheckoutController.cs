using AgriAI.Domain.Entities;
using AgriAI.Infrastructure.Data;
using AgriAI.Infrastructure.Identity;
using AgriAI.Web.Services;
using AgriAI.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace AgriAI.Web.Controllers
{
    [Authorize]
    public class CheckoutController : Controller
    {
        private const string CartSessionKey = "AgriAI_ShoppingCart_Session";
        private const string CartCookieKey = "AgriAI_Cart_Cookie";

        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IRazorpayPaymentService _razorpayService;
        private readonly IWhatsAppNotificationService _whatsAppService;
        private readonly ILogger<CheckoutController> _logger;

        public CheckoutController(
            ApplicationDbContext context, 
            UserManager<ApplicationUser> userManager,
            IRazorpayPaymentService razorpayService,
            IWhatsAppNotificationService whatsAppService,
            ILogger<CheckoutController> logger)
        {
            _context = context;
            _userManager = userManager;
            _razorpayService = razorpayService;
            _whatsAppService = whatsAppService;
            _logger = logger;
        }

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
            ViewBag.RazorpayKeyId = _razorpayService.GetKeyId();

            if (User.IsInRole("Admin"))
            {
                TempData["ErrorMessage"] = "🛡️ Administrators are platform regulators and cannot purchase crops or place orders.";
                return Redirect("/Admin/Dashboard?tab=products");
            }

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
                        TempData["ErrorMessage"] = "⚠️ You cannot purchase your own listed produce. You can manage it from your Farmer Dashboard.";
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
                    return User.IsInRole("Farmer") ? Redirect("/Farmer/Dashboard?tab=farmerPurchasesTab") : Redirect("/Customer/Dashboard?tab=shopTab");
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

        /// <summary>
        /// AJAX: Initializes a Razorpay order with total escrow amount in paise.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateRazorpayOrder([FromBody] CreateRazorpayOrderRequest request)
        {
            if (User.IsInRole("Admin"))
            {
                return Json(new { success = false, message = "Administrators cannot place crop orders." });
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Json(new { success = false, message = "Please sign in to complete your checkout." });
            }

            decimal baseTotal = 0m;
            string description = "AgriAI Escrow Purchase";

            if (request.IsCart)
            {
                var cart = GetSessionCart();
                if (!cart.Any())
                {
                    return Json(new { success = false, message = "Your cart is empty." });
                }
                baseTotal = cart.Sum(i => i.TotalPrice);
                description = $"AgriAI Harvest ({cart.Count} Crop Varieties)";
            }
            else if (request.ProductId.HasValue)
            {
                var product = await _context.Products.FindAsync(request.ProductId.Value);
                if (product == null)
                {
                    return Json(new { success = false, message = "Produce not found." });
                }
                int qty = request.Quantity < 1 ? 1 : request.Quantity;
                baseTotal = product.Price * qty;
                description = $"{product.Name} ({qty} Kg) Direct Farm Purchase";
            }
            else
            {
                return Json(new { success = false, message = "Invalid crop purchase parameters." });
            }

            decimal platformFee = Math.Round(baseTotal * 0.02m, 2);
            decimal grandTotal = baseTotal + platformFee;
            string receipt = $"rcpt_{DateTime.UtcNow.Ticks % 10000000}";

            var (success, razorpayOrderId, error) = await _razorpayService.CreateOrderAsync(grandTotal, receipt, description);
            if (!success)
            {
                _logger.LogError("Failed to create Razorpay order: {Error}", error);
                return Json(new { success = false, message = $"Razorpay Error: {error}" });
            }

            long amountInPaise = (long)Math.Round(grandTotal * 100m, MidpointRounding.AwayFromZero);

            return Json(new
            {
                success = true,
                razorpayOrderId = razorpayOrderId,
                amountInPaise = amountInPaise,
                amountFormatted = grandTotal.ToString("N2"),
                currency = "INR",
                keyId = _razorpayService.GetKeyId(),
                description = description,
                prefill = new
                {
                    name = $"{currentUser.FirstName} {currentUser.LastName}".Trim(),
                    email = currentUser.Email ?? "",
                    contact = currentUser.PhoneNumber ?? ""
                }
            });
        }

        /// <summary>
        /// AJAX: Verifies Razorpay payment signature, creates order & payment records, deducts stock.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyAndCompleteOrder([FromBody] VerifyRazorpayPaymentRequest request)
        {
            if (User.IsInRole("Admin"))
            {
                return Json(new { success = false, message = "Administrators cannot place crop orders." });
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                var currentUser = await _userManager.GetUserAsync(User);
                userId = currentUser?.Id ?? User.Identity?.Name;
            }
            if (string.IsNullOrEmpty(userId))
            {
                return Json(new { success = false, message = "Session expired. Please log in again." });
            }

            // 1. Cryptographically verify signature
            bool isSignatureValid = _razorpayService.VerifyPaymentSignature(
                request.RazorpayOrderId,
                request.RazorpayPaymentId,
                request.RazorpaySignature);

            if (!isSignatureValid)
            {
                _logger.LogWarning("Untrusted Razorpay signature: Order {OrderId}, Payment {PaymentId}", request.RazorpayOrderId, request.RazorpayPaymentId);
                return Json(new { success = false, message = "Security Warning: Payment signature verification failed. Untrusted transaction." });
            }

            // 2. Setup transport & delivery address
            string transportType = request.TransportType ?? "Direct Farm Pickup";
            string deliveryAddress = request.DeliveryAddress ?? "";
            if (transportType == "Direct Farm Pickup" || string.IsNullOrWhiteSpace(deliveryAddress))
            {
                deliveryAddress = "Direct Farm Pickup (Gujarat Field/Godown)";
            }

            string generatedOtp = new Random().Next(1000, 9999).ToString();
            decimal baseTotal = 0m;
            Order order;

            // 3. Process items & calculate totals
            if (request.IsCart)
            {
                var cartItems = GetSessionCart();
                if (!cartItems.Any())
                {
                    return Json(new { success = false, message = "Your cart is empty." });
                }

                baseTotal = cartItems.Sum(i => i.TotalPrice);
                decimal platformFee = Math.Round(baseTotal * 0.02m, 2);
                decimal grandTotal = baseTotal + platformFee;

                order = new Order
                {
                    CustomerId = userId,
                    OrderDate = DateTime.UtcNow,
                    TotalAmount = grandTotal,
                    Status = "Accepted",
                    PaymentMethod = "Online (Escrow Protected)",
                    DeliveryOtp = generatedOtp,
                    TransportType = transportType,
                    LogisticsFee = 0m,
                    PlatformFee = platformFee,
                    ShippingAddress = deliveryAddress,
                    FarmerAcceptanceAt = DateTime.UtcNow,
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

                    var dbProd = await _context.Products.FindAsync(item.ProductId);
                    if (dbProd != null && dbProd.StockQuantity >= item.Quantity)
                    {
                        dbProd.StockQuantity -= item.Quantity;
                    }
                }

                // Clear session & persistent cookie cart
                HttpContext.Session.Remove(CartSessionKey);
                Response.Cookies.Delete(CartCookieKey);
            }
            else
            {
                Product? product = null;
                if (request.ProductId.HasValue)
                {
                    product = await _context.Products.FindAsync(request.ProductId.Value);
                }

                int qty = request.Quantity < 1 ? 1 : request.Quantity;
                baseTotal = product != null ? product.Price * qty : 0m;
                decimal platformFee = Math.Round(baseTotal * 0.02m, 2);
                decimal grandTotal = baseTotal + platformFee;

                order = new Order
                {
                    CustomerId = userId,
                    OrderDate = DateTime.UtcNow,
                    TotalAmount = grandTotal,
                    Status = "Accepted",
                    PaymentMethod = "Online (Escrow Protected)",
                    DeliveryOtp = generatedOtp,
                    TransportType = transportType,
                    LogisticsFee = 0m,
                    PlatformFee = platformFee,
                    ShippingAddress = deliveryAddress,
                    FarmerAcceptanceAt = DateTime.UtcNow,
                    CancellationDeadline = DateTime.UtcNow.AddHours(3)
                };

                if (product != null)
                {
                    order.OrderItems.Add(new OrderItem
                    {
                        ProductId = product.Id,
                        Quantity = qty,
                        UnitPrice = product.Price
                    });

                    if (product.StockQuantity >= qty)
                    {
                        product.StockQuantity -= qty;
                    }
                }
            }

            await _context.Orders.AddAsync(order);
            await _context.SaveChangesAsync();

            // 4. Save Payment record in database linked to Order
            var payment = new Payment
            {
                OrderId = order.Id,
                RazorpayOrderId = request.RazorpayOrderId,
                RazorpayPaymentId = request.RazorpayPaymentId,
                RazorpaySignature = request.RazorpaySignature,
                Amount = order.TotalAmount,
                Currency = "INR",
                Status = "Captured"
            };

            await _context.Payments.AddAsync(payment);
            await _context.SaveChangesAsync();

            // Trigger 100% Automated WhatsApp Notification safely before returning
            await TriggerAutomatedWhatsAppAlertsAsync(order, userId);

            TempData["SuccessMessage"] = $"🌾 Escrow Payment Confirmed! Order #{order.Id:D5} placed successfully. (Razorpay Ref: {request.RazorpayPaymentId})";

            return Json(new
            {
                success = true,
                orderId = order.Id,
                redirectUrl = Url.Action("OrderDetails", "Customer", new { id = order.Id, from = User.IsInRole("Farmer") ? "farmer" : "active" })
            });
        }

        [HttpPost]
        public async Task<IActionResult> ProcessPayment(int? productId, int quantity = 1, bool isCart = false, string paymentMethod = "Online", string transportType = "Direct Farm Pickup", string deliveryAddress = "")
        {
            if (User.IsInRole("Admin"))
            {
                TempData["ErrorMessage"] = "🛡️ Administrators cannot place orders.";
                return Redirect("/Admin/Dashboard?tab=products");
            }

            if (quantity < 1) quantity = 1;

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                var currentUser = await _userManager.GetUserAsync(User);
                userId = currentUser?.Id ?? User.Identity?.Name;
            }
            if (string.IsNullOrEmpty(userId))
            {
                return Challenge();
            }

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
                    return User.IsInRole("Farmer") ? Redirect("/Farmer/Dashboard?tab=farmerPurchasesTab") : Redirect("/Customer/Dashboard?tab=cartTab");
                }

                baseTotal = cartItems.Sum(i => i.TotalPrice);
                decimal platformFee = Math.Round(baseTotal * 0.02m, 2);
                decimal grandTotal = baseTotal + platformFee;

                var order = new Order
                {
                    CustomerId = userId,
                    OrderDate = DateTime.UtcNow,
                    TotalAmount = grandTotal,
                    Status = "Accepted",
                    PaymentMethod = paymentMethod == "COD" ? "Cash on Delivery" : "Online (Escrow Protected)",
                    DeliveryOtp = generatedOtp,
                    TransportType = transportType,
                    LogisticsFee = 0m,
                    PlatformFee = platformFee,
                    ShippingAddress = deliveryAddress,
                    FarmerAcceptanceAt = DateTime.UtcNow,
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

                // Trigger 100% Automated WhatsApp Notification safely before redirecting
                await TriggerAutomatedWhatsAppAlertsAsync(order, userId);

                TempData["SuccessMessage"] = $"🌾 Multi-Product Order #{order.Id:D5} placed & confirmed ({cartItems.Count} crop varieties)! 3-hour harvest review period active.";
                return RedirectToAction("OrderDetails", "Customer", new { id = order.Id, from = User.IsInRole("Farmer") ? "farmer" : "active" });
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
                Status = "Accepted",
                PaymentMethod = paymentMethod == "COD" ? "Cash on Delivery" : "Online (Escrow Protected)",
                DeliveryOtp = generatedOtp,
                TransportType = transportType,
                LogisticsFee = 0m,
                PlatformFee = singlePlatformFee,
                ShippingAddress = deliveryAddress,
                FarmerAcceptanceAt = DateTime.UtcNow,
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

            // Trigger 100% Automated WhatsApp Notification safely before redirecting
            await TriggerAutomatedWhatsAppAlertsAsync(singleOrder, userId);

            TempData["SuccessMessage"] = $"🌾 Order #{singleOrder.Id:D5} placed & confirmed! 3-hour harvest review period active.";
            return RedirectToAction("OrderDetails", "Customer", new { id = singleOrder.Id, from = User.IsInRole("Farmer") ? "farmer" : "active" });
        }
        private async Task TriggerAutomatedWhatsAppAlertsAsync(Order order, string buyerUserId)
        {
            try
            {
                _logger.LogInformation("🚀 [WhatsApp Automation] Starting automated order alerts for Order #{OrderId} (Buyer: {BuyerId})", order.Id, buyerUserId);

                // Reload full order with products and payment details
                var fullOrder = await _context.Orders
                    .Include(o => o.OrderItems)
                        .ThenInclude(oi => oi.Product)
                    .Include(o => o.Payment)
                    .FirstOrDefaultAsync(o => o.Id == order.Id) ?? order;

                var buyerUser = await _userManager.FindByIdAsync(buyerUserId) ?? await _userManager.FindByNameAsync(buyerUserId);
                var buyerName = $"{buyerUser?.FirstName} {buyerUser?.LastName}".Trim();
                if (string.IsNullOrEmpty(buyerName)) buyerName = "Customer";
                var buyerPhone = buyerUser?.PhoneNumber ?? "";

                // Find primary farmer for the crop
                var firstProd = fullOrder.OrderItems.FirstOrDefault()?.Product;
                var farmerId = firstProd?.FarmerId;
                if (string.IsNullOrEmpty(farmerId) && fullOrder.OrderItems.Any())
                {
                    var pId = fullOrder.OrderItems.First().ProductId;
                    var p = await _context.Products.FindAsync(pId);
                    farmerId = p?.FarmerId;
                }

                ApplicationUser? farmerUser = null;
                if (!string.IsNullOrEmpty(farmerId))
                {
                    farmerUser = await _userManager.FindByIdAsync(farmerId) ?? await _userManager.FindByNameAsync(farmerId);
                }

                var farmerPhone = farmerUser?.PhoneNumber ?? "";
                var farmerName = $"{farmerUser?.FirstName} {farmerUser?.LastName}".Trim();
                if (string.IsNullOrEmpty(farmerName)) farmerName = "Farmer";

                var farmerAddress = !string.IsNullOrWhiteSpace(farmerUser?.Address)
                    ? $"{farmerUser.Address}, {farmerUser.City}"
                    : (farmerUser?.City ?? "Gujarat Farm Location");

                decimal farmerEarnings = fullOrder.OrderItems.Sum(oi => oi.UnitPrice * oi.Quantity);

                // 1. Send automated WhatsApp confirmation to Buyer (Payment Confirmed - NO OTP)
                if (!string.IsNullOrEmpty(buyerPhone))
                {
                    var buyerRes = await _whatsAppService.SendOrderConfirmationToBuyerAsync(fullOrder, buyerPhone, buyerName, farmerName, farmerPhone, farmerAddress);
                    _logger.LogInformation("✅ [WhatsApp Automation] Buyer alert sent to {Phone}. Success={Success}, MsgId={MsgId}, Err={Err}", 
                        buyerPhone, buyerRes.Success, buyerRes.MessageId, buyerRes.ErrorMessage);
                }
                else
                {
                    _logger.LogWarning("⚠️ [WhatsApp Automation] Buyer {BuyerId} has no phone number in profile. Skipping buyer WhatsApp alert.", buyerUserId);
                }

                // 2. Send automated WhatsApp message to Farmer (New Order Notification)
                if (!string.IsNullOrEmpty(farmerPhone))
                {
                    var farmerRes = await _whatsAppService.SendOrderNotificationToFarmerAsync(fullOrder, farmerPhone, farmerName, buyerName, farmerEarnings, buyerPhone);
                    _logger.LogInformation("✅ [WhatsApp Automation] Farmer alert sent to {Phone}. Success={Success}, MsgId={MsgId}, Err={Err}", 
                        farmerPhone, farmerRes.Success, farmerRes.MessageId, farmerRes.ErrorMessage);
                }
                else
                {
                    _logger.LogWarning("⚠️ [WhatsApp Automation] Farmer {FarmerId} has no phone number in profile. Skipping farmer WhatsApp alert.", farmerId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [WhatsApp Automation] Error sending automated WhatsApp notification for Order #{OrderId}", order.Id);
            }
        }
    }
}