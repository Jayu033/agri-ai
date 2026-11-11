using AgriAI.Domain.Entities;

namespace AgriAI.Web.Services
{
    public interface IWhatsAppNotificationService
    {
        Task<(bool Success, string MessageId, string ErrorMessage)> SendMessageAsync(string toPhoneNumber, string messageBody);
        
        Task<(bool Success, string MessageId, string ErrorMessage)> SendOrderNotificationToFarmerAsync(
            Order order, string farmerPhone, string farmerName, string buyerName, decimal farmerEarnings, string buyerPhone = "", string farmerEmail = "");
        
        Task<(bool Success, string MessageId, string ErrorMessage)> SendOrderConfirmationToBuyerAsync(
            Order order, string buyerPhone, string buyerName, string farmerName, string farmerPhone = "", string pickupAddress = "", string buyerEmail = "");
        
        Task<(bool Success, string MessageId, string ErrorMessage)> SendProduceReadyNotificationAsync(
            Order order, string buyerPhone, string buyerName, string farmerName, string farmerPhone, string pickupAddress, string buyerEmail = "");
        
        Task<(bool Success, string MessageId, string ErrorMessage)> SendProduceDispatchedNotificationAsync(
            Order order, string buyerPhone, string buyerName, string farmerName, string farmerPhone, string cargoServiceName, string biltyNumber, string buyerEmail = "");
        
        Task<(bool Success, string MessageId, string ErrorMessage)> SendRegistrationOtpAsync(
            string phoneNumber, string otpCode, string role, string email = "");
            
        Task<(bool Success, string MessageId, string ErrorMessage)> SendFarmerKycApprovedNotificationAsync(
            string farmerPhone, string farmerName, string farmerEmail = "");
    }
}
