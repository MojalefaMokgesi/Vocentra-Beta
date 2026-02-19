using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vocentra.Data;

namespace Vocentra.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,FinanceAdmin")]
    public class DiagnosticsController : Controller
    {
        private readonly AppDbContext _db;

        public DiagnosticsController(AppDbContext db) => _db = db;

        [HttpGet]
        public IActionResult Index()
        {
            var info = new
            {
                Environment = HttpContext.RequestServices.GetService(typeof(Microsoft.Extensions.Hosting.IHostEnvironment)) is Microsoft.Extensions.Hosting.IHostEnvironment env ? env.EnvironmentName : "unknown",
                CanConnectDatabase = _db.Database.CanConnect(),
                Time = System.DateTime.UtcNow
            };
            return Json(info);
        }
    }
}
