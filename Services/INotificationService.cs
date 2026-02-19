using System.Threading.Tasks;
using Vocentra.Models;

namespace Vocentra.Services
{
    public interface INotificationService
    {
        Task CreateNotificationAsync(string userId, string title, string body, string? link = null);
    }
}
