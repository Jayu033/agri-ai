using AgriAI.Infrastructure.Identity;
using AgriAI.Web.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AgriAI.Web.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IWebHostEnvironment _env;

        public AccountController(
            UserManager<ApplicationUser> userManager, 
            SignInManager<ApplicationUser> signInManager, 
            RoleManager<IdentityRole> roleManager,
            IWebHostEnvironment env)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _env = env;
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            var isFarmer = model.Role == "Farmer";

            // Strict Anti-Fraud Validation for Farmers
            if (isFarmer)
            {
                if (string.IsNullOrWhiteSpace(model.BankAccountNumber) || string.IsNullOrWhiteSpace(model.BankIfscCode))
                {
                    ModelState.AddModelError(string.Empty, "Bank Account Number and IFSC Code are mandatory for Farmer payout verification.");
                }

                if (model.KycDocumentFile == null || model.KycDocumentFile.Length == 0)
                {
                    ModelState.AddModelError("KycDocumentFile", "Please upload a clear 7/12 (Satbara) or Aadhaar Card document photo or PDF.");
                }
                else
                {
                    var allowedExts = new[] { ".jpg", ".jpeg", ".png", ".pdf", ".webp" };
                    var ext = Path.GetExtension(model.KycDocumentFile.FileName).ToLowerInvariant();
                    if (!allowedExts.Contains(ext))
                    {
                        ModelState.AddModelError("KycDocumentFile", "Invalid document file type. Only JPG, PNG, WEBP, and PDF files are accepted.");
                    }
                    if (model.KycDocumentFile.Length < 5120) // Less than 5KB (empty/dummy)
                    {
                        ModelState.AddModelError("KycDocumentFile", "The uploaded document is too small or blank. Please upload a clear document image.");
                    }
                    if (model.KycDocumentFile.Length > 10 * 1024 * 1024) // Greater than 10MB
                    {
                        ModelState.AddModelError("KycDocumentFile", "Document size exceeds 10MB limit.");
                    }
                }
            }

            if (ModelState.IsValid)
            {
                string? profilePicPath = null;
                string? kycDocPath = null;

                // Handle Profile Photo Upload or Camera Capture (Farmer or Customer)
                if (model.ProfilePictureFile != null && model.ProfilePictureFile.Length > 0)
                {
                    var profilesDir = Path.Combine(_env.WebRootPath, "uploads", "profiles");
                    if (!Directory.Exists(profilesDir))
                    {
                        Directory.CreateDirectory(profilesDir);
                    }

                    var profileFileName = $"{Guid.NewGuid()}_{Path.GetFileName(model.ProfilePictureFile.FileName)}";
                    var profileFilePath = Path.Combine(profilesDir, profileFileName);

                    using (var stream = new FileStream(profileFilePath, FileMode.Create))
                    {
                        await model.ProfilePictureFile.CopyToAsync(stream);
                    }

                    profilePicPath = $"/uploads/profiles/{profileFileName}";
                }
                else if (!string.IsNullOrWhiteSpace(model.CapturedProfilePictureBase64))
                {
                    var profilesDir = Path.Combine(_env.WebRootPath, "uploads", "profiles");
                    if (!Directory.Exists(profilesDir))
                    {
                        Directory.CreateDirectory(profilesDir);
                    }

                    var base64Data = model.CapturedProfilePictureBase64;
                    if (base64Data.Contains(","))
                    {
                        base64Data = base64Data.Substring(base64Data.IndexOf(",") + 1);
                    }

                    var bytes = Convert.FromBase64String(base64Data);
                    var profileFileName = $"{Guid.NewGuid()}_camera_capture.jpg";
                    var profileFilePath = Path.Combine(profilesDir, profileFileName);

                    await System.IO.File.WriteAllBytesAsync(profileFilePath, bytes);
                    profilePicPath = $"/uploads/profiles/{profileFileName}";
                }

                // Handle KYC document upload for Farmers
                if (isFarmer && model.KycDocumentFile != null && model.KycDocumentFile.Length > 0)
                {
                    var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "kyc");
                    if (!Directory.Exists(uploadsDir))
                    {
                        Directory.CreateDirectory(uploadsDir);
                    }

                    var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(model.KycDocumentFile.FileName)}";
                    var filePath = Path.Combine(uploadsDir, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await model.KycDocumentFile.CopyToAsync(stream);
                    }

                    kycDocPath = $"/uploads/kyc/{fileName}";
                }

                var user = new ApplicationUser 
                { 
                    UserName = model.Username, 
                    Email = model.Email,
                    PhoneNumber = model.PhoneNumber,
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    ProfilePicturePath = profilePicPath,
                    City = model.City ?? string.Empty,
                    Address = model.Address ?? string.Empty,
                    BankName = model.BankName,
                    BankAccountNumber = model.BankAccountNumber,
                    BankIfscCode = model.BankIfscCode,
                    UpiId = model.UpiId,
                    KycDocumentType = model.KycDocumentType,
                    KycDocumentPath = kycDocPath,
                    KycSubmittedAt = isFarmer ? DateTime.UtcNow : null,
                    IsApprovedByAdmin = !isFarmer // Customers are auto-approved, Farmers require Admin Verification
                };

                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    if (!await _roleManager.RoleExistsAsync(model.Role))
                    {
                        await _roleManager.CreateAsync(new IdentityRole(model.Role));
                    }
                    
                    await _userManager.AddToRoleAsync(user, model.Role);

                    if (isFarmer)
                    {
                        TempData["SuccessMessage"] = "Farmer registration submitted successfully! Your account and 7/12 documents are under Admin verification. You will be able to log in once approved.";
                        return RedirectToAction("Login");
                    }
                    else
                    {
                        await _signInManager.SignInAsync(user, isPersistent: false);
                        return RedirectToAction("Dashboard", "Customer");
                    }
                }
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }
            return View(model);
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (ModelState.IsValid)
            {
                // Try finding user by Email or Username
                var user = await _userManager.FindByEmailAsync(model.UsernameOrEmail) 
                           ?? await _userManager.FindByNameAsync(model.UsernameOrEmail);

                if (user != null)
                {
                    var roles = await _userManager.GetRolesAsync(user);

                    // Check if Farmer is approved by Admin
                    if (roles.Contains("Farmer") && !user.IsApprovedByAdmin)
                    {
                        if (!string.IsNullOrEmpty(user.KycRejectionReason))
                        {
                            ModelState.AddModelError(string.Empty, $"Your Farmer KYC verification was REJECTED by Admin. Reason: {user.KycRejectionReason}. Please contact support.");
                        }
                        else
                        {
                            ModelState.AddModelError(string.Empty, "Your Farmer account is currently under Admin verification. Please wait for Admin approval before logging in.");
                        }
                        return View(model);
                    }

                    var result = await _signInManager.PasswordSignInAsync(user.UserName!, model.Password, model.RememberMe, lockoutOnFailure: false);
                    if (result.Succeeded)
                    {
                        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                        {
                            return Redirect(returnUrl);
                        }

                        if (roles.Contains("Admin"))
                            return RedirectToAction("Dashboard", "Admin");
                        if (roles.Contains("Farmer"))
                            return RedirectToAction("Dashboard", "Farmer");
                        
                        return RedirectToAction("Dashboard", "Customer");
                    }
                }
                
                ModelState.AddModelError(string.Empty, "Invalid username/email or password.");
            }
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user == null)
                {
                    // Don't reveal that the user does not exist
                    return RedirectToAction("ForgotPasswordConfirmation");
                }

                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var callbackUrl = Url.Action("ResetPassword", "Account", new { token, email = user.Email }, Request.Scheme);
                
                // MOCK EMAIL SENDER: Since we don't have SMTP, we just pass the link via TempData to display it on screen
                TempData["ResetLink"] = callbackUrl;
                
                return RedirectToAction("ForgotPasswordConfirmation");
            }

            return View(model);
        }

        [HttpGet]
        public IActionResult ForgotPasswordConfirmation()
        {
            return View();
        }

        [HttpGet]
        public IActionResult ResetPassword(string? token = null, string? email = null)
        {
            if (token == null || email == null)
            {
                return BadRequest("A code must be supplied for password reset.");
            }
            var model = new ResetPasswordViewModel { Token = token, Email = email };
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                // Don't reveal that the user does not exist
                return RedirectToAction("Login");
            }

            var result = await _userManager.ResetPasswordAsync(user, model.Token, model.Password);
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "Your password has been reset successfully. Please log in.";
                return RedirectToAction("Login");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
        }
    }
}
