using ApartmentManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApartmentManagementSystem.Controllers
{
    [ApiController]
    [Route("api/razorpay")]
    [AllowAnonymous]
    public class RazorpayWebhookController : ControllerBase
    {
        private readonly PaymentGatewayService _paymentGateway;
        private readonly ILogger<RazorpayWebhookController> _logger;

        public RazorpayWebhookController(
            PaymentGatewayService paymentGateway,
            ILogger<RazorpayWebhookController> logger)
        {
            _paymentGateway = paymentGateway;
            _logger = logger;
        }

        [HttpGet("webhook")]
        public IActionResult WebhookInfo()
        {
            return Content(
                "Razorpay webhook endpoint is working.\n\n" +
                "This URL accepts POST requests only (from Razorpay servers).\n" +
                "Do not open it in a browser to test.\n\n" +
                "In Razorpay Dashboard → Webhooks:\n" +
                "1. Add this URL\n" +
                "2. Select event: payment.captured\n" +
                "3. Copy the webhook secret into Integrations → Webhook secret → Save",
                "text/plain");
        }

        [HttpPost("webhook")]
        public async Task<IActionResult> Webhook()
        {
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();
            var signature = Request.Headers["X-Razorpay-Signature"].FirstOrDefault();

            var ok = await _paymentGateway.ProcessWebhookAsync(body, signature);
            if (!ok)
            {
                _logger.LogWarning(
                    "Razorpay webhook rejected (bad signature, unknown order, or invalid payload).");
                return BadRequest();
            }

            return Ok();
        }
    }
}
