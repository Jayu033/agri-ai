using AgriAI.Application.Interfaces;
using AgriAI.Domain.Entities;
using AgriAI.Infrastructure.Data;
using AgriAI.Infrastructure.Identity;
using AgriAI.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgriAI.Web.Controllers
{
    // [Authorize(Roles = "Admin")] // Uncomment when Identity Roles are fully enforced
    public class AdminController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public AdminController(
            IUnitOfWork unitOfWork, 
            UserManager<ApplicationUser> userManager, 
            ApplicationDbContext context,
            IWebHostEnvironment webHostEnvironment)
        {
            _unitOfWork = unitOfWork;
            _userManager = userManager;
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        public async Task<IActionResult> Dashboard()
        {
            var products = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Images)
                .ToListAsync();

            var categories = await _context.Categories.ToListAsync();

            var orders = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(i => i.Product)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            var allUsers = _userManager.Users.ToList();
            var userVMs = new List<UserManagementViewModel>();
            int farmersCount = 0;
            int customersCount = 0;

            foreach (var user in allUsers)
            {
                var roles = await _userManager.GetRolesAsync(user);
                var roleName = roles.FirstOrDefault() ?? "Customer";

                if (string.Equals(roleName, "Farmer", StringComparison.OrdinalIgnoreCase))
                    farmersCount++;
                else if (string.Equals(roleName, "Customer", StringComparison.OrdinalIgnoreCase))
                    customersCount++;

                // Exclude Admin from user list
                if (!string.Equals(roleName, "Admin", StringComparison.OrdinalIgnoreCase))
                {
                    bool isBlocked = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow;
                    userVMs.Add(new UserManagementViewModel
                    {
                        Id = user.Id,
                        Username = user.UserName ?? string.Empty,
                        Email = user.Email ?? string.Empty,
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        ProfilePicturePath = user.ProfilePicturePath,
                        Role = roleName,
                        IsBlocked = isBlocked,
                        IsApprovedByAdmin = user.IsApprovedByAdmin,
                        City = user.City,
                        Address = user.Address,
                        BankName = user.BankName,
                        BankAccountNumber = user.BankAccountNumber,
                        BankIfscCode = user.BankIfscCode,
                        UpiId = user.UpiId,
                        KycDocumentPath = user.KycDocumentPath,
                        KycDocumentType = user.KycDocumentType,
                        KycSubmittedAt = user.KycSubmittedAt,
                        KycRejectionReason = user.KycRejectionReason,
                        CreatedAt = user.CreatedAt
                    });
                }
            }

            ViewBag.TotalProducts = products.Count;
            ViewBag.TotalFarmers = farmersCount;
            ViewBag.TotalCustomers = customersCount;
            ViewBag.TotalOrders = orders.Count;
            ViewBag.TotalRevenue = orders.Sum(o => o.TotalAmount);
            ViewBag.AllUsers = userVMs;
            ViewBag.AllProducts = products;
            ViewBag.AllCategories = categories;

            return View(orders);
        }

        // GET: /Admin/OrderDetails/5
        public async Task<IActionResult> OrderDetails(int id)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(i => i.Product)
                        .ThenInclude(p => p.Images)
                .Include(o => o.OrderItems)
                    .ThenInclude(i => i.Product)
                        .ThenInclude(p => p.Category)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
            {
                TempData["ErrorMessage"] = "Order not found.";
                return RedirectToAction(nameof(Dashboard));
            }

            // 1. Get Customer Details (Buyer)
            ApplicationUser? customerUser = null;
            if (!string.IsNullOrEmpty(order.CustomerId))
            {
                customerUser = await _userManager.FindByIdAsync(order.CustomerId) 
                            ?? await _userManager.FindByEmailAsync(order.CustomerId)
                            ?? await _userManager.FindByNameAsync(order.CustomerId);
            }

            // 2. Get Farmer / Producer Details (The verified farmer who listed / fulfilled the produce)
            ApplicationUser? farmerUser = null;
            var firstProduct = order.OrderItems.FirstOrDefault()?.Product;
            if (firstProduct != null && !string.IsNullOrEmpty(firstProduct.FarmerId))
            {
                farmerUser = await _userManager.FindByIdAsync(firstProduct.FarmerId) 
                          ?? await _userManager.FindByEmailAsync(firstProduct.FarmerId)
                          ?? await _userManager.FindByNameAsync(firstProduct.FarmerId);
            }

            // Fallback to registered farmer if direct ID wasn't set
            if (farmerUser == null)
            {
                var farmersInRole = await _userManager.GetUsersInRoleAsync("Farmer");
                farmerUser = farmersInRole.FirstOrDefault();
            }

            ViewBag.CustomerUser = customerUser;
            ViewBag.FarmerUser = farmerUser;

            return View(order);
        }

        // GET: /Admin/Users
        public async Task<IActionResult> Users(string? roleFilter)
        {
            var allUsers = _userManager.Users.ToList();
            var userVMs = new List<UserManagementViewModel>();

            foreach (var user in allUsers)
            {
                var roles = await _userManager.GetRolesAsync(user);
                var roleName = roles.FirstOrDefault() ?? "Customer";

                // Strictly exclude Admin from the User Management list per business rules
                if (string.Equals(roleName, "Admin", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Apply role filter if specified (e.g. Farmer, Customer)
                if (!string.IsNullOrEmpty(roleFilter) && !string.Equals(roleFilter, "All", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.Equals(roleName, roleFilter, StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                bool isBlocked = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow;

                userVMs.Add(new UserManagementViewModel
                {
                    Id = user.Id,
                    Username = user.UserName ?? string.Empty,
                    Email = user.Email ?? string.Empty,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    ProfilePicturePath = user.ProfilePicturePath,
                    Role = roleName,
                    IsBlocked = isBlocked,
                    IsApprovedByAdmin = user.IsApprovedByAdmin,
                    City = user.City,
                    Address = user.Address,
                    BankName = user.BankName,
                    BankAccountNumber = user.BankAccountNumber,
                    BankIfscCode = user.BankIfscCode,
                    UpiId = user.UpiId,
                    KycDocumentPath = user.KycDocumentPath,
                    KycDocumentType = user.KycDocumentType,
                    KycSubmittedAt = user.KycSubmittedAt,
                    KycRejectionReason = user.KycRejectionReason,
                    CreatedAt = user.CreatedAt
                });
            }

            ViewBag.CurrentFilter = string.IsNullOrEmpty(roleFilter) ? "All" : roleFilter;
            return View(userVMs);
        }

        // POST: /Admin/ApproveFarmer
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveFarmer(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                TempData["ErrorMessage"] = "Invalid user ID.";
                return RedirectToAction(nameof(Users), new { roleFilter = "Farmer" });
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData["ErrorMessage"] = "Farmer not found.";
                return RedirectToAction(nameof(Users), new { roleFilter = "Farmer" });
            }

            user.IsApprovedByAdmin = true;
            user.KycRejectionReason = null;
            user.KycApprovedAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);

            TempData["SuccessMessage"] = $"Farmer '{user.UserName}' has been approved and activated successfully.";
            return RedirectToAction(nameof(Users), new { roleFilter = "Farmer" });
        }

        // POST: /Admin/RejectFarmer
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectFarmer(string id, string? reason)
        {
            if (string.IsNullOrEmpty(id))
            {
                TempData["ErrorMessage"] = "Invalid user ID.";
                return RedirectToAction(nameof(Users), new { roleFilter = "Farmer" });
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData["ErrorMessage"] = "Farmer not found.";
                return RedirectToAction(nameof(Users), new { roleFilter = "Farmer" });
            }

            var userName = user.UserName ?? user.Email;

            // 1. Delete physical KYC Document file from disk
            if (!string.IsNullOrEmpty(user.KycDocumentPath))
            {
                try
                {
                    var kycFilePath = Path.Combine(_webHostEnvironment.WebRootPath, user.KycDocumentPath.TrimStart('/', '\\'));
                    if (System.IO.File.Exists(kycFilePath))
                    {
                        System.IO.File.Delete(kycFilePath);
                    }
                }
                catch { }
            }

            // 2. Delete physical Profile Picture file from disk
            if (!string.IsNullOrEmpty(user.ProfilePicturePath) && !user.ProfilePicturePath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var profileFilePath = Path.Combine(_webHostEnvironment.WebRootPath, user.ProfilePicturePath.TrimStart('/', '\\'));
                    if (System.IO.File.Exists(profileFilePath))
                    {
                        System.IO.File.Delete(profileFilePath);
                    }
                }
                catch { }
            }

            // 3. Remove any products created by this farmer if any
            var farmerProducts = await _context.Products.Where(p => p.FarmerId == user.Id).ToListAsync();
            if (farmerProducts.Any())
            {
                _context.Products.RemoveRange(farmerProducts);
                await _context.SaveChangesAsync();
            }

            // 4. Completely delete user account record from database
            var result = await _userManager.DeleteAsync(user);
            if (result.Succeeded)
            {
                var rejectionText = string.IsNullOrWhiteSpace(reason) 
                    ? "Incomplete / invalid 7/12 document" 
                    : reason.Trim();

                TempData["SuccessMessage"] = $"Farmer '{userName}' application rejected ({rejectionText}). All account data and uploaded documents have been completely cleared so the farmer can submit a fresh registration.";
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to completely delete rejected farmer: " + string.Join(", ", result.Errors.Select(e => e.Description));
            }

            return RedirectToAction(nameof(Users), new { roleFilter = "Farmer" });
        }

        // POST: /Admin/ToggleBlockUser
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleBlockUser(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                TempData["ErrorMessage"] = "Invalid user ID.";
                return RedirectToAction(nameof(Users));
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction(nameof(Users));
            }

            // Cannot block Admin
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser != null && currentUser.Id == user.Id)
            {
                TempData["ErrorMessage"] = "You cannot block your own admin account!";
                return RedirectToAction(nameof(Users));
            }

            bool currentlyBlocked = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow;

            if (currentlyBlocked)
            {
                await _userManager.SetLockoutEndDateAsync(user, null);
                TempData["SuccessMessage"] = $"User '{user.Email}' has been unblocked successfully.";
            }
            else
            {
                await _userManager.SetLockoutEnabledAsync(user, true);
                await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));
                TempData["SuccessMessage"] = $"User '{user.Email}' has been blocked successfully.";
            }

            return RedirectToAction(nameof(Users));
        }

        // POST: /Admin/DeleteUser
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                TempData["ErrorMessage"] = "Invalid user ID.";
                return RedirectToAction(nameof(Users));
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction(nameof(Users));
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser != null && currentUser.Id == user.Id)
            {
                TempData["ErrorMessage"] = "You cannot delete your own admin account!";
                return RedirectToAction(nameof(Users));
            }

            var result = await _userManager.DeleteAsync(user);
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = $"User '{user.Email}' has been deleted successfully.";
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to delete user: " + string.Join(", ", result.Errors.Select(e => e.Description));
            }

            return RedirectToAction(nameof(Users));
        }

        // POST: /Admin/ReleaseEscrowPayoutViaBilty (Step 6 Blueprint)
        [HttpPost]
        public async Task<IActionResult> ReleaseEscrowPayoutViaBilty(int orderId, string? reason)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
            {
                TempData["ErrorMessage"] = "Order not found.";
                return RedirectToAction(nameof(Dashboard));
            }

            order.Status = "Delivered & Settled (Admin Approved)";
            order.AdminApprovedPayout = true;
            order.AdminPayoutReason = string.IsNullOrWhiteSpace(reason) 
                ? "Escrow payout released by Admin upon verifying valid GSRTC / Cargo Bilty proof of dispatch." 
                : reason.Trim();
            order.DeliveredAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"🛡️ Escrow Payout of ₹{order.TotalAmount:N2} for Order #{order.Id:D5} has been released to Farmer Bank based on verified Bilty Proof!";
            return RedirectToAction(nameof(Dashboard));
        }
    }
}
