namespace AgriAI.Web.Services
{
    public interface IRazorpayPaymentService
    {
        string GetKeyId();
        Task<(bool Success, string RazorpayOrderId, string ErrorMessage)> CreateOrderAsync(decimal amountInInr, string receiptId, string? notes = null);
        bool VerifyPaymentSignature(string razorpayOrderId, string razorpayPaymentId, string razorpaySignature);
        Task<(bool Success, string RefundId, string ErrorMessage)> RefundPaymentAsync(string razorpayPaymentId, decimal? amountInInr = null);
    }
}
