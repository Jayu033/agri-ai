namespace AgriAI.Web.ViewModels
{
    public class CreateRazorpayOrderRequest
    {
        public int? ProductId { get; set; }
        public int Quantity { get; set; } = 1;
        public bool IsCart { get; set; } = false;
        public string TransportType { get; set; } = "Direct Farm Pickup";
        public string DeliveryAddress { get; set; } = string.Empty;
    }

    public class VerifyRazorpayPaymentRequest
    {
        public string RazorpayPaymentId { get; set; } = string.Empty;
        public string RazorpayOrderId { get; set; } = string.Empty;
        public string RazorpaySignature { get; set; } = string.Empty;

        public int? ProductId { get; set; }
        public int Quantity { get; set; } = 1;
        public bool IsCart { get; set; } = false;
        public string TransportType { get; set; } = "Direct Farm Pickup";
        public string DeliveryAddress { get; set; } = string.Empty;
    }
}
