using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Vocentra.Data;
using Vocentra.Models;

namespace Vocentra.Services
{
    public class AuditService : IAuditService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<AuditService> _log;
        private readonly Microsoft.AspNetCore.Http.IHttpContextAccessor _hca;

        public AuditService(AppDbContext db, ILogger<AuditService> log, Microsoft.AspNetCore.Http.IHttpContextAccessor hca)
        {
            _db = db;
            _log = log;
            _hca = hca;
        }

        public async Task LogAsync(string actorUserId, string actorRole, string action, string entityType, string entityId, string? ip = null, string? ua = null, string? metadataJson = null)
        {
            try
            {
                // If ip or ua not provided, attempt to read from HttpContext
                if (string.IsNullOrEmpty(ip) || string.IsNullOrEmpty(ua))
                {
                    var ctx = _hca.HttpContext;
                    if (ctx != null)
                    {
                        if (string.IsNullOrEmpty(ip)) ip = ctx.Connection.RemoteIpAddress?.ToString();
                        if (string.IsNullOrEmpty(ua)) ua = ctx.Request.Headers["User-Agent"].ToString();
                    }
                }

                var a = new AuditLog
                {
                    ActorUserId = actorUserId,
                    ActorRole = actorRole,
                    Action = action,
                    EntityType = entityType,
                    EntityId = entityId,
                    IpAddress = ip,
                    UserAgent = ua,
                    MetadataJson = metadataJson
                };
                _db.AuditLogs.Add(a);
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to write audit log");
            }
        }
    }
}
