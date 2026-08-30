using System.Security.Cryptography;
using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using LecturerModel = Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Lecturer;

namespace Academic_Staff_Engagement_Claim_Processing_System.Services
{
    public class AccountRegistrationResult
    {
        public bool Succeeded { get; set; }
        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }
    }

    public class AccountRegistrationRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string Rank { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string GovernmentId { get; set; } = string.Empty;
        public string SignatureData { get; set; } = string.Empty;
        public ManagementTitle? ManagementTitle { get; set; }

        // Who is registering this account — used both for
        // Lecturer.SignatureCapturedByHodId attribution and for the
        // audit log. Comes from the authenticated caller, never a
        // form field.
        public int RegisteringUserId { get; set; }
        public string ActorUsername { get; set; } = string.Empty;
        public string ActorRole { get; set; } = string.Empty;
        public string? IpAddress { get; set; }
    }

    public class AccountRegistrationService
    {
        private readonly ApplicationDbContext _context;
        private readonly EmailService _emailService;
        private readonly IWebHostEnvironment _environment;
        private readonly AuditLogger _auditLogger;

        public AccountRegistrationService(
            ApplicationDbContext context,
            EmailService emailService,
            IWebHostEnvironment environment,
            AuditLogger auditLogger)
        {
            _context = context;
            _emailService = emailService;
            _environment = environment;
            _auditLogger = auditLogger;
        }

        public async Task<AccountRegistrationResult> RegisterAsync(AccountRegistrationRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return Fail("Please enter the user's full name.");

            if (string.IsNullOrWhiteSpace(request.Email))
                return Fail("Please enter the user's email address.");

            if (string.IsNullOrWhiteSpace(request.Role))
                return Fail("Please select the user's system role.");

            if (string.IsNullOrWhiteSpace(request.SignatureData))
                return Fail("Please provide the user's digital signature.");

            string name = request.Name.Trim();
            string email = request.Email.Trim().ToLowerInvariant();
            string role = request.Role.Trim();
            string rank = request.Rank?.Trim() ?? string.Empty;
            string department = request.Department?.Trim() ?? string.Empty;
            string governmentId = request.GovernmentId?.Trim() ?? string.Empty;

            if (!role.Equals("Lecturer", StringComparison.OrdinalIgnoreCase) &&
                !role.Equals("HOD", StringComparison.OrdinalIgnoreCase) &&
                !role.Equals("Dean", StringComparison.OrdinalIgnoreCase) &&
                !role.Equals("Management", StringComparison.OrdinalIgnoreCase))
            {
                return Fail("Invalid system role selected.");
            }

            LecturerRank? lecturerRank = null;

            if (role.Equals("Lecturer", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(governmentId))
                    return Fail("Please enter the lecturer's Government ID.");

                if (string.IsNullOrWhiteSpace(rank))
                    return Fail("Please select the lecturer's academic rank.");

                if (!Enum.TryParse<LecturerRank>(rank.Replace(" ", ""), true, out var parsedRank))
                    return Fail("The selected lecturer rank is invalid.");

                lecturerRank = parsedRank;
            }

            if (role.Equals("HOD", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(department))
            {
                return Fail("Please enter the HOD's department.");
            }

            if (role.Equals("Management", StringComparison.OrdinalIgnoreCase) &&
                request.ManagementTitle is null)
            {
                return Fail("Please select the Management office (HR Officer, DVCAR, or Vice Chancellor).");
            }

            string username = name;

            bool usernameExists =
                await _context.AdminAccounts.AnyAsync(a => a.UserName == username) ||
                await _context.Lecturers.AnyAsync(l => l.UserName == username);

            if (usernameExists)
                return Fail($"The username '{username}' already exists. Please use a different name.");

            bool emailExists =
                await _context.AdminAccounts.AnyAsync(a => a.Email == email) ||
                await _context.Lecturers.AnyAsync(l => l.Email == email);

            if (emailExists)
                return Fail($"An account with the email address '{email}' already exists.");

            string password = GeneratePassword();

            string signatureFilePath;
            try
            {
                signatureFilePath = await SaveSignatureAsync(request.SignatureData, username);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SIGNATURE ERROR: {ex}");
                return Fail("The user's signature could not be saved.");
            }

            string signatureAbsolutePath = Path.Combine(
                _environment.WebRootPath,
                signatureFilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

            string signatureHash;
            try
            {
                signatureHash = await CalculateFileHashAsync(signatureAbsolutePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SIGNATURE HASH ERROR: {ex}");
                return Fail("The user's signature could not be verified.");
            }

            // ------------------------------------------------------------
            // CREATE ACCOUNT + AUDIT LOG — same transaction, so a failure
            // in either one rolls back both. Email is deliberately sent
            // AFTER commit, outside the transaction: a slow/failed email
            // should never hold a DB transaction open, and a failed email
            // should never undo an otherwise-successful account creation.
            // ------------------------------------------------------------

            using var transaction = await _context.Database.BeginTransactionAsync();

            int createdId;
            string entityType;
            string accountTypeLabel;

            try
            {
                if (role.Equals("Lecturer", StringComparison.OrdinalIgnoreCase))
                {
                    var registeringHod = await _context.Hods
                        .FirstOrDefaultAsync(h => h.Id == request.RegisteringUserId);

                    if (registeringHod is null)
                    {
                        await transaction.RollbackAsync();
                        return Fail("A registering HOD account could not be found.");
                    }

                    var lecturer = new LecturerModel(0, username, email)
                    {
                        Rank = lecturerRank,
                        Type = UserRole.PartTimeLecturer
                    };

                    var hasher = new PasswordHasher<LecturerModel>();
                    lecturer.SetPasswordHash(hasher.HashPassword(lecturer, password));
                    lecturer.SetGovernmentIdEncrypted(governmentId);
                    lecturer.CaptureSignature(signatureFilePath, signatureHash, registeringHod.Id);

                    _context.Lecturers.Add(lecturer);
                    await _context.SaveChangesAsync();

                    createdId = lecturer.Id;
                    entityType = "Lecturer";
                    accountTypeLabel = "Lecturer";
                }
                else if (role.Equals("HOD", StringComparison.OrdinalIgnoreCase))
                {
                    var hod = new Hod(0, username, email, department);
                    var hasher = new PasswordHasher<AdminAccount>();
                    hod.SetPasswordHash(hasher.HashPassword(hod, password));
                    hod.CaptureSignature(signatureFilePath, signatureHash);

                    _context.Hods.Add(hod);
                    await _context.SaveChangesAsync();

                    createdId = hod.Id;
                    entityType = "HOD";
                    accountTypeLabel = "HOD";
                }
                else if (role.Equals("Dean", StringComparison.OrdinalIgnoreCase))
                {
                    var dean = new Dean(0, username, email);
                    var hasher = new PasswordHasher<AdminAccount>();
                    dean.SetPasswordHash(hasher.HashPassword(dean, password));
                    dean.CaptureSignature(signatureFilePath, signatureHash);

                    _context.Deans.Add(dean);
                    await _context.SaveChangesAsync();

                    createdId = dean.Id;
                    entityType = "Dean";
                    accountTypeLabel = "Dean";
                }
                else // Management
                {
                    var management = new Management(0, username, email, request.ManagementTitle!.Value);
                    var hasher = new PasswordHasher<AdminAccount>();
                    management.SetPasswordHash(hasher.HashPassword(management, password));
                    management.CaptureSignature(signatureFilePath, signatureHash);

                    _context.ManagementAccounts.Add(management);
                    await _context.SaveChangesAsync();

                    createdId = management.Id;
                    entityType = "Management";
                    accountTypeLabel = $"Management ({request.ManagementTitle})";
                }

                await _auditLogger.LogAsync(
                    AuditAction.AccountCreated,
                    request.ActorUsername,
                    request.ActorRole,
                    request.RegisteringUserId > 0 ? request.RegisteringUserId : (int?)null,
                    entityType,
                    createdId,
                    $"Username: {username}",
                    request.IpAddress);

                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"DATABASE ERROR: {ex}");
                return Fail("The user account could not be saved to the database.");
            }

            return await FinishAsync(email, name, username, password, accountTypeLabel);
        }

        private async Task<AccountRegistrationResult> FinishAsync(
            string email, string name, string username, string password, string accountType)
        {
            var result = new AccountRegistrationResult { Succeeded = true };

            try
            {
                await _emailService.SendWelcomeEmailAsync(email, name, username, password);
                result.SuccessMessage =
                    $"{accountType} account for {name} was registered successfully. " +
                    $"The login credentials have been sent to {email}.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"EMAIL ERROR: {ex}");
                result.SuccessMessage =
                    $"{accountType} account for {name} was created successfully, " +
                    $"but the welcome email could not be sent. Username: {username}";
            }

            return result;
        }

        private static AccountRegistrationResult Fail(string message) =>
            new() { Succeeded = false, ErrorMessage = message };

        private async Task<string> SaveSignatureAsync(string signatureData, string username)
        {
            const string prefix = "data:image/png;base64,";

            if (!signatureData.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Invalid signature format.");

            byte[] imageBytes;
            try
            {
                imageBytes = Convert.FromBase64String(signatureData.Substring(prefix.Length));
            }
            catch
            {
                throw new InvalidOperationException("The signature image is invalid.");
            }

            if (imageBytes.Length == 0)
                throw new InvalidOperationException("The signature image is empty.");

            const int maxSignatureBytes = 500_000;
            if (imageBytes.Length > maxSignatureBytes)
                throw new InvalidOperationException("The signature image is too large. Please use a smaller image.");

            byte[] pngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
            if (imageBytes.Length < pngSignature.Length ||
                !imageBytes.Take(pngSignature.Length).SequenceEqual(pngSignature))
            {
                throw new InvalidOperationException("The signature image is not a valid PNG file.");
            }

            string signaturesDirectory = Path.Combine(_environment.WebRootPath, "uploads", "signatures");
            Directory.CreateDirectory(signaturesDirectory);

            string safeUsername = string.Concat(
                username.Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-'));

            if (string.IsNullOrWhiteSpace(safeUsername))
                safeUsername = "user";

            string fileName = $"{safeUsername}_{Guid.NewGuid():N}.png";
            string absolutePath = Path.Combine(signaturesDirectory, fileName);

            await File.WriteAllBytesAsync(absolutePath, imageBytes);

            return $"/uploads/signatures/{fileName}";
        }

        private static async Task<string> CalculateFileHashAsync(string filePath)
        {
            await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var sha256 = SHA256.Create();
            byte[] hash = await sha256.ComputeHashAsync(stream);
            return Convert.ToHexString(hash);
        }

        private static string GeneratePassword()
        {
            const string uppercase = "ABCDEFGHJKLMNPQRSTUVWXYZ";
            const string lowercase = "abcdefghijkmnopqrstuvwxyz";
            const string numbers = "23456789";
            const string symbols = "@#$%";
            string allCharacters = uppercase + lowercase + numbers + symbols;

            var password = new char[10];
            password[0] = uppercase[RandomNumberGenerator.GetInt32(uppercase.Length)];
            password[1] = lowercase[RandomNumberGenerator.GetInt32(lowercase.Length)];
            password[2] = numbers[RandomNumberGenerator.GetInt32(numbers.Length)];
            password[3] = symbols[RandomNumberGenerator.GetInt32(symbols.Length)];

            for (int i = 4; i < password.Length; i++)
                password[i] = allCharacters[RandomNumberGenerator.GetInt32(allCharacters.Length)];

            for (int i = password.Length - 1; i > 0; i--)
            {
                int j = RandomNumberGenerator.GetInt32(i + 1);
                (password[i], password[j]) = (password[j], password[i]);
            }

            return new string(password);
        }
    }
}