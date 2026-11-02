using AgriAI.Domain.Entities;

namespace AgriAI.Web.Services
{
    public interface IWhatsAppNotificationService
    {
        Task<(bool Success, string MessageId, string ErrorMessage)> SendMessageAsync(string toPhoneNumber, string messageBody);
        Task<(bool Success, string MessageId, string ErrorMessage)> SendOrderNotificationToFarmerAsync(Order order, string farmerPhone, string farmerName, string buyerName, decimal farmerEarnings);
        Task<(bool Success, string MessageId, string ErrorMessage)> SendOrderConfirmationToBuyerAsync(Order order, string buyerPhone, string buyerName, string farmerName);
        Task<(bool Success, string MessageId, string ErrorMessage)> SendRegistrationOtpAsync(string phoneNumber, string otpCode, string role);
    }
}
