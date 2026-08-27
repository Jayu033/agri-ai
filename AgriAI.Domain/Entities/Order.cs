using AgriAI.Domain.Common;

namespace AgriAI.Domain.Entities
{
    public class Order : BaseEntity
    {
        public string CustomerId { get; set; } = string.Empty;
        
        public DateTime OrderDate { get; set; } = DateTime.UtcNow;
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = "Pending"; // Pending, Ready for Pickup, Delivered, Completed, Cancelled
        
        public string PaymentMethod { get; set; } = "Online"; // Online (Escrow), COD (Cash on Delivery)
        public string DeliveryOtp { get; set; } = "1234"; // 4-digit verification code
        public string TransportType { get; set; } = "Direct Farm Pickup"; // Direct Farm Pickup, Porter / Local Transport
        public decimal LogisticsFee { get; set; } = 0;
        public decimal PlatformFee { get; set; } = 0;
        public string ShippingAddress { get; set; } = "Farm Location / Gujarat";
        
        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
        public Payment? Payment { get; set; }
    }
}
