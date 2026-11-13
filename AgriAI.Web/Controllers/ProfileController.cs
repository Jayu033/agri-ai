using AgriAI.Infrastructure.Identity;
using AgriAI.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AgriAI.Web.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IWebHostEnvironment _env;

        public ProfileController(
            UserManager<ApplicationUser> userManager, 
            SignInManager<ApplicationUser> signInManager,
            IWebHostEnvironment env)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _env = env;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            var roles = await _userManager.GetRolesAsync(user);

            var model = new ProfileViewModel
            {
                Username = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                FirstName = user.FirstName,
                LastName = user.LastName,
                PhoneNumber = user.PhoneNumber,
                Role = roles.FirstOrDefault() ?? "Customer",
                ProfilePicturePath = user.ProfilePicturePath,
                City = user.City,
                Address = user.Address,
                BankName = user.BankName,
                BankAccountNumber = user.BankAccountNumber,
                BankIfscCode = user.BankIfscCode,
                UpiId = user.UpiId,
                KycDocumentType = user.KycDocumentType,
                KycDocumentPath = user.KycDocumentPath,
                IsApprovedByAdmin = user.IsApprovedByAdmin,
                KycRejectionReason = user.KycRejectionReason,
                KycSubmittedAt = user.KycSubmittedAt
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Index(ProfileViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            var roles = await _userManager.GetRolesAsync(user);
            var isFarmer = roles.Contains("Farmer");

            if (!ModelState.IsValid)
            {
                // Restore non-editable/context fields
                model.Email = user.Email ?? string.Empty;
                model.Role = roles.FirstOrDefault() ?? "Customer";
                model.ProfilePicturePath = user.ProfilePicturePath;
                model.KycDocumentPath = user.KycDocumentPath;
                model.IsApprovedByAdmin = user.IsApprovedByAdmin;
                model.KycRejectionReason = user.KycRejectionReason;
                model.KycSubmittedAt = user.KycSubmittedAt;
                return View(model);
            }

            // Verify current password before allowing any changes
            var passwordCheck = await _userManager.CheckPasswordAsync(user, model.CurrentPassword);
            if (!passwordCheck)
            {
                ModelState.AddModelError("CurrentPassword", "Incorrect current password. Changes not saved.");
                model.Email = user.Email ?? string.Empty;
                model.Role = roles.FirstOrDefault() ?? "Customer";
                model.ProfilePicturePath = user.ProfilePicturePath;
                model.KycDocumentPath = user.KycDocumentPath;
                model.IsApprovedByAdmin = user.IsApprovedByAdmin;
                model.KycRejectionReason = user.KycRejectionReason;
                model.KycSubmittedAt = user.KycSubmittedAt;
                return View(model);
            }

            // 1. Handle Password Change (if requested)
            if (!string.IsNullOrWhiteSpace(model.NewPassword))
            {
                var changePasswordResult = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
                if (!changePasswordResult.Succeeded)
                {
                    foreach (var error in changePasswordResult.Errors)
                    {
                        ModelState.AddModelError("NewPassword", error.Description);
                    }
                    model.Email = user.Email ?? string.Empty;
                    model.Role = roles.FirstOrDefault() ?? "Customer";
                    model.ProfilePicturePath = user.ProfilePicturePath;
                    model.KycDocumentPath = user.KycDocumentPath;
                    model.IsApprovedByAdmin = user.IsApprovedByAdmin;
                    model.KycRejectionReason = user.KycRejectionReason;
                    model.KycSubmittedAt = user.KycSubmittedAt;
                    return View(model);
                }
            }

            // 2. Handle New Profile Picture upload
            var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
            if (model.NewProfilePictureFile != null && model.NewProfilePictureFile.Length > 0)
            {
                var profilesDir = Path.Combine(webRoot, "uploads", "profiles");
                if (!Directory.Exists(profilesDir))
                {
                    Directory.CreateDirectory(profilesDir);
                }

                var profileFileName = $"{Guid.NewGuid()}_{Path.GetFileName(model.NewProfilePictureFile.FileName)}";
                var profileFilePath = Path.Combine(profilesDir, profileFileName);

                using (var stream = new FileStream(profileFilePath, FileMode.Create))
                {
                    await model.NewProfilePictureFile.CopyToAsync(stream);
                }

                user.ProfilePicturePath = $"/uploads/profiles/{profileFileName}";
            }

            // 3. Handle New KYC Document upload (for Farmers)
            if (isFarmer && model.NewKycDocumentFile != null && model.NewKycDocumentFile.Length > 0)
            {
                var uploadsDir = Path.Combine(webRoot, "uploads", "kyc");
                if (!Directory.Exists(uploadsDir))
                {
                    Directory.CreateDirectory(uploadsDir);
                }

                var kycFileName = $"{Guid.NewGuid()}_{Path.GetFileName(model.NewKycDocumentFile.FileName)}";
                var kycFilePath = Path.Combine(uploadsDir, kycFileName);

                using (var stream = new FileStream(kycFilePath, FileMode.Create))
                {
                    await model.NewKycDocumentFile.CopyToAsync(stream);
                }

                user.KycDocumentPath = $"/uploads/kyc/{kycFileName}";
                user.KycSubmittedAt = DateTime.UtcNow;
            }

            // 4. Update Profile Details
            user.UserName = model.Username;
            user.FirstName = model.FirstName;
            user.LastName = model.LastName;
            user.PhoneNumber = model.PhoneNumber;

            // Update Farmer Specific Information
            if (isFarmer)
            {
                user.City = model.City ?? string.Empty;
                user.Address = model.Address ?? string.Empty;
                user.BankName = model.BankName;
                user.BankAccountNumber = model.BankAccountNumber;
                user.BankIfscCode = model.BankIfscCode?.ToUpperInvariant();
                user.UpiId = model.UpiId;
                if (!string.IsNullOrEmpty(model.KycDocumentType))
                {
                    user.KycDocumentType = model.KycDocumentType;
                }
            }

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                await _signInManager.RefreshSignInAsync(user);
                TempData["SuccessMessage"] = "Profile & Account Settings updated successfully!";
                return RedirectToAction("Index");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            model.Email = user.Email ?? string.Empty;
            model.Role = roles.FirstOrDefault() ?? "Customer";
            model.ProfilePicturePath = user.ProfilePicturePath;
            model.KycDocumentPath = user.KycDocumentPath;
            model.IsApprovedByAdmin = user.IsApprovedByAdmin;
            model.KycRejectionReason = user.KycRejectionReason;
            model.KycSubmittedAt = user.KycSubmittedAt;
            return View(model);
        }
    }
}
