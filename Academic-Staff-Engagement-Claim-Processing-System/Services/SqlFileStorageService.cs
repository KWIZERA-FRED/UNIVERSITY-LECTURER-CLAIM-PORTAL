using System;
using System.Security.Cryptography;
using System.Threading.Tasks;

using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Academic_Staff_Engagement_Claim_Processing_System.Services
{
    public class SqlFileStorageService : IFileStorageService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SqlFileStorageService> _logger;

        public SqlFileStorageService(
            ApplicationDbContext context,
            ILogger<SqlFileStorageService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<string> SaveAsync(
            string folder,
            string preferredFileName,
            byte[] content,
            string contentType)
        {
            if (content == null || content.Length == 0)
            {
                throw new ArgumentException(
                    "Cannot save empty content.",
                    nameof(content));
            }

            if (string.IsNullOrWhiteSpace(preferredFileName))
            {
                throw new ArgumentException(
                    "FileName is required.",
                    nameof(preferredFileName));
            }

            var safeFolder =
                string.IsNullOrWhiteSpace(folder)
                    ? "general"
                    : NormalizeFolder(folder);

            var safeFileName = Path.GetFileName(preferredFileName);

            if (string.IsNullOrWhiteSpace(safeFileName))
            {
                safeFileName = "file";
            }

            if (safeFileName.Length > 255)
            {
                safeFileName = safeFileName.Substring(0, 255);
            }

            var hash = ComputeSha256Hex(content);

            var storedFile = new StoredFile
            {
                Id = Guid.NewGuid(),
                OriginalFileName = safeFileName,
                ContentType = string.IsNullOrWhiteSpace(contentType)
                    ? "application/octet-stream"
                    : contentType,
                SizeBytes = content.LongLength,
                Sha256Hash = hash,
                Folder = safeFolder,
                Content = content,
                CreatedAtUtc = DateTime.UtcNow
            };

            _context.StoredFiles.Add(storedFile);

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Stored file {FileId} ({FileName}, {SizeBytes} bytes) in folder '{Folder}'.",
                storedFile.Id,
                safeFileName,
                content.Length,
                safeFolder);

            return storedFile.Id.ToString("D");
        }

        public async Task<byte[]> ReadAsync(string storageKey)
        {
            var id = ParseKey(storageKey);

            var storedFile = await _context.StoredFiles
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == id);

            if (storedFile == null)
            {
                throw new FileNotFoundException(
                    $"Storage key '{storageKey}' does not exist.");
            }

            return storedFile.Content;
        }

        public async Task<bool> ExistsAsync(string storageKey)
        {
            if (!TryParseKey(storageKey, out var id))
            {
                return false;
            }

            return await _context.StoredFiles
                .AsNoTracking()
                .AnyAsync(f => f.Id == id);
        }

        public async Task DeleteAsync(string storageKey)
        {
            if (!TryParseKey(storageKey, out var id))
            {
                return;
            }

            var storedFile = await _context.StoredFiles
                .FirstOrDefaultAsync(f => f.Id == id);

            if (storedFile == null)
            {
                return;
            }

            _context.StoredFiles.Remove(storedFile);

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Deleted stored file {FileId}.",
                id);
        }

        private static Guid ParseKey(string storageKey)
        {
            if (!TryParseKey(storageKey, out var id))
            {
                throw new ArgumentException(
                    "Invalid storage key.",
                    nameof(storageKey));
            }

            return id;
        }

        private static bool TryParseKey(string? storageKey, out Guid id)
        {
            id = Guid.Empty;

            if (string.IsNullOrWhiteSpace(storageKey))
            {
                return false;
            }

            return Guid.TryParse(storageKey, out id);
        }

        private static string NormalizeFolder(string folder)
        {
            var trimmed = folder.Trim().Trim('/', '\\');

            if (trimmed.Length > 100)
            {
                trimmed = trimmed.Substring(0, 100);
            }

            return trimmed;
        }

        private static string ComputeSha256Hex(byte[] content)
        {
            using var sha = SHA256.Create();

            return Convert.ToHexString(sha.ComputeHash(content))
                .ToLowerInvariant();
        }
    }
}