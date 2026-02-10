using Microsoft.AspNetCore.Mvc;
using Vocentra.Services;

namespace Vocentra.Controllers
{
    [Route("payments/payfast")]
    public class PayFastController : Controller
    {
        private readonly PayFastService _payFast;

        public PayFastController(PayFastService payFast)
        {
            _payFast = payFast;
        }

        // Example endpoint you call to start payment
        // GET /payments/payfast/start?ref=INV123&amount=99.99&item=Vocentra%20Listing
        [HttpGet("start")]
        public IActionResult Start([FromQuery] string refId, [FromQuery] decimal amount, [FromQuery] string item)
        {
            if (string.IsNullOrWhiteSpace(refId)) return BadRequest("Missing refId");
            if (amount <= 0) return BadRequest("Amount must be > 0");
            if (string.IsNullOrWhiteSpace(item)) item = "Vocentra Payment";

            var fields = _payFast.BuildRequestFields(refId, amount, item);

            // Render a view that POSTS to PayFast with EXACT fields (no antiforgery, no extra fields)
            ViewBag.ProcessUrl = _payFast.ProcessUrl;
            return View("PayFastRedirect", fields);
        }

        // Return / Cancel pages
        [HttpGet("return")]
        public IActionResult Return() => Content("Payment complete (return).");

        [HttpGet("cancel")]
        public IActionResult Cancel() => Content("Payment cancelled.");

        // ITN (notify) endpoint (POST)
        [HttpPost("notify")]
        public IActionResult Notify()
        {
            // You will implement ITN later; for now just return 200 OK so PayFast doesn't retry forever.
            return Ok();
        }
    }
}
