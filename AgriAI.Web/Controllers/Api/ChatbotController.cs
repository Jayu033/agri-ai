using Microsoft.AspNetCore.Mvc;

namespace AgriAI.Web.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    public class ChatbotController : ControllerBase
    {
        [HttpPost("ask")]
        public async Task<IActionResult> Ask([FromBody] ChatRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest("Message is required.");
            }

            // TODO: Integrate actual Gemini / OpenAI SDK here.
            // For now, this is a simulated AI response supporting EN, HI, GU.
            
            string responseMessage = "Hello! I am the AgriAI assistant. How can I help you with farming or your orders today?";
            
            if (request.Message.Contains("ખેતી", StringComparison.OrdinalIgnoreCase) || request.Message.Contains("ગુજરાતી", StringComparison.OrdinalIgnoreCase))
            {
                responseMessage = "નમસ્તે! હું AgriAI આસિસ્ટન્ટ છું. હું તમને ખેતી, પાક ના રોગ અને ઓર્ડર ટ્રેકિંગ માં મદદ કરી શકું છું. જણાવો હું તમારી શું મદદ કરું?";
            }
            else if (request.Message.Contains("खेती", StringComparison.OrdinalIgnoreCase) || request.Message.Contains("हिंदी", StringComparison.OrdinalIgnoreCase))
            {
                responseMessage = "नमस्ते! मैं AgriAI सहायक हूँ। मैं आपको खेती और ऑर्डर ट्रैक करने में मदद कर सकता हूँ। बताइये मैं आपकी क्या मदद करूँ?";
            }

            // Simulate AI delay
            await Task.Delay(1000);

            return Ok(new { Response = responseMessage });
        }
    }

    public class ChatRequest
    {
        public string Message { get; set; } = string.Empty;
        public string Language { get; set; } = "en"; // en, gu, hi
    }
}
