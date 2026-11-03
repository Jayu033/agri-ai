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

        // 1. Farmer gets incoming order notification when Customer pays
        public async Task<(bool Success, string MessageId, string ErrorMessage)> SendOrderNotificationToFarmerAsync(
            Order order, string farmerPhone, string farmerName, string buyerName, decimal farmerEarnings, string buyerPhone = "")
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
            if (!string.IsNullOrEmpty(buyerPhone))
            {
                sb.AppendLine($"• *Phone / સંપર્ક:* {buyerPhone}");
            }
            sb.AppendLine($"• *Location / સ્થળ:* {deliveryAddr}");
            sb.AppendLine();
            sb.AppendLine("⏳ *Action Required / આગળનું પગલું:*");
            sb.AppendLine("Please review order & mark ready on portal when harvest packaging is done.");
            sb.AppendLine("_કૃપા કરીને ઓર્ડર ચકાસી ખેતરે ઉપજ તૈયાર કરી પોર્ટલ પર 'Ready for Pickup' માર્ક કરવા વિનંતી._");
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine("🚜 *AgriAI - Direct Gujarat Farm-to-Consumer Portal* 🌾");

            return await SendMessageAsync(farmerPhone, sb.ToString());
        }

        // 2. Customer gets strictly Payment Successful & Order Confirmed (NO OTP)
        public async Task<(bool Success, string MessageId, string ErrorMessage)> SendOrderConfirmationToBuyerAsync(
            Order order, string buyerPhone, string buyerName, string farmerName, string farmerPhone = "", string pickupAddress = "")
        {
            var firstItem = order.OrderItems.FirstOrDefault();
            var cropName = firstItem?.Product?.Name ?? "ખેતી પાક (Produce)";
            int totalQty = order.OrderItems.Sum(oi => oi.Quantity);
            if (order.OrderItems.Count > 1)
            {
                cropName += $" (+{order.OrderItems.Count - 1} varieties)";
            }

            var safeBuyerName = string.IsNullOrWhiteSpace(buyerName) ? "Customer" : buyerName.Trim();
            var safeFarmerName = string.IsNullOrWhiteSpace(farmerName) ? "ખેડૂત મિત્ર" : farmerName.Trim();

            var sb = new StringBuilder();
            sb.AppendLine("🌾 *AGRIAI PAYMENT SUCCESSFUL* 🌾");
            sb.AppendLine("_પેમેન્ટ અને ઓર્ડર સફળતાપૂર્વક કન્ફર્મ થયેલ છે_");
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine($"Hello / નમસ્તે *{safeBuyerName}* જી,");
            sb.AppendLine("Your payment has been successfully received and order is confirmed!");
            sb.AppendLine("_તમારું પેમેન્ટ સફળતાપૂર્વક થઈ ગયું છે અને ઓર્ડર કન્ફર્મ થઈ ગયો છે!_");
            sb.AppendLine();
            sb.AppendLine("📋 *ORDER SUMMARY / ઓર્ડર વિગતો:*");
            sb.AppendLine($"• *Order ID / ઓર્ડર નં:* #{order.Id:D5}");
            sb.AppendLine($"• *Produce / પાક:* *{cropName}* ({totalQty} Kg)");
            sb.AppendLine($"• *Farmer / ખેડૂત:* *{safeFarmerName}*");
            sb.AppendLine($"• *Total Paid / ચૂકવેલ રકમ:* *₹{order.TotalAmount:N2}*");
            sb.AppendLine("• *Status / સ્થિતિ:* 100% Escrow Secured ✅");
            sb.AppendLine();
            sb.AppendLine("🌾 *Next Step / હવે પછીનું પગલું:*");
            sb.AppendLine("The farmer is packing your produce. You will receive a WhatsApp message as soon as it is 'Ready for Pickup'.");
            sb.AppendLine("_ખેડૂત ભાઈ તમારો પાક તૈયાર કરશે એટલે તમને તરત જ WhatsApp મેસેજ દ્વારા જાણ કરવામાં આવશે._");
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine("🚜 *AgriAI - Pure Direct Farm Marketplace* 🌾");

            return await SendMessageAsync(buyerPhone, sb.ToString());
        }

        // 3. Customer gets Ready for Pickup notification when Farmer marks ready (NO OTP)
        public async Task<(bool Success, string MessageId, string ErrorMessage)> SendProduceReadyNotificationAsync(
            Order order, string buyerPhone, string buyerName, string farmerName, string farmerPhone, string pickupAddress)
        {
            var firstItem = order.OrderItems.FirstOrDefault();
            var cropName = firstItem?.Product?.Name ?? "ખેતી પાક (Produce)";
            int totalQty = order.OrderItems.Sum(oi => oi.Quantity);
            if (order.OrderItems.Count > 1)
            {
                cropName += $" (+{order.OrderItems.Count - 1} varieties)";
            }

            var safeBuyerName = string.IsNullOrWhiteSpace(buyerName) ? "Customer" : buyerName.Trim();
            var safeFarmerName = string.IsNullOrWhiteSpace(farmerName) ? "ખેડૂત મિત્ર" : farmerName.Trim();
            var farmLoc = string.IsNullOrWhiteSpace(pickupAddress) ? (order.ShippingAddress ?? "Gujarat Farm Location") : pickupAddress;

            var sb = new StringBuilder();
            sb.AppendLine("🌾 *AGRIAI - PRODUCE READY FOR PICKUP* 🌾");
            sb.AppendLine("_તમારો ખેતી પાક ખેતરે તૈયાર છે!_");
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine($"Hello / નમસ્તે *{safeBuyerName}* જી,");
            sb.AppendLine($"Great news! Farmer *{safeFarmerName}* has harvested and packed your produce!");
            sb.AppendLine($"_ખેડૂત ભાઈએ તમારો પાક પેક કરીને ખેતરે તૈયાર કરી લીધો છે!_");
            sb.AppendLine();
            sb.AppendLine("📋 *ORDER DETAILS / ઓર્ડર વિગતો:*");
            sb.AppendLine($"• *Order ID / ઓર્ડર નં:* #{order.Id:D5}");
            sb.AppendLine($"• *Produce / પાક:* *{cropName}* ({totalQty} Kg)");
            sb.AppendLine($"• *Farmer / ખેડૂત:* *{safeFarmerName}*");
            if (!string.IsNullOrEmpty(farmerPhone))
            {
                sb.AppendLine($"• *Farmer Phone / ખેડૂત ફોન:* {farmerPhone}");
            }
            sb.AppendLine();
            sb.AppendLine("📍 *PICKUP FARM LOCATION / ખેતરનું સ્થળ:*");
            sb.AppendLine($"• {farmLoc}");
            sb.AppendLine();
            sb.AppendLine("🚜 *Collection Note / સૂચના:*");
            sb.AppendLine("Please collect your fresh produce from the farm location mentioned above.");
            sb.AppendLine("_કૃપા કરીને ઉપર જણાવેલ સ્થળેથી તમારો તાજો ખેતી પાક મેળવી લેવા વિનંતી._");
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine("🚜 *AgriAI - Direct Gujarat Farm-to-Consumer Portal* 🌾");

            return await SendMessageAsync(buyerPhone, sb.ToString());
        }

        // 4. Customer gets Dispatched notification when Farmer dispatches via Cargo/Bus (NO OTP)
        public async Task<(bool Success, string MessageId, string ErrorMessage)> SendProduceDispatchedNotificationAsync(
            Order order, string buyerPhone, string buyerName, string farmerName, string farmerPhone, string cargoServiceName, string biltyNumber)
        {
            var firstItem = order.OrderItems.FirstOrDefault();
            var cropName = firstItem?.Product?.Name ?? "ખેતી પાક (Produce)";
            int totalQty = order.OrderItems.Sum(oi => oi.Quantity);
            if (order.OrderItems.Count > 1)
            {
                cropName += $" (+{order.OrderItems.Count - 1} varieties)";
            }

            var safeBuyerName = string.IsNullOrWhiteSpace(buyerName) ? "Customer" : buyerName.Trim();
            var safeFarmerName = string.IsNullOrWhiteSpace(farmerName) ? "ખેડૂત મિત્ર" : farmerName.Trim();
            var safeCargo = string.IsNullOrWhiteSpace(cargoServiceName) ? "GSRTC ST Bus Parcel / Cargo" : cargoServiceName;
            var safeBilty = string.IsNullOrWhiteSpace(biltyNumber) ? "LR-Direct" : biltyNumber;

            var sb = new StringBuilder();
            sb.AppendLine("🚚 *AGRIAI PRODUCE DISPATCHED* 🚚");
            sb.AppendLine("_તમારો પાક ટ્રાન્સપોર્ટ / પાર્સલ દ્વારા રવાના થઈ ગયો છે!_");
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine($"Hello / નમસ્તે *{safeBuyerName}* જી,");
            sb.AppendLine($"Farmer *{safeFarmerName}* has dispatched your order via cargo logistics!");
            sb.AppendLine($"_ખેડૂત ભાઈએ તમારો પાક પાર્સલ સર્વિસ દ્વારા રવાના કરી દીધો છે._");
            sb.AppendLine();
            sb.AppendLine("📋 *DISPATCH DETAILS / ડિસ્પેચ વિગતો:*");
            sb.AppendLine($"• *Order ID / ઓર્ડર નં:* #{order.Id:D5}");
            sb.AppendLine($"• *Produce / પાક:* *{cropName}* ({totalQty} Kg)");
            sb.AppendLine($"• *Transport / વાહન:* *{safeCargo}*");
            sb.AppendLine($"• *Bilty / LR No:* *{safeBilty}*");
            if (!string.IsNullOrEmpty(farmerPhone))
            {
                sb.AppendLine($"• *Farmer Contact / ખેડૂત ફોન:* {farmerPhone}");
            }
            sb.AppendLine($"• *Delivery Address / સ્થળ:* {order.ShippingAddress}");
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine("🚜 *AgriAI - Direct Gujarat Farm-to-Consumer Portal* 🌾");

            return await SendMessageAsync(buyerPhone, sb.ToString());
        }

        // 5. Account Registration OTP
        public async Task<(bool Success, string MessageId, string ErrorMessage)> SendRegistrationOtpAsync(string phoneNumber, string otpCode, string role)
        {
            var roleName = string.Equals(role, "Farmer", StringComparison.OrdinalIgnoreCase) 
                ? "ખેડૂત ભાઈ (Farmer)" 
                : "ગ્રાહક (Customer)";

            var sb = new StringBuilder();
            sb.AppendLine("🌾 *AGRIAI ACCOUNT SECURITY OTP* 🌾");
            sb.AppendLine("_એગ્રીએઆઈ એકાઉન્ટ સુરક્ષા વેરિફિકેશન કોડ_");
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine($"Hello / નમસ્તે *{roleName}*,");
            sb.AppendLine("Welcome to AgriAI! Here is your official registration verification code:");
            sb.AppendLine("_AgriAI માં તમારું સ્વાગત છે! તમારો સત્તાવાર એકાઉન્ટ વેરિફિકેશન કોડ:_");
            sb.AppendLine();
            sb.AppendLine("🔐 *YOUR OTP CODE / તમારો ઓટીપી:*");
            sb.AppendLine($"👉 *{otpCode}* 👈");
            sb.AppendLine();
            sb.AppendLine("⏱️ *Valid for 5 minutes only.*");
            sb.AppendLine("_આ કોડ ફક્ત ૫ મિનિટ માટે જ માન્ય રહેશે._");
            sb.AppendLine();
            sb.AppendLine("⚠️ *Security Warning:*");
            sb.AppendLine("Do NOT share this code with anyone. AgriAI representatives never ask for your OTP.");
            sb.AppendLine("_સુરક્ષા ખાતર આ કોડ કોઈ સાથે શેર કરવો નહીં._");
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine("🚜 *AgriAI - Direct Gujarat Farm-to-Consumer Portal* 🌾");

            return await SendMessageAsync(phoneNumber, sb.ToString());
        }
    }
}
