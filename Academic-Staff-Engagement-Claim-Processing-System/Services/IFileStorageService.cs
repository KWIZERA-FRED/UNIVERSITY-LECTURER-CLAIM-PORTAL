using System.Threading.Tasks;

namespace Academic_Staff_Engagement_Claim_Processing_System.Services
{
    public interface IFileStorageService
    {
        Task<string> SaveAsync(
            string folder,
            string preferredFileName,
            byte[] content,
            string contentType);

        Task<byte[]> ReadAsync(string storageKey);

        Task<bool> ExistsAsync(string storageKey);

        Task DeleteAsync(string storageKey);
    }
}