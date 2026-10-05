using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages.Migration
{
    public class SignaturesModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IAmazonS3 _s3Client;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;

        private readonly string _bucketName;

        public List<string> Uploaded { get; private set; } = new();
        public List<string> AlreadyUploaded { get; private set; } = new();
        public List<string> MissingFiles { get; private set; } = new();
        public List<string> Failed { get; private set; } = new();

        public SignaturesModel(
            ApplicationDbContext context,
            IAmazonS3 s3Client,
            IConfiguration configuration,
            IWebHostEnvironment environment)
        {
            _context = context;
            _s3Client = s3Client;
            _configuration = configuration;
            _environment = environment;

            _bucketName =
                _configuration["R2:BucketName"]
                ?? throw new InvalidOperationException(
                    "R2:BucketName is not configured.");
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var migratedPaths =
                new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase);

            var lecturers = await _context.Lecturers
                .Where(x => !string.IsNullOrWhiteSpace(x.SignatureFilePath))
                .ToListAsync();

            foreach (var lecturer in lecturers)
            {
                await MigrateSignatureAsync(
                    lecturer.SignatureFilePath!,
                    "Lecturer",
                    lecturer.UserName,
                    migratedPaths);
            }

            var adminAccounts = await _context.AdminAccounts
                .Where(x => !string.IsNullOrWhiteSpace(x.SignatureFilePath))
                .ToListAsync();

            foreach (var account in adminAccounts)
            {
                await MigrateSignatureAsync(
                    account.SignatureFilePath!,
                    "AdminAccount",
                    account.UserName,
                    migratedPaths);
            }

            var contractSignatures =
                await _context.ContractSignatures
                    .Where(x => !string.IsNullOrWhiteSpace(x.SignatureFilePath))
                    .ToListAsync();

            foreach (var signature in contractSignatures)
            {
                await MigrateSignatureAsync(
                    signature.SignatureFilePath!,
                    "ContractSignature",
                    signature.Id.ToString(),
                    migratedPaths);
            }

            return Page();
        }

        private async Task MigrateSignatureAsync(
            string databasePath,
            string entityType,
            string identifier,
            Dictionary<string, string> migratedPaths)
        {
            try
            {
                var normalizedPath = databasePath
                    .Replace("\\", "/")
                    .TrimStart('/');

                if (migratedPaths.TryGetValue(
                    normalizedPath,
                    out var existingKey))
                {
                    AlreadyUploaded.Add(
                        $"{entityType} [{identifier}] -> {existingKey}");

                    return;
                }

                var relativePath = normalizedPath.Replace(
                    "/",
                    Path.DirectorySeparatorChar.ToString());

                var localPath = Path.Combine(
                    _environment.WebRootPath,
                    relativePath);

                if (!System.IO.File.Exists(localPath))
                {
                    MissingFiles.Add(
                        $"{entityType} [{identifier}] -> {databasePath}");

                    return;
                }

                var content =
                    await System.IO.File.ReadAllBytesAsync(localPath);

                var fileName = Path.GetFileName(localPath);

                var r2Key = $"signatures/{fileName}";

                using var stream = new MemoryStream(content);

                var uploadRequest = new PutObjectRequest
                {
                    BucketName = _bucketName,
                    Key = r2Key,
                    InputStream = stream,
                    ContentType = "image/png",
                    DisablePayloadSigning = true,
                    DisableDefaultChecksumValidation = true,
                    UseChunkEncoding = false
                };

                await _s3Client.PutObjectAsync(uploadRequest);

                await _s3Client.GetObjectMetadataAsync(
                    new GetObjectMetadataRequest
                    {
                        BucketName = _bucketName,
                        Key = r2Key
                    });

                migratedPaths[normalizedPath] = r2Key;

                Uploaded.Add(
                    $"{entityType} [{identifier}] -> {r2Key}");
            }
            catch (Exception ex)
            {
                Failed.Add(
                    $"{entityType} [{identifier}] -> {ex.Message}");
            }
        }
    }
}