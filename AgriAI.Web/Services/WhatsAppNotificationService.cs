using AgriAI.Domain.Entities;
using System.Net;
using System.Net.Mail;
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

            _provider = _configuration["WhatsApp:Provider"] ?? "Disabled";
            _apiHost = _configuration["WhatsApp:ApiUrl"] ?? "";
            _instanceId = _configuration["WhatsApp:InstanceId"] ?? "";
            _token = _configuration["WhatsApp:Token"] ?? "";
            _isEnabled = _configuration.GetValue<bool>("WhatsApp:IsEnabled", false);
        }

        private async Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody, string? plainTextBody = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(toEmail)) return false;

                var isEmailEnabled = _configuration.GetValue<bool>("EmailSettings:IsEnabled", true);
                if (!isEmailEnabled) return false;

                var smtpServer = _configuration["EmailSettings:SmtpServer"] ?? "smtp.gmail.com";
                var port = _configuration.GetValue<int>("EmailSettings:Port", 587);
                var senderEmail = _configuration["EmailSettings:SenderEmail"] ?? "meetradadiya0166@gmail.com";
                var senderName = _configuration["EmailSettings:SenderName"] ?? "AgriAI Gujarat";
                var password = _configuration["EmailSettings:Password"] ?? "ybegmtysukxmjymc";

                using var client = new SmtpClient(smtpServer, port)
                {
                    Credentials = new NetworkCredential(senderEmail, password),
                    EnableSsl = true,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    Timeout = 15000
                };

                using var mailMessage = new MailMessage
                {
                    From = new MailAddress(senderEmail, senderName),
                    Subject = subject,
                    Body = htmlBody,
                    IsBodyHtml = true,
                    BodyEncoding = Encoding.UTF8,
                    SubjectEncoding = Encoding.UTF8
                };
                mailMessage.To.Add(toEmail.Trim());

                if (!string.IsNullOrWhiteSpace(plainTextBody))
                {
                    var plainView = AlternateView.CreateAlternateViewFromString(plainTextBody, Encoding.UTF8, "text/plain");
                    mailMessage.AlternateViews.Add(plainView);
                }
                var htmlView = AlternateView.CreateAlternateViewFromString(htmlBody, Encoding.UTF8, "text/html");
                mailMessage.AlternateViews.Add(htmlView);

                await client.SendMailAsync(mailMessage);
                _logger.LogInformation("✅ [Email Notification Delivered] To: {Email}, Subject: {Subject}", toEmail, subject);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [Email Send Failed] To: {Email}, Error: {Message}", toEmail, ex.Message);
                return false;
            }
        }

        private string BuildHtmlEmail(
            string badgeText,
            string badgeBg,
            string badgeColor,
            string badgeBorder,
            string recipientName,
            string introText,
            string? calloutHtml,
            IEnumerable<(string Label, string Value, bool IsHighlight)> rows,
            string? noteBoxHtml,
            string? buttonText = null,
            string? buttonUrl = null)
        {
            var sb = new StringBuilder();
            sb.Append(@"<!DOCTYPE html>
<html>
<head>
<meta charset=""utf-8"">
<meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
</head>
<body style=""margin:0;padding:20px;background-color:#f1f5f9;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;-webkit-font-smoothing:antialiased;"">
<table width=""100%"" border=""0"" cellspacing=""0"" cellpadding=""0"">
<tr><td align=""center"">
<table width=""100%"" border=""0"" cellspacing=""0"" cellpadding=""0"" style=""max-width:580px;background-color:#ffffff;border-radius:14px;overflow:hidden;border:1px solid #e2e8f0;box-shadow:0 4px 16px rgba(0,0,0,0.05);"">
<!-- Header -->
<tr><td style=""background:linear-gradient(135deg,#15803d 0%,#166534 100%);padding:24px 20px;text-align:center;color:#ffffff;"">
  <div style=""font-size:22px;font-weight:800;letter-spacing:0.5px;color:#ffffff;"">🌾 AgriAI Gujarat</div>
  <div style=""font-size:12px;color:#dcfce7;margin-top:4px;"">Direct Farm-to-Consumer Digital Marketplace</div>
</td></tr>
<!-- Body -->
<tr><td style=""padding:24px;color:#1e293b;font-size:14px;line-height:1.6;"">
  <div style=""margin-bottom:14px;"">
    <span style=""display:inline-block;padding:4px 12px;border-radius:9999px;font-size:11px;font-weight:700;text-transform:uppercase;letter-spacing:0.5px;background-color:" + badgeBg + @";color:" + badgeColor + @";border:1px solid " + badgeBorder + @";"">" + badgeText + @"</span>
  </div>
  <h2 style=""margin:0 0 8px;font-size:16px;font-weight:700;color:#0f172a;"">Hello <span style=""color:#15803d;"">" + System.Net.WebUtility.HtmlEncode(recipientName) + @"</span>,</h2>
  <p style=""margin:0 0 16px;font-size:13px;color:#475569;line-height:1.5;"">" + introText + @"</p>
");

            if (!string.IsNullOrEmpty(calloutHtml))
            {
                sb.Append(calloutHtml);
            }

            if (rows != null && rows.Any())
            {
                sb.Append(@"<table width=""100%"" border=""0"" cellspacing=""0"" cellpadding=""0"" style=""width:100%;border-collapse:collapse;margin:16px 0;background-color:#f8fafc;border-radius:10px;border:1px solid #e2e8f0;overflow:hidden;"">");
                foreach (var row in rows)
                {
                    var bg = row.IsHighlight ? "background-color:#f0fdf4;" : "";
                    var color = row.IsHighlight ? "#15803d" : "#0f172a";
                    var weight = row.IsHighlight ? "700" : "600";
                    sb.Append(@"<tr style=""" + bg + @""">
  <td style=""padding:10px 14px;font-size:13px;color:#64748b;border-bottom:1px solid #e2e8f0;width:42%;font-weight:500;"">" + System.Net.WebUtility.HtmlEncode(row.Label) + @"</td>
  <td style=""padding:10px 14px;font-size:13px;color:" + color + @";border-bottom:1px solid #e2e8f0;font-weight:" + weight + @";text-align:right;"">" + System.Net.WebUtility.HtmlEncode(row.Value) + @"</td>
</tr>");
                }
                sb.Append("</table>");
            }

            if (!string.IsNullOrEmpty(noteBoxHtml))
            {
                sb.Append(noteBoxHtml);
            }

            if (!string.IsNullOrEmpty(buttonText) && !string.IsNullOrEmpty(buttonUrl))
            {
                sb.Append(@"<div style=""text-align:center;margin-top:20px;"">
  <a href=""" + buttonUrl + @""" style=""display:inline-block;background-color:#15803d;color:#ffffff;text-decoration:none;padding:10px 22px;font-size:13px;font-weight:700;border-radius:8px;box-shadow:0 2px 6px rgba(21,128,61,0.25);"">" + System.Net.WebUtility.HtmlEncode(buttonText) + @"</a>
</div>");
            }

            sb.Append(@"</td></tr>
<!-- Footer -->
<tr><td style=""background-color:#f8fafc;padding:16px 20px;border-top:1px solid #e2e8f0;text-align:center;font-size:12px;color:#64748b;"">
  <p style=""margin:0 0 4px;"">Need assistance? Contact Support: <strong style=""color:#334155;"">support@agriai.com</strong></p>
  <p style=""margin:0;font-size:11px;color:#94a3b8;"">AgriAI Gujarat • Smart Farm & Market • © 2026</p>
</td></tr>
</table>
</td></tr></table>
</body></html>");

            return sb.ToString();
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
                if (string.IsNullOrEmpty(cleanPhone))
                {
                    return (false, string.Empty, "Invalid phone number.");
                }

                if (!_isEnabled || _provider.Equals("Disabled", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("📱 [Simulated WhatsApp Alert] To: {Phone}
{Body}", cleanPhone, messageBody);
                    return (true, "simulated_" + Guid.NewGuid().ToString("N")[..8], string.Empty);
                }

                return (true, "mock_" + Guid.NewGuid().ToString("N")[..8], string.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SendMessageAsync");
                return (false, string.Empty, ex.Message);
            }
        }

        // 1. Send Order Notification to Farmer
        public async Task<(bool Success, string MessageId, string ErrorMessage)> SendOrderNotificationToFarmerAsync(
            Order order, string farmerPhone, string farmerName, string buyerName, decimal farmerEarnings, string buyerPhone = "", string farmerEmail = "")
        {
            var firstItem = order.OrderItems.FirstOrDefault();
            var cropName = firstItem?.Product?.Name ?? "Farm Produce";
            int totalQty = order.OrderItems.Sum(oi => oi.Quantity);
            if (order.OrderItems.Count > 1)
            {
                cropName += $" (+{order.OrderItems.Count - 1} varieties)";
            }

            var rzpRef = order.Payment != null && !string.IsNullOrEmpty(order.Payment.RazorpayPaymentId) 
                ? order.Payment.RazorpayPaymentId 
                : (order.PaymentMethod == "Cash on Delivery" ? "COD (Pay on Pickup)" : "Escrow Protected");

            var deliveryAddr = string.IsNullOrWhiteSpace(order.ShippingAddress) ? "Direct Farm Pickup (Gujarat Field/Godown)" : order.ShippingAddress;
            var safeFarmerName = string.IsNullOrWhiteSpace(farmerName) ? "Farmer" : farmerName.Trim();
            var safeBuyerName = string.IsNullOrWhiteSpace(buyerName) ? "Verified Buyer" : buyerName.Trim();

            var sb = new StringBuilder();
            sb.AppendLine("🌾 *AGRIAI PRODUCE ORDER RECEIPT* 🌾");
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine($"Hello *{safeFarmerName}*,");
            sb.AppendLine("You have received a new purchase order for your farm produce!");
            sb.AppendLine();
            sb.AppendLine("📋 *ORDER DETAILS:*");
            sb.AppendLine($"• *Order ID:* #{order.Id:D5}");
            sb.AppendLine($"• *Produce:* *{cropName}* ({totalQty} Kg)");
            sb.AppendLine($"• *Total Amount:* *₹{order.TotalAmount:N2}*");
            sb.AppendLine($"• *Your Net Earnings:* *₹{farmerEarnings:N2}*");
            sb.AppendLine($"• *Fulfillment Mode:* {order.TransportType ?? "Direct Farm Pickup"}");
            sb.AppendLine();
            sb.AppendLine("💳 *ESCROW PAYMENT STATUS:*");
            if (order.PaymentMethod != "Cash on Delivery")
            {
                sb.AppendLine("✅ *100% Payment Secured (Razorpay Escrow)*");
                sb.AppendLine($"🔒 *₹{farmerEarnings:N2}* is safely locked in AgriAI Escrow Vault.");
                sb.AppendLine("Funds will be credited to your account upon verified delivery.");
                sb.AppendLine($"• *Ref ID:* {rzpRef}");
            }
            else
            {
                sb.AppendLine("💵 *Cash on Delivery (Pay on Pickup)*");
                sb.AppendLine($"Collect ₹{farmerEarnings:N2} in cash when the buyer arrives for pickup.");
            }
            sb.AppendLine();
            sb.AppendLine("📍 *BUYER DETAILS:*");
            sb.AppendLine($"• *Buyer Name:* {safeBuyerName}");
            if (!string.IsNullOrEmpty(buyerPhone))
            {
                sb.AppendLine($"• *Contact:* {buyerPhone}");
            }
            sb.AppendLine($"• *Location:* {deliveryAddr}");
            sb.AppendLine();
            sb.AppendLine("⏳ *Next Step:*");
            sb.AppendLine("Please prepare the crop packaging and mark 'Ready for Pickup' on your Farmer Dashboard.");
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine("🚜 *AgriAI - Direct Farm-to-Consumer Digital Marketplace* 🌾");

            var messageBody = sb.ToString();

            if (!string.IsNullOrWhiteSpace(farmerEmail))
            {
                var rows = new List<(string Label, string Value, bool IsHighlight)>
                {
                    ("Order Number", $"#{order.Id:D5}", false),
                    ("Produce Name", cropName, false),
                    ("Ordered Quantity", $"{totalQty} Kg", false),
                    ("Total Order Amount", $"₹{order.TotalAmount:N2}", false),
                    ("Your Net Earnings", $"₹{farmerEarnings:N2}", true),
                    ("Buyer Name", safeBuyerName, false),
                    ("Buyer Phone", !string.IsNullOrEmpty(buyerPhone) ? buyerPhone : "N/A", false),
                    ("Delivery Location", deliveryAddr, false),
                    ("Fulfillment Mode", order.TransportType ?? "Direct Farm Pickup", false)
                };

                var escrowBox = order.PaymentMethod != "Cash on Delivery"
                    ? $@"<div style=""background-color:#f0fdf4;border:1px solid #bbf7d0;border-radius:10px;padding:14px;margin-top:14px;font-size:12px;color:#14532d;"">
  <strong>🔒 100% Escrow Payment Secured:</strong> The buyer has paid in full online. Your payment is safely held in AgriAI Escrow and will be released to your bank account immediately upon verified delivery. (Ref: {rzpRef})
</div>"
                    : $@"<div style=""background-color:#fefce8;border:1px solid #fde047;border-radius:10px;padding:14px;margin-top:14px;font-size:12px;color:#713f12;"">
  <strong>💵 Cash on Delivery (Pay on Pickup):</strong> Please collect ₹{farmerEarnings:N2} in cash when the buyer arrives at the farm to collect the harvest.
</div>";

                var actionBox = @"<div style=""background-color:#f8fafc;border:1px solid #e2e8f0;border-radius:8px;padding:10px 14px;margin-top:10px;font-size:12px;color:#64748b;"">
  <strong>⏳ Next Step:</strong> Please package the harvested produce and click <em>'Ready for Pickup'</em> in your Farmer Dashboard.
</div>";

                var html = BuildHtmlEmail(
                    "📦 New Order Received",
                    "#dcfce7",
                    "#166534",
                    "#bbf7d0",
                    safeFarmerName,
                    "You have received a new purchase order for your farm produce. Order details are summarized below:",
                    null,
                    rows,
                    escrowBox + actionBox,
                    "View Order on Dashboard →",
                    "https://riai.runasp.net/Farmer/Dashboard"
                );

                await SendEmailAsync(farmerEmail, $"🌾 AgriAI - New Order Received #{order.Id:D5}", html, messageBody);
            }

            return await SendMessageAsync(farmerPhone, messageBody);
        }

        // 2. Order Confirmation to Buyer
        public async Task<(bool Success, string MessageId, string ErrorMessage)> SendOrderConfirmationToBuyerAsync(
            Order order, string buyerPhone, string buyerName, string farmerName, string farmerPhone = "", string pickupAddress = "", string buyerEmail = "")
        {
            var firstItem = order.OrderItems.FirstOrDefault();
            var cropName = firstItem?.Product?.Name ?? "Farm Produce";
            int totalQty = order.OrderItems.Sum(oi => oi.Quantity);
            if (order.OrderItems.Count > 1)
            {
                cropName += $" (+{order.OrderItems.Count - 1} varieties)";
            }

            var safeBuyerName = string.IsNullOrWhiteSpace(buyerName) ? "Customer" : buyerName.Trim();
            var safeFarmerName = string.IsNullOrWhiteSpace(farmerName) ? "Farmer" : farmerName.Trim();

            var sb = new StringBuilder();
            sb.AppendLine("🌾 *AGRIAI ORDER CONFIRMED* 🌾");
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine($"Hello *{safeBuyerName}*,");
            sb.AppendLine("Your payment has been received and your order is confirmed!");
            sb.AppendLine();
            sb.AppendLine("📋 *ORDER SUMMARY:*");
            sb.AppendLine($"• *Order ID:* #{order.Id:D5}");
            sb.AppendLine($"• *Produce:* *{cropName}* ({totalQty} Kg)");
            sb.AppendLine($"• *Farmer:* *{safeFarmerName}*");
            sb.AppendLine($"• *Total Amount Paid:* *₹{order.TotalAmount:N2}*");
            sb.AppendLine("• *Payment Status:* 100% Escrow Secured ✅");
            sb.AppendLine();
            sb.AppendLine("🌾 *Next Step:*");
            sb.AppendLine("The farmer is preparing your order. You will be notified as soon as it is ready.");
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine("🚜 *AgriAI - Direct Farm Marketplace* 🌾");

            var messageBody = sb.ToString();

            if (!string.IsNullOrWhiteSpace(buyerEmail))
            {
                var rows = new List<(string Label, string Value, bool IsHighlight)>
                {
                    ("Order Number", $"#{order.Id:D5}", false),
                    ("Produce Ordered", $"{cropName} ({totalQty} Kg)", false),
                    ("Farmer Name", safeFarmerName, false),
                    ("Crop Subtotal", $"₹{(order.TotalAmount - order.PlatformFee):N2}", false),
                    ("Platform Fee (2%)", $"₹{order.PlatformFee:N2}", false),
                    ("Total Amount Paid", $"₹{order.TotalAmount:N2}", true),
                    ("Payment Method", $"{order.PaymentMethod} (100% Escrow Secured)", false)
                };

                var note = @"<div style=""background-color:#eff6ff;border:1px solid #bfdbfe;border-radius:10px;padding:14px;margin-top:14px;font-size:12px;color:#1e40af;"">
  <strong>🛡️ AgriAI Escrow Protection Guarantee:</strong><br>
  Your payment is held safely in AgriAI Escrow Vault. Funds will only be released to the farmer after you inspect and verify the delivered produce.
</div>";

                var html = BuildHtmlEmail(
                    "✅ Order Confirmed",
                    "#dcfce7",
                    "#166534",
                    "#bbf7d0",
                    safeBuyerName,
                    "Thank you for your order! Your payment was received and the farmer has been notified to prepare your fresh produce:",
                    null,
                    rows,
                    note,
                    "View Order Status →",
                    "https://riai.runasp.net/Customer/Dashboard"
                );

                await SendEmailAsync(buyerEmail, $"🌾 AgriAI - Order Confirmed & Payment Receipt #{order.Id:D5}", html, messageBody);
            }

            return await SendMessageAsync(buyerPhone, messageBody);
        }

        // 3. Produce Ready for Pickup Notification
        public async Task<(bool Success, string MessageId, string ErrorMessage)> SendProduceReadyNotificationAsync(
            Order order, string buyerPhone, string buyerName, string farmerName, string farmerPhone, string pickupAddress, string deliveryOtp, string buyerEmail = "")
        {
            var firstItem = order.OrderItems.FirstOrDefault();
            var cropName = firstItem?.Product?.Name ?? "Farm Produce";
            int totalQty = order.OrderItems.Sum(oi => oi.Quantity);
            if (order.OrderItems.Count > 1)
            {
                cropName += $" (+{order.OrderItems.Count - 1} varieties)";
            }

            var safeBuyerName = string.IsNullOrWhiteSpace(buyerName) ? "Customer" : buyerName.Trim();
            var safeFarmerName = string.IsNullOrWhiteSpace(farmerName) ? "Farmer" : farmerName.Trim();
            var safeAddress = string.IsNullOrWhiteSpace(pickupAddress) ? "Direct Farm Pickup Godown" : pickupAddress;

            var sb = new StringBuilder();
            sb.AppendLine("🚜 *AGRIAI ORDER READY FOR PICKUP* 🚜");
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine($"Hello *{safeBuyerName}*,");
            sb.AppendLine($"Farmer *{safeFarmerName}* has prepared and packed your fresh harvest!");
            sb.AppendLine();
            sb.AppendLine("📋 *ORDER DETAILS:*");
            sb.AppendLine($"• *Order ID:* #{order.Id:D5}");
            sb.AppendLine($"• *Produce:* *{cropName}* ({totalQty} Kg)");
            sb.AppendLine($"• *Farmer Phone:* {farmerPhone}");
            sb.AppendLine($"• *Farm Location:* {safeAddress}");
            sb.AppendLine();
            sb.AppendLine("🔐 *YOUR DELIVERY VERIFICATION OTP:*");
            sb.AppendLine($"👉 *{deliveryOtp}* 👈");
            sb.AppendLine("⚠️ *Important:* Give this 4-digit OTP to the farmer only after inspecting your produce at pickup.");
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine("🚜 *AgriAI - Pure Farm-Fresh Direct Marketplace* 🌾");

            var messageBody = sb.ToString();

            if (!string.IsNullOrWhiteSpace(buyerEmail))
            {
                var callout = $@"<div style=""background-color:#f0fdf4;border:2px dashed #86efac;border-radius:12px;padding:18px;text-align:center;margin:16px 0;"">
  <div style=""font-size:11px;text-transform:uppercase;letter-spacing:0.5px;color:#166534;font-weight:700;"">Your Delivery Verification OTP</div>
  <div style=""font-size:32px;font-weight:800;color:#15803d;letter-spacing:6px;font-family:monospace;margin:6px 0;"">{deliveryOtp}</div>
  <div style=""font-size:12px;color:#166534;font-weight:600;"">⚠️ Provide this 4-digit OTP to the farmer only after receiving and inspecting the crop.</div>
</div>";

                var rows = new List<(string Label, string Value, bool IsHighlight)>
                {
                    ("Order Number", $"#{order.Id:D5}", false),
                    ("Harvested Produce", $"{cropName} ({totalQty} Kg)", false),
                    ("Farmer Name", safeFarmerName, false),
                    ("Farmer Phone", farmerPhone, false),
                    ("Pickup Farm Address", safeAddress, false)
                };

                var note = @"<div style=""background-color:#eff6ff;border:1px solid #bfdbfe;border-radius:8px;padding:12px 14px;margin-top:14px;font-size:12px;color:#1e40af;"">
  <strong>📍 Pickup Guidance:</strong> You may visit the farm during daytime hours. Upon inspecting the produce quality, provide the 4-digit OTP to complete delivery.
</div>";

                var html = BuildHtmlEmail(
                    "🚜 Ready for Pickup",
                    "#fef3c7",
                    "#92400e",
                    "#fde68a",
                    safeBuyerName,
                    $"Great news! Farmer <strong>{safeFarmerName}</strong> has prepared your harvest order. It is now ready for physical pickup at the farm:",
                    callout,
                    rows,
                    note,
                    "View Farm Pickup Address →",
                    "https://riai.runasp.net/Customer/Dashboard"
                );

                await SendEmailAsync(buyerEmail, $"🌾 AgriAI - Your Order is Ready for Farm Pickup #{order.Id:D5}", html, messageBody);
            }

            return await SendMessageAsync(buyerPhone, messageBody);
        }

        // 4. Produce Dispatched via Bilty Logistics
        public async Task<(bool Success, string MessageId, string ErrorMessage)> SendProduceDispatchedNotificationAsync(
            Order order, string buyerPhone, string buyerName, string farmerName, string cargoServiceName, string biltyNumber, string deliveryOtp, string farmerPhone = "", string buyerEmail = "")
        {
            var firstItem = order.OrderItems.FirstOrDefault();
            var cropName = firstItem?.Product?.Name ?? "Farm Produce";
            int totalQty = order.OrderItems.Sum(oi => oi.Quantity);
            if (order.OrderItems.Count > 1)
            {
                cropName += $" (+{order.OrderItems.Count - 1} varieties)";
            }

            var safeBuyerName = string.IsNullOrWhiteSpace(buyerName) ? "Customer" : buyerName.Trim();
            var safeFarmerName = string.IsNullOrWhiteSpace(farmerName) ? "Farmer" : farmerName.Trim();
            var safeCargo = string.IsNullOrWhiteSpace(cargoServiceName) ? "GSRTC Bus Parcel / Cargo" : cargoServiceName;
            var safeBilty = string.IsNullOrWhiteSpace(biltyNumber) ? "LR-Direct" : biltyNumber;

            var sb = new StringBuilder();
            sb.AppendLine("🚚 *AGRIAI PRODUCE DISPATCHED* 🚚");
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine($"Hello *{safeBuyerName}*,");
            sb.AppendLine($"Farmer *{safeFarmerName}* has dispatched your crop order via cargo transport!");
            sb.AppendLine();
            sb.AppendLine("📋 *DISPATCH DETAILS:*");
            sb.AppendLine($"• *Order ID:* #{order.Id:D5}");
            sb.AppendLine($"• *Produce:* *{cropName}* ({totalQty} Kg)");
            sb.AppendLine($"• *Transport Agency:* *{safeCargo}*");
            sb.AppendLine($"• *Bilty / LR No:* *{safeBilty}*");
            if (!string.IsNullOrEmpty(farmerPhone))
            {
                sb.AppendLine($"• *Farmer Phone:* {farmerPhone}");
            }
            sb.AppendLine($"• *Delivery Destination:* {order.ShippingAddress}");
            sb.AppendLine();
            sb.AppendLine("🔐 *YOUR DELIVERY VERIFICATION OTP:*");
            sb.AppendLine($"👉 *{deliveryOtp}* 👈");
            sb.AppendLine("⚠️ Hand over this 4-digit OTP to the delivery agent only upon receiving your consignment.");
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine("🚜 *AgriAI - Direct Farm Logistics* 🌾");

            var messageBody = sb.ToString();

            if (!string.IsNullOrWhiteSpace(buyerEmail))
            {
                var callout = $@"<div style=""background-color:#f0fdf4;border:2px dashed #86efac;border-radius:12px;padding:18px;text-align:center;margin:16px 0;"">
  <div style=""font-size:11px;text-transform:uppercase;letter-spacing:0.5px;color:#166534;font-weight:700;"">Your Delivery Verification OTP</div>
  <div style=""font-size:32px;font-weight:800;color:#15803d;letter-spacing:6px;font-family:monospace;margin:6px 0;"">{deliveryOtp}</div>
  <div style=""font-size:12px;color:#166534;font-weight:600;"">⚠️ Hand over this 4-digit OTP to the transport agent upon receiving the cargo.</div>
</div>";

                var rows = new List<(string Label, string Value, bool IsHighlight)>
                {
                    ("Order Number", $"#{order.Id:D5}", false),
                    ("Produce & Quantity", $"{cropName} ({totalQty} Kg)", false),
                    ("Transport Carrier", safeCargo, false),
                    ("Bilty / LR Tracking No.", safeBilty, true),
                    ("Farmer Contact", !string.IsNullOrEmpty(farmerPhone) ? farmerPhone : "N/A", false),
                    ("Delivery Destination", order.ShippingAddress ?? "Gujarat Address", false)
                };

                var note = @"<div style=""background-color:#eff6ff;border:1px solid #bfdbfe;border-radius:10px;padding:14px;margin-top:14px;font-size:12px;color:#1e40af;"">
  <strong>📦 Consignment Tracking Notice:</strong> Keep your Bilty/LR number ready to receive the goods from the transport office or bus parcel depot.
</div>";

                var html = BuildHtmlEmail(
                    "🚚 Consignment Dispatched",
                    "#dbeafe",
                    "#1e40af",
                    "#bfdbfe",
                    safeBuyerName,
                    "Your crop order has been officially handed over to the transport agency and is in transit to your city:",
                    callout,
                    rows,
                    note,
                    "Track Consignment →",
                    "https://riai.runasp.net/Customer/Dashboard"
                );

                await SendEmailAsync(buyerEmail, $"🚚 AgriAI - Your Order Has Been Dispatched #{order.Id:D5}", html, messageBody);
            }

            return await SendMessageAsync(buyerPhone, messageBody);
        }

        // 5. Account Registration OTP
        public async Task<(bool Success, string MessageId, string ErrorMessage)> SendRegistrationOtpAsync(string phoneNumber, string otpCode, string role, string email = "")
        {
            var roleName = string.Equals(role, "Farmer", StringComparison.OrdinalIgnoreCase) 
                ? "Farmer" 
                : "Customer";

            var sb = new StringBuilder();
            sb.AppendLine("🌾 *AGRIAI ACCOUNT SECURITY OTP* 🌾");
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine($"Hello *{roleName}*,");
            sb.AppendLine("Welcome to AgriAI! Here is your official registration verification code:");
            sb.AppendLine();
            sb.AppendLine("🔐 *YOUR OTP CODE:*");
            sb.AppendLine($"👉 *{otpCode}* 👈");
            sb.AppendLine();
            sb.AppendLine("⏱️ *Valid for 5 minutes only.*");
            sb.AppendLine();
            sb.AppendLine("⚠️ *Security Warning:*");
            sb.AppendLine("Do NOT share this code with anyone. AgriAI representatives will never ask for your verification code or password.");
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine("🚜 *AgriAI - Direct Farm-to-Consumer Portal* 🌾");

            var messageBody = sb.ToString();

            if (!string.IsNullOrWhiteSpace(email))
            {
                var callout = $@"<div style=""background-color:#fffbeb;border:2px dashed #fcd34d;border-radius:12px;padding:18px;text-align:center;margin:16px 0;"">
  <div style=""font-size:11px;text-transform:uppercase;letter-spacing:0.5px;color:#92400e;font-weight:700;"">Your 6-Digit Verification Code</div>
  <div style=""font-size:32px;font-weight:800;color:#78350f;letter-spacing:6px;font-family:monospace;margin:6px 0;"">{otpCode}</div>
  <div style=""font-size:12px;color:#b45309;font-weight:600;"">⏱️ This code is valid for 5 minutes only.</div>
</div>";

                var note = @"<div style=""background-color:#fef2f2;border-left:4px solid #ef4444;border-radius:6px;padding:10px 14px;margin-top:14px;font-size:12px;color:#b91c1c;"">
  <strong>⚠️ Security Notice:</strong> Do NOT share this OTP with anyone. AgriAI representatives will never ask for your verification code or password.
</div>";

                var html = BuildHtmlEmail(
                    "🔐 Security Verification",
                    "#fef3c7",
                    "#92400e",
                    "#fde68a",
                    roleName,
                    "Welcome to AgriAI Gujarat! Please use the official 6-digit one-time password (OTP) below to verify your account registration:",
                    callout,
                    new List<(string, string, bool)>(),
                    note
                );

                await SendEmailAsync(email, "🌾 AgriAI - Account Verification Code (OTP)", html, messageBody);
            }

            return (true, "email_otp_" + Guid.NewGuid().ToString("N")[..8], string.Empty);
        }

        // 6. Farmer KYC Approved Notification by Admin
        public async Task<(bool Success, string MessageId, string ErrorMessage)> SendFarmerKycApprovedNotificationAsync(
            string farmerPhone, string farmerName, string farmerEmail = "")
        {
            var safeFarmerName = string.IsNullOrWhiteSpace(farmerName) ? "Farmer" : farmerName.Trim();

            var sb = new StringBuilder();
            sb.AppendLine("🌾 *AGRIAI - FARMER KYC APPROVED* 🌾");
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine($"Hello *{safeFarmerName}*,");
            sb.AppendLine();
            sb.AppendLine("Congratulations! Your farmer KYC documentation has been officially verified and approved by the AgriAI Admin team!");
            sb.AppendLine();
            sb.AppendLine("📋 *ACCOUNT STATUS:*");
            sb.AppendLine($"• *Farmer Name:* {safeFarmerName}");
            sb.AppendLine("• *Status:* 🟢 100% KYC Verified & Active");
            sb.AppendLine("• *Selling Access:* Approved ✅ (0% Broker Commission)");
            sb.AppendLine();
            sb.AppendLine("🚜 *START SELLING:*");
            sb.AppendLine("You can now log in to your Farmer Dashboard, list your fresh farm crops, and start receiving direct orders with 100% Escrow security!");
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine("🚜 *AgriAI - Empowering Gujarat Farmers* 🌾");

            var messageBody = sb.ToString();

            if (!string.IsNullOrWhiteSpace(farmerEmail))
            {
                var rows = new List<(string Label, string Value, bool IsHighlight)>
                {
                    ("Farmer Name", safeFarmerName, false),
                    ("Account Status", "🟢 100% KYC Verified (Active)", true),
                    ("Marketplace Access", "Approved ✅ (0% Brokerage)", true),
                    ("Escrow Payment Guarantee", "Active (100% Secured Payouts)", false)
                };

                var note = @"<div style=""background-color:#f0fdf4;border:1px solid #bbf7d0;border-radius:10px;padding:14px;margin-top:14px;font-size:12px;color:#166534;"">
  <strong>🚜 Next Step:</strong><br>
  You can now log in to your Farmer Dashboard, list your freshly harvested produce, and start receiving direct orders with zero broker cuts.
</div>";

                var html = BuildHtmlEmail(
                    "🟢 KYC Approved",
                    "#dcfce7",
                    "#166534",
                    "#bbf7d0",
                    safeFarmerName,
                    "Congratulations! Your identity and 7/12 land records have been successfully audited and approved. Your digital storefront is now active:",
                    null,
                    rows,
                    note,
                    "Open Farmer Dashboard →",
                    "https://riai.runasp.net/Farmer/Dashboard"
                );

                await SendEmailAsync(farmerEmail, "🌾 AgriAI - Congratulations! Your Farmer Account is KYC Approved", html, messageBody);
            }

            return await SendMessageAsync(farmerPhone, messageBody);
        }

        // 7. Customer Refund Notification
        public async Task<(bool Success, string MessageId, string ErrorMessage)> SendRefundNotificationToBuyerAsync(
            Order order, string buyerPhone, string buyerName, decimal refundAmount, string refundId, string cancelReason, string buyerEmail = "")
        {
            var firstItem = order.OrderItems.FirstOrDefault();
            var cropName = firstItem?.Product?.Name ?? "Farm Produce";
            var safeBuyerName = string.IsNullOrWhiteSpace(buyerName) ? "Customer" : buyerName.Trim();
            var safeRefundId = string.IsNullOrWhiteSpace(refundId) ? ("RF-" + Guid.NewGuid().ToString("N")[..8].ToUpper()) : refundId;
            var safeReason = string.IsNullOrWhiteSpace(cancelReason) ? "Order Cancelled / Stock Unavailable" : cancelReason;

            var sb = new StringBuilder();
            sb.AppendLine("💰 *AGRIAI REFUND SUCCESSFUL* 💰");
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine($"Hello *{safeBuyerName}*,");
            sb.AppendLine($"Your refund for Order #{order.Id:D5} has been processed from AgriAI Escrow Vault!");
            sb.AppendLine();
            sb.AppendLine("📋 *REFUND DETAILS:*");
            sb.AppendLine($"• *Order ID:* #{order.Id:D5}");
            sb.AppendLine($"• *Produce:* *{cropName}*");
            sb.AppendLine($"• *Refund Amount:* *₹{refundAmount:N2}* (100% Full Refund)");
            sb.AppendLine($"• *Refund Reference:* {safeRefundId}");
            sb.AppendLine($"• *Cancellation Reason:* {safeReason}");
            sb.AppendLine("• *Status:* 100% Refund Released ✅");
            sb.AppendLine();
            sb.AppendLine("💳 *Bank Credit Note:*");
            sb.AppendLine("The refund will reflect in your original bank account/UPI in 1-3 business days.");
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine("🚜 *AgriAI - Direct Gujarat Farm-to-Consumer Portal* 🌾");

            var plainText = sb.ToString();

            if (!string.IsNullOrWhiteSpace(buyerEmail))
            {
                var callout = $@"<div style=""background-color:#f0fdf4;border:2px solid #86efac;border-radius:10px;padding:16px;text-align:center;margin:16px 0;"">
  <div style=""font-size:11px;text-transform:uppercase;letter-spacing:0.5px;color:#166534;font-weight:700;"">Total Refunded Amount</div>
  <div style=""font-size:28px;font-weight:800;color:#15803d;font-family:monospace;margin:4px 0;"">₹{refundAmount:N2}</div>
  <div style=""font-size:12px;color:#15803d;font-weight:600;"">🟢 100% Full Refund Released</div>
</div>";

                var rows = new List<(string Label, string Value, bool IsHighlight)>
                {
                    ("Order Number", $"#{order.Id:D5}", false),
                    ("Produce Name", cropName, false),
                    ("Refund Reference ID", safeRefundId, false),
                    ("Refund Method", "Original Payment Account (Bank / UPI)", false),
                    ("Cancellation Reason", safeReason, false),
                    ("Escrow Status", "Successfully Refunded ✅", true)
                };

                var note = @"<div style=""background-color:#f8fafc;border:1px solid #e2e8f0;border-radius:8px;padding:12px 14px;margin-top:14px;font-size:12px;color:#64748b;"">
  <strong>ℹ️ Bank Settlement Notice:</strong> Depending on your bank's clearance policy, the funds will reflect in your account within 1 to 3 business days.
</div>";

                var html = BuildHtmlEmail(
                    "💰 Refund Processed",
                    "#dcfce7",
                    "#166534",
                    "#bbf7d0",
                    safeBuyerName,
                    $"Your refund for Order <strong>#{order.Id:D5}</strong> has been successfully processed from AgriAI Escrow Vault and transferred back to your original payment method:",
                    callout,
                    rows,
                    note,
                    "View Order History →",
                    "https://riai.runasp.net/Customer/Dashboard"
                );

                await SendEmailAsync(buyerEmail, $"💰 AgriAI - Refund Successfully Processed #{order.Id:D5}", html, plainText);
            }

            return await SendMessageAsync(buyerPhone, plainText);
        }
    }
}
