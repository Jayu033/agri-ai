using AgriAI.Domain.Entities;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AgriAI.Web.Services
{
    public class WhatsAppNotificationService : IWhatsAppNotificationService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<WhatsAppNotificationService> _logger;

        private readonly string _provider;
        private readonly string _apiHost;
        private readonly string _instanceId;
        private readonly string _token;
        private readonly bool _isEnabled;

        public WhatsAppNotificationService(HttpClient httpClient, IConfiguration configuration, ILogger<WhatsAppNotificationService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;

            _provider = _configuration["WhatsApp:Provider"] ?? "GreenAPI";
            _apiHost = _configuration["WhatsApp:ApiUrl"] ?? "https://api.green-api.com";
            _instanceId = _configuration["WhatsApp:InstanceId"] ?? "";
            _token = _configuration["WhatsApp:Token"] ?? "";
            _isEnabled = _configuration.GetValue<bool>("WhatsApp:IsEnabled", true);
        }

        private string CleanPhoneNumber(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return string.Empty;

            var sb = new StringBuilder();
            foreach (var ch in phone)
            {
                if (char.IsDigit(ch)) sb.Append(ch);
            }

            var clean = sb.ToString();
            // If Indian 10-digit number without country code, prepend 91
            if (clean.Length == 10)
            {
                clean = "91" + clean;
            }
            else if (clean.Length == 11 && clean.StartsWith("0"))
            {
                clean = "91" + clean[1..];
            }

            return clean;
        }

        public async Task<(bool Success, string MessageId, string ErrorMessage)> SendMessageAsync(string toPhoneNumber, string messageBody)
        {
            try
            {
                var cleanPhone = CleanPhoneNumber(toPhoneNumber);
                if (string.IsNullOrWhiteSpace(cleanPhone))
                {
                    _logger.LogWarning("WhatsApp Send skipped: Invalid/empty phone number '{Phone}'.", toPhoneNumber);
                    return (false, string.Empty, "Invalid phone number.");
                }

                // If not configured or disabled, log as simulation / sandbox
                if (!_isEnabled || string.IsNullOrWhiteSpace(_instanceId) || string.IsNullOrWhiteSpace(_token) || _instanceId.Contains("YOUR_"))
                {
                    _logger.LogInformation("📲 [SIMULATED WHATSAPP NOTIFICATION to +{Phone} via {Provider}]:\n{Message}", cleanPhone, _provider, messageBody);
                    return (true, "simulated_" + Guid.NewGuid().ToString("N")[..8], string.Empty);
                }

                // 1. GREEN-API (Free Developer Tier)
                if (string.Equals(_provider, "GreenAPI", StringComparison.OrdinalIgnoreCase))
                {
                    var host = string.IsNullOrWhiteSpace(_apiHost) ? "https://api.green-api.com" : _apiHost.TrimEnd('/');
                    var endpoint = $"{host}/waInstance{_instanceId}/sendMessage/{_token}";

                    var payload = new
                    {
                        chatId = $"{cleanPhone}@c.us",
                        message = messageBody
                    };

                    var jsonString = JsonSerializer.Serialize(payload);
                    using var content = new StringContent(jsonString, Encoding.UTF8, "application/json");

                    var response = await _httpClient.PostAsync(endpoint, content);
                    var responseString = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogError("Green-API returned status {Status}: {Body}", response.StatusCode, responseString);
                        return (false, string.Empty, $"Green-API error ({response.StatusCode}): {responseString}");
                    }

                    var jsonNode = JsonNode.Parse(responseString);
                    var idMessage = jsonNode?["idMessage"]?.ToString();

                    if (!string.IsNullOrEmpty(idMessage))
                    {
                        _logger.LogInformation("✅ Automated Green-API WhatsApp message delivered to +{Phone}. ID: {MsgId}", cleanPhone, idMessage);
                        return (true, idMessage, string.Empty);
                    }

                    _logger.LogWarning("Green-API message response: {Body}", responseString);
                    return (true, "greenapi_ok", string.Empty);
                }
                else
                {
                    // 2. UltraMsg REST API Fallback
                    var endpoint = $"https://api.ultramsg.com/{_instanceId}/messages/chat";

                    var formValues = new Dictionary<string, string>
                    {
                        { "token", _token },
                        { "to", cleanPhone },
                        { "body", messageBody }
                    };

                    using var formContent = new FormUrlEncodedContent(formValues);
                    var response = await _httpClient.PostAsync(endpoint, formContent);
                    var responseString = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogError("UltraMsg API returned status {Status}: {Body}", response.StatusCode, responseString);
                        return (false, string.Empty, $"UltraMsg API error ({response.StatusCode}): {responseString}");
                    }

                    var jsonNode = JsonNode.Parse(responseString);
                    var isSent = jsonNode?["sent"]?.ToString();
                    var messageId = jsonNode?["id"]?.ToString() ?? "msg_ok";

                    if (string.Equals(isSent, "true", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation("✅ Automated UltraMsg WhatsApp message delivered to +{Phone}. ID: {MsgId}", cleanPhone, messageId);
                        return (true, messageId, string.Empty);
                    }

                    var errMsg = jsonNode?["message"]?.ToString() ?? responseString;
                    _logger.LogWarning("UltraMsg reported unsent: {Error}", errMsg);
                    return (false, string.Empty, errMsg);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in WhatsAppNotificationService.SendMessageAsync.");
                return (false, string.Empty, ex.Message);
            }
        }

        public async Task<(bool Success, string MessageId, string ErrorMessage)> SendOrderNotificationToFarmerAsync(
            Order order, string farmerPhone, string farmerName, string buyerName, decimal farmerEarnings)
        {
            var firstItem = order.OrderItems.FirstOrDefault();
            var cropName = firstItem?.Product?.Name ?? "ખેતી પાક (Produce)";
            int totalQty = order.OrderItems.Sum(oi => oi.Quantity);
            if (order.OrderItems.Count > 1)
            {
                cropName += $" (+{order.OrderItems.Count - 1} varieties)";
            }

            var rzpRef = order.Payment != null && !string.IsNullOrEmpty(order.Payment.RazorpayPaymentId) 
                ? order.Payment.RazorpayPaymentId 
                : (order.PaymentMethod == "Cash on Delivery" ? "COD (Pay on Pickup)" : "Escrow Protected");

            var deliveryAddr = string.IsNullOrWhiteSpace(order.ShippingAddress) ? "Direct Farm Pickup (Gujarat Field/Godown)" : order.ShippingAddress;
            var safeFarmerName = string.IsNullOrWhiteSpace(farmerName) ? "ખેડૂત ભાઈ" : farmerName.Trim();
            var safeBuyerName = string.IsNullOrWhiteSpace(buyerName) ? "Verified Buyer" : buyerName.Trim();

            var sb = new StringBuilder();
            sb.AppendLine("🌾 *AGRIAI PRODUCE ORDER RECEIPT* 🌾");
            sb.AppendLine("_ખેતી પાક ઓર્ડર સત્તાવાર રસીદ_");
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine($"Hello / નમસ્તે *{safeFarmerName}* જી,");
            sb.AppendLine("A new direct farm crop order has been placed!");
            sb.AppendLine("_તમારી પાસેથી પાક ખરીદીનો નવો ઓર્ડર મળ્યો છે!_");
            sb.AppendLine();
            sb.AppendLine("📋 *ORDER DETAILS / ઓર્ડર વિગતો:*");
            sb.AppendLine($"• *Order ID / ઓર્ડર નં:* #{order.Id:D5}");
            sb.AppendLine($"• *Produce / પાક:* *{cropName}* ({totalQty} Kg)");
            sb.AppendLine($"• *Total Bill / કુલ રકમ:* *₹{order.TotalAmount:N2}*");
            sb.AppendLine($"• *Your Earnings / તમારી આવક:* *₹{farmerEarnings:N2}*");
            sb.AppendLine($"• *Logistics / ડિલિવરી:* {order.TransportType ?? "Direct Farm Pickup"}");
            sb.AppendLine();
            sb.AppendLine("💳 *ESCROW PROTECTION / પેમેન્ટ સુરક્ષા:*");
            if (order.PaymentMethod != "Cash on Delivery")
            {
                sb.AppendLine("✅ *100% Payment Successful (Razorpay Secured)*");
                sb.AppendLine($"🔒 *₹{farmerEarnings:N2}* is safely locked in AgriAI Escrow Vault!");
                sb.AppendLine("_તમારા પૈસા એસ્ક્રો વૉલ્ટમાં ૧૦૦% સુરક્ષિત છે. માલ મળતાં જ ખાતામાં જમા થશે._");
                sb.AppendLine($"• *Ref ID:* {rzpRef}");
            }
            else
            {
                sb.AppendLine("💵 *Cash on Delivery (Pay on Pickup)*");
                sb.AppendLine($"_ગ્રાહક ખેતરે માલ લેવા આવે ત્યારે ₹{farmerEarnings:N2} રોકડા મેળવી લેવા._");
            }
            sb.AppendLine();
            sb.AppendLine("📍 *BUYER INFO / ગ્રાહક વિગતો:*");
            sb.AppendLine($"• *Name / નામ:* {safeBuyerName}");
            sb.AppendLine($"• *Location / સ્થળ:* {deliveryAddr}");
            sb.AppendLine();
            sb.AppendLine("⏳ Please review order & prepare harvest packaging.");
            sb.AppendLine("_કૃપા કરીને ઓર્ડર ચકાસી ખેતરે ઉપજ તૈયાર રાખવા વિનંતી._");
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine("🚜 *AgriAI - Direct Gujarat Farm-to-Consumer Portal* 🌾");

            return await SendMessageAsync(farmerPhone, sb.ToString());
        }

        public async Task<(bool Success, string MessageId, string ErrorMessage)> SendOrderConfirmationToBuyerAsync(
            Order order, string buyerPhone, string buyerName, string farmerName)
        {
            var firstItem = order.OrderItems.FirstOrDefault();
            var cropName = firstItem?.Product?.Name ?? "ખેતી પાક (Produce)";
            int totalQty = order.OrderItems.Sum(oi => oi.Quantity);

            var sb = new StringBuilder();
            sb.AppendLine("🌾 *AGRIAI ORDER CONFIRMATION* 🌾");
            sb.AppendLine("_ઓર્ડર કન્ફર્મેશન રસીદ_");
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine($"Hello / નમસ્તે *{buyerName}* જી,");
            sb.AppendLine("Thank you for buying direct from our Gujarat Farmers!");
            sb.AppendLine("_ખેડૂત પાસેથી સીધી ખરીદી કરવા બદલ તમારો ખૂબ ખૂબ આભાર._");
            sb.AppendLine();
            sb.AppendLine("📋 *ORDER SUMMARY:*");
            sb.AppendLine($"• *Order ID:* #{order.Id:D5}");
            sb.AppendLine($"• *Produce:* *{cropName}* ({totalQty} Kg)");
            sb.AppendLine($"• *Farmer / ખેડૂત:* *{farmerName}*");
            sb.AppendLine($"• *Total Paid:* *₹{order.TotalAmount:N2}*");
            sb.AppendLine("• *Status:* 100% Escrow Secured ✅");
            sb.AppendLine();
            sb.AppendLine("Track your order live on the AgriAI portal!");
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine("🚜 *AgriAI - Pure Direct Farm Marketplace* 🌾");

            return await SendMessageAsync(buyerPhone, sb.ToString());
        }
    }
}
