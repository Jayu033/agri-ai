using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AgriAI.Web.Services
{
    public class RazorpayPaymentService : IRazorpayPaymentService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<RazorpayPaymentService> _logger;
        private readonly string _keyId;
        private readonly string _keySecret;

        public RazorpayPaymentService(HttpClient httpClient, IConfiguration configuration, ILogger<RazorpayPaymentService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;

            _keyId = _configuration["Razorpay:KeyId"] ?? "";
            _keySecret = _configuration["Razorpay:KeySecret"] ?? "";
        }

        public string GetKeyId() => _keyId;

        public async Task<(bool Success, string RazorpayOrderId, string ErrorMessage)> CreateOrderAsync(decimal amountInInr, string receiptId, string? notes = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_keyId) || string.IsNullOrWhiteSpace(_keySecret))
                {
                    return (false, string.Empty, "Razorpay API keys are missing in appsettings.json.");
                }

                // Amount in paise (1 INR = 100 paise)
                long amountInPaise = (long)Math.Round(amountInInr * 100m, MidpointRounding.AwayFromZero);
                if (amountInPaise <= 0) amountInPaise = 100; // minimum ₹1

                var requestPayload = new
                {
                    amount = amountInPaise,
                    currency = "INR",
                    receipt = string.IsNullOrEmpty(receiptId) ? $"rcpt_{DateTime.UtcNow.Ticks % 1000000}" : (receiptId.Length > 40 ? receiptId[..40] : receiptId),
                    payment_capture = 1
                };

                var requestJson = JsonSerializer.Serialize(requestPayload);
                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.razorpay.com/v1/orders")
                {
                    Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
                };

                var authString = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_keyId}:{_keySecret}"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authString);

                var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Razorpay Create Order API Failed: Status {Status}, Response: {Body}", response.StatusCode, responseContent);
                    return (false, string.Empty, $"Razorpay API error ({response.StatusCode}): {responseContent}");
                }

                var jsonNode = JsonNode.Parse(responseContent);
                var orderId = jsonNode?["id"]?.ToString() ?? string.Empty;

                if (string.IsNullOrEmpty(orderId))
                {
                    return (false, string.Empty, "Razorpay returned an empty order identifier.");
                }

                return (true, orderId, string.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in Razorpay CreateOrderAsync.");
                return (false, string.Empty, ex.Message);
            }
        }

        public bool VerifyPaymentSignature(string razorpayOrderId, string razorpayPaymentId, string razorpaySignature)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_keySecret) || 
                    string.IsNullOrWhiteSpace(razorpayOrderId) || 
                    string.IsNullOrWhiteSpace(razorpayPaymentId) || 
                    string.IsNullOrWhiteSpace(razorpaySignature))
                {
                    return false;
                }

                string payload = $"{razorpayOrderId}|{razorpayPaymentId}";
                using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_keySecret));
                byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
                string generatedSignature = Convert.ToHexString(hash).ToLower();

                return string.Equals(generatedSignature, razorpaySignature.Trim(), StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in Razorpay VerifyPaymentSignature.");
                return false;
            }
        }

        public async Task<(bool Success, string RefundId, string ErrorMessage)> RefundPaymentAsync(string razorpayPaymentId, decimal? amountInInr = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_keyId) || string.IsNullOrWhiteSpace(_keySecret) || string.IsNullOrWhiteSpace(razorpayPaymentId))
                {
                    return (false, string.Empty, "Missing parameters for refund.");
                }

                object payload;
                if (amountInInr.HasValue && amountInInr.Value > 0)
                {
                    long amountInPaise = (long)Math.Round(amountInInr.Value * 100m, MidpointRounding.AwayFromZero);
                    payload = new { amount = amountInPaise };
                }
                else
                {
                    payload = new { };
                }

                var request = new HttpRequestMessage(HttpMethod.Post, $"https://api.razorpay.com/v1/payments/{razorpayPaymentId}/refund")
                {
                    Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
                };

                var authString = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_keyId}:{_keySecret}"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authString);

                var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Razorpay Refund API Response: Status {Status}, Response: {Body}", response.StatusCode, responseContent);
                    return (false, string.Empty, responseContent);
                }

                var jsonNode = JsonNode.Parse(responseContent);
                var refundId = jsonNode?["id"]?.ToString() ?? "refund_ok";
                return (true, refundId, string.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in Razorpay RefundPaymentAsync.");
                return (false, string.Empty, ex.Message);
            }
        }
    }
}
