using System;

namespace AgriAI.Domain.Entities
{
    public class SupportTicket
    {
        public int Id { get; set; }
        public string TicketNumber { get; set; } = string.Empty; // e.g. TKT-00101
        public string? UserId { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public string SenderEmail { get; set; } = string.Empty;
        public string? SenderPhone { get; set; }
        public string UserRole { get; set; } = "Guest"; // Guest, Farmer, Customer, Admin
        
        public string Category { get; set; } = "General"; // Escrow & Payout, Quality Issue, Delivery Delay, Account KYC, General
        public int? OrderId { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? AttachmentPath { get; set; }
        public string Priority { get; set; } = "Medium"; // Low, Medium, High, Urgent
        
        public string Status { get; set; } = "Open"; // Open, In Progress, Resolved, Closed
        public string? AdminResponse { get; set; }
        public string? ResolvedByAdminId { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
