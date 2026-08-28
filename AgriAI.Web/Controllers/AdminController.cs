using AgriAI.Application.Interfaces;
using AgriAI.Domain.Entities;
using AgriAI.Infrastructure.Identity;
using AgriAI.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AgriAI.Web.Controllers
{
    // [Authorize(Roles = "Admin")] // Uncomment when Identity Roles are fully enforced
    public class AdminController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserManager<ApplicationUser> _userManager;

        public AdminController(IUnitOfWork unitOfWork, UserManager<ApplicationUser> userManager)
        {
            _unitOfWork = unitOfWork;
            _userManager = userManager;
        }

        public async Task<IActionResult> Dashboard()
        {
            var products = await _unitOfWork.Repository<Product>().GetAllAsync();
            var categories = await _unitOfWork.Repository<Category>().GetAllAsync();
            var orders = await _unitOfWork.Repository<Order>().GetAllAsync();

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
            ViewBag.TotalCategories = categories.Count;
            ViewBag.TotalOrders = orders.Count;
            ViewBag.TotalRevenue = orders.Sum(o => o.TotalAmount);
            ViewBag.TotalUsers = allUsers.Count;
            ViewBag.FarmersCount = farmersCount;
            ViewBag.CustomersCount = customersCount;
            ViewBag.AllProducts = products;
            ViewBag.AllUsers = userVMs;

            // Fetch recent orders
            var recentOrders = orders.OrderByDescending(o => o.OrderDate).Take(10).ToList();
            
            return View(recentOrders);
        }

        // GET: /Admin/Users
        public async Task<IActionResult> Users(string roleFilter = "All")
        {
            var usersList = _userManager.Users.ToList();
            var userVMs = new List<UserManagementViewModel>();

            foreach (var user in usersList)
            {
                var roles = await _userManager.GetRolesAsync(user);
                var roleName = roles.FirstOrDefault() ?? "Customer";

                // Exclude Admin accounts from management list
                if (string.Equals(roleName, "Admin", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (roleFilter != "All" && !string.Equals(roleName, roleFilter, StringComparison.OrdinalIgnoreCase))
                {
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

            ViewBag.CurrentFilter = roleFilter;
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

            user.IsApprovedByAdmin = false;
            user.KycRejectionReason = string.IsNullOrWhiteSpace(reason) 
                ? "Document verification failed. Please re-upload a clear 7/12 (Satbara) or Aadhaar document." 
                : reason.Trim();
            await _userManager.UpdateAsync(user);

            TempData["SuccessMessage"] = $"Farmer '{user.UserName}' verification has been rejected with reason: '{user.KycRejectionReason}'.";
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
    }
}
