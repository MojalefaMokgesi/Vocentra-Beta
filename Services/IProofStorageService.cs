using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Vocentra.Models;

namespace Vocentra.Services
{
    public interface IProofStorageService
    {
        Task<ProofDocument> SaveAsync(IFormFile file);
        Task<Stream> GetStreamAsync(ProofDocument doc);
        // Create a time-limited download token that can be used to stream the document without DB lookups
        Task<string> CreateDownloadTokenAsync(ProofDocument doc, TimeSpan validFor);
        // Validate token and return stream
        Task<Stream> GetStreamFromDownloadTokenAsync(string token);
    }
}
