using AgriAI.Domain.Common;

namespace AgriAI.Domain.Entities
{
    public class Order : BaseEntity
    {
        public string CustomerId { get; set; } = string.Empty;
        
        public DateTime OrderDate { get; set; } = DateTime.UtcNow;
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = "Requested"; // Requested, Accepted, Declined, Cancelled, Ready for Pickup, Delivered & Settled
        
        public string PaymentMethod { get; set; } = "Online"; // Online (Escrow), COD (Cash on Delivery)
        public string DeliveryOtp { get; set; } = "1234"; // 4-digit verification code
        public string TransportType { get; set; } = "Direct Farm Pickup"; // Direct Farm Pickup, Porter / Local Transport
        public decimal LogisticsFee { get; set; } = 0;
        public decimal PlatformFee { get; set; } = 0;
        public string ShippingAddress { get; set; } = "Farm Location / Gujarat";

        // Step 2: Farmer Acceptance & 3-Hour Customer Cancellation Window
        public DateTime? FarmerAcceptanceAt { get; set; }
        public DateTime? CancellationDeadline { get; set; }
        public string? FarmerDeclineReason { get; set; }
        public string? CustomerCancelReason { get; set; }
        public bool IsCustomerCancelled { get; set; } = false;
        public bool IsFarmerDeclined { get; set; } = false;

        // Step 5 & 6: Logistics GSRTC / Cargo Bilty Dispatch & Payout Settlement
        public string? BiltyImageUrl { get; set; }
        public string? BiltyNumber { get; set; }
        public string? CargoServiceName { get; set; }
        public DateTime? DispatchedAt { get; set; }
        public DateTime? DeliveredAt { get; set; }
        public bool AdminApprovedPayout { get; set; } = false;
        public string? AdminPayoutReason { get; set; }
        
        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
        public Payment? Payment { get; set; }
    }
}
