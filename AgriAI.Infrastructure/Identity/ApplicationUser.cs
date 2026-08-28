using Microsoft.AspNetCore.Identity;

namespace AgriAI.Infrastructure.Identity
{
    public class ApplicationUser : IdentityUser
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string ZipCode { get; set; } = string.Empty;
        public string? ProfilePicturePath { get; set; }
        
        // Farmer KYC & Payout Bank Details
        public string? BankName { get; set; }
        public string? BankAccountNumber { get; set; }
        public string? BankIfscCode { get; set; }
        public string? UpiId { get; set; }
        public string? KycDocumentPath { get; set; }
        public string? KycDocumentType { get; set; }
        public bool IsApprovedByAdmin { get; set; } = true;
        public string? KycRejectionReason { get; set; }
        public DateTime? KycSubmittedAt { get; set; }
        public DateTime? KycApprovedAt { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
