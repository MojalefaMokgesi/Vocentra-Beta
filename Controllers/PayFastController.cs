using Microsoft.AspNetCore.Mvc;
using Vocentra.Services;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;

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

        // GET: /payments/payfast/start?refId=ABC123&amount=10.00&item=Test%20Product
        [HttpGet("start")]
        public IActionResult Start([FromQuery] string refId, [FromQuery] decimal amount, [FromQuery] string item)
        {
            if (string.IsNullOrWhiteSpace(refId)) return BadRequest("Missing refId");
            if (amount <= 0) return BadRequest("Amount must be > 0");
            if (string.IsNullOrWhiteSpace(item)) item = "Vocentra Payment";

            var fields = _payFast.BuildRequestFields(refId, amount, item);

            ViewBag.ProcessUrl = _payFast.ProcessUrl;
            return View("RedirectToPayFast", fields);
        }

        // Return URL (GET)
        [HttpGet("return")]
        public IActionResult Return() => Content("Payment complete.");

        // Cancel URL (GET)
        [HttpGet("cancel")]
        public IActionResult Cancel() => Content("Payment cancelled.");

        // Notify URL (POST) - ITN
        [HttpPost("notify")]
        public async Task<IActionResult> Notify(CancellationToken ct)
        {
            var itnData = new Dictionary<string, string>();
            foreach (var key in Request.Form.Keys)
                itnData[key] = Request.Form[key].ToString();

            // 1) Signature check
            if (!_payFast.VerifySignature(itnData))
                return BadRequest("Invalid signature");

            // 2) Server validation check (PayFast query/validate)
            var valid = await _payFast.ValidateItnAsync(itnData, ct);
            if (!valid)
                return BadRequest("ITN not valid");

            // TODO: Mark payment as COMPLETE/FAILED in your DB based on itnData["payment_status"]
            return Ok();
        }
    }
}
