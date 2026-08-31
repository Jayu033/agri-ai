using System.ComponentModel.DataAnnotations;

namespace AgriAI.Web.ViewModels
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Username or Email is required.")]
        [Display(Name = "Username or Email")]
        public string UsernameOrEmail { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Remember me?")]
        public bool RememberMe { get; set; }
    }

    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Username is required.")]
        [Display(Name = "Username")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "First Name is required.")]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Last Name is required.")]
        [Display(Name = "Last Name")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Phone Number")]
        [Phone]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required.")]
        [DataType(DataType.Password)]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select your role.")]
        [Display(Name = "Register As")]
        public string Role { get; set; } = "Customer"; // Customer or Farmer

        // Optional / Farmer KYC & Payout Fields
        [Display(Name = "Farm / Street Address")]
        public string? Address { get; set; }

        [Display(Name = "City / District / Village")]
        public string? City { get; set; }

        [Display(Name = "Bank Name")]
        public string? BankName { get; set; }

        [Display(Name = "Bank Account Number")]
        [RegularExpression(@"^[0-9]{9,18}$", ErrorMessage = "Bank Account Number must be 9 to 18 numeric digits.")]
        public string? BankAccountNumber { get; set; }

        [Display(Name = "IFSC Code")]
        [RegularExpression(@"^[A-Z]{4}0[A-Z0-9]{6}$", ErrorMessage = "Please enter a valid 11-character uppercase IFSC code (e.g. SBIN0001234).")]
        public string? BankIfscCode { get; set; }

        [Display(Name = "UPI ID (e.g. 9724560047@upi)")]
        [RegularExpression(@"^[a-zA-Z0-9.\-_]{2,256}@[a-zA-Z]{2,64}$", ErrorMessage = "Please enter a valid UPI ID (e.g. 9876543210@upi).")]
        public string? UpiId { get; set; }

        [Display(Name = "KYC Document Type")]
        public string? KycDocumentType { get; set; } = "7/12 (Satbara)";

        [Display(Name = "Upload 7/12 (Satbara) or Aadhaar Card")]
        public IFormFile? KycDocumentFile { get; set; }

        [Display(Name = "Profile Photo")]
        public IFormFile? ProfilePictureFile { get; set; }

        public string? CapturedProfilePictureBase64 { get; set; }
    }

    public class ProfileViewModel
    {
        [Required(ErrorMessage = "Username is required.")]
        [Display(Name = "Username")]
        public string Username { get; set; } = string.Empty;

        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [Display(Name = "Last Name")]
        public string LastName { get; set; } = string.Empty;
        
        [Display(Name = "Email Address")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "You must enter your current password to save changes.")]
        [DataType(DataType.Password)]
        [Display(Name = "Current Password")]
        public string CurrentPassword { get; set; } = string.Empty;
    }

    public class ForgotPasswordViewModel
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }

    public class ResetPasswordViewModel
    {
        [Required]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Token { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required.")]
        [DataType(DataType.Password)]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class UserManagementViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string FullName => string.IsNullOrWhiteSpace($"{FirstName} {LastName}") ? Username : $"{FirstName} {LastName}".Trim();
        public string? ProfilePicturePath { get; set; }
        public string Role { get; set; } = string.Empty;
        public bool IsBlocked { get; set; }
        public DateTime CreatedAt { get; set; }

        // Farmer KYC & Verification
        public bool IsApprovedByAdmin { get; set; } = true;
        public string? KycRejectionReason { get; set; }
        public string? City { get; set; }
        public string? Address { get; set; }
        public string? BankName { get; set; }
        public string? BankAccountNumber { get; set; }
        public string? BankIfscCode { get; set; }
        public string? UpiId { get; set; }
        public string? KycDocumentPath { get; set; }
        public string? KycDocumentType { get; set; }
        public DateTime? KycSubmittedAt { get; set; }
    }
}
