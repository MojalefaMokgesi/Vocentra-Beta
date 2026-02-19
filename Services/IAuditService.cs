using System.Threading.Tasks;

namespace Vocentra.Services
{
    public interface IAuditService
    {
        Task LogAsync(string actorUserId, string actorRole, string action, string entityType, string entityId, string? ip = null, string? ua = null, string? metadataJson = null);
    }
}
