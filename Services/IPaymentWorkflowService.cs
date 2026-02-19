using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Vocentra.Models;

namespace Vocentra.Services
{
    public interface IPaymentWorkflowService
    {
        Task<PaymentRequest> CreatePaymentRequestAsync(Job job);
        Task<PaymentSubmission> SubmitProofAsync(int requestId, IFormFile file, string? notes, string? userId, decimal? amountClaimed = null);
        Task ApproveAsync(int requestId, string adminUserId);
        Task DeclineAsync(int requestId, string adminUserId, string reason);
        Task RequestMoreInfoAsync(int requestId, string adminUserId, string message);
    }
}
