using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Academic_Staff_Engagement_Claim_Processing_System.Services
{
    public class AccountRegistrationRequest
    {
        public string Name { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string Department { get; set; } = string.Empty;

        public string Rank { get; set; } = string.Empty;

        public string Role { get; set; } = string.Empty;

        public string GovernmentId { get; set; } = string.Empty;

        public string SignatureData { get; set; } = string.Empty;

        public string? ManagementTitle { get; set; }

        public UserRole? LecturerType { get; set; }

        public int RegisteringUserId { get; set; }

        public string ActorUsername { get; set; } = string.Empty;

        public string ActorRole { get; set; } = string.Empty;

        public string? IpAddress { get; set; }
    }


    public class AccountRegistrationResult
    {
        public bool Succeeded { get; private set; }

        public string? SuccessMessage { get; set; }

        public string? ErrorMessage { get; private set; }

        public int? CreatedUserId { get; private set; }

        public string? Username { get; private set; }


        public static AccountRegistrationResult Success(
            string message,
            int createdUserId,
            string username)
        {
            return new AccountRegistrationResult
            {
                Succeeded = true,
                SuccessMessage = message,
                CreatedUserId = createdUserId,
                Username = username
            };
        }


        public static AccountRegistrationResult Fail(
            string message)
        {
            return new AccountRegistrationResult
            {
                Succeeded = false,
                ErrorMessage = message
            };
        }
    }


    public class AccountRegistrationService
    {
        private readonly ApplicationDbContext _context;

        private readonly AuditLogger _auditLogger;

        private readonly EmailService _emailService;

        private readonly ILogger<AccountRegistrationService> _logger;

        private readonly IWebHostEnvironment _environment;


        public AccountRegistrationService(
            ApplicationDbContext context,
            AuditLogger auditLogger,
            EmailService emailService,
            ILogger<AccountRegistrationService> logger,
            IWebHostEnvironment environment)
        {
            _context = context;
            _auditLogger = auditLogger;
            _emailService = emailService;
            _logger = logger;
            _environment = environment;
        }


        public async Task<AccountRegistrationResult> RegisterAsync(
            AccountRegistrationRequest request)
        {
            if (request == null)
            {
                return AccountRegistrationResult.Fail(
                    "The registration request is invalid.");
            }


            request.Name =
                request.Name?.Trim()
                ?? string.Empty;

            request.Email =
                request.Email?.Trim()
                ?? string.Empty;

            request.Department =
                request.Department?.Trim()
                ?? string.Empty;

            request.Rank =
                request.Rank?.Trim()
                ?? string.Empty;

            request.Role =
                request.Role?.Trim()
                ?? string.Empty;

            request.GovernmentId =
                request.GovernmentId?.Trim()
                ?? string.Empty;


            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return AccountRegistrationResult.Fail(
                    "Full name is required.");
            }


            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return AccountRegistrationResult.Fail(
                    "Email address is required.");
            }


            if (string.IsNullOrWhiteSpace(request.Role))
            {
                return AccountRegistrationResult.Fail(
                    "User role is required.");
            }


            if (!IsValidEmail(request.Email))
            {
                return AccountRegistrationResult.Fail(
                    "The email address is not valid.");
            }


            if (request.Role.Equals(
                    "Lecturer",
                    StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(
                    request.GovernmentId))
                {
                    return AccountRegistrationResult.Fail(
                        "Government ID is required.");
                }


                if (string.IsNullOrWhiteSpace(
                    request.Rank))
                {
                    return AccountRegistrationResult.Fail(
                        "Academic rank is required.");
                }


                if (!request.LecturerType.HasValue)
                {
                    return AccountRegistrationResult.Fail(
                        "Lecturer employment type is required.");
                }


                if (request.LecturerType.Value !=
                        UserRole.PartTimeLecturer &&
                    request.LecturerType.Value !=
                        UserRole.FullTimeLecturer)
                {
                    return AccountRegistrationResult.Fail(
                        "Only Part-Time Lecturer or Full-Time Lecturer is allowed.");
                }


                if (string.IsNullOrWhiteSpace(
                    request.SignatureData))
                {
                    return AccountRegistrationResult.Fail(
                        "Digital signature is required.");
                }
            }


            bool emailExists =
                await _context.Lecturers
                    .AnyAsync(x =>
                        x.Email == request.Email)
                ||
                await _context.AdminAccounts
                    .AnyAsync(x =>
                        x.Email == request.Email);


            if (emailExists)
            {
                return AccountRegistrationResult.Fail(
                    $"An account with the email '{request.Email}' already exists.");
            }


            string username =
                request.Name;


            bool usernameExists =
                await _context.Lecturers
                    .AnyAsync(x =>
                        x.UserName == username)
                ||
                await _context.AdminAccounts
                    .AnyAsync(x =>
                        x.UserName == username);


            if (usernameExists)
            {
                return AccountRegistrationResult.Fail(
                    $"An account with the username '{username}' already exists.");
            }


            string password =
                GenerateSecurePassword();


            string? signatureFilePath =
                null;

            string? signatureHash =
                null;


            try
            {
                if (!request.Role.Equals(
                        "Lecturer",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return AccountRegistrationResult.Fail(
                        $"The role '{request.Role}' is not supported by this registration workflow.");
                }


                var registeringHod =
                    await _context.Hods
                        .FirstOrDefaultAsync(
                            h => h.Id ==
                                 request.RegisteringUserId);


                if (registeringHod == null)
                {
                    return AccountRegistrationResult.Fail(
                        "The registering HOD account could not be found.");
                }


                var signatureResult =
                    await SaveSignatureAsync(
                        request.SignatureData,
                        username);


                if (!signatureResult.Success)
                {
                    return AccountRegistrationResult.Fail(
                        signatureResult.ErrorMessage
                        ??
                        "The digital signature could not be saved.");
                }


                signatureFilePath =
                    signatureResult.FilePath;


                signatureHash =
                    ComputeSha256FromBase64Png(
                        request.SignatureData);


                if (!Enum.TryParse<LecturerRank>(
                        request.Rank,
                        true,
                        out LecturerRank lecturerRank))
                {
                    DeleteSignatureFileIfExists(
                        signatureFilePath);

                    return AccountRegistrationResult.Fail(
                        "The selected academic rank is invalid.");
                }


                var lecturer =
                    new Lecturer(
                        0,
                        username,
                        request.Email)
                    {
                        Rank = lecturerRank,

                        Type =
                            request.LecturerType.Value
                    };


                var passwordHasher =
                    new PasswordHasher<Lecturer>();


                lecturer.SetPasswordHash(
                    passwordHasher.HashPassword(
                        lecturer,
                        password));


                lecturer.SetGovernmentIdEncrypted(
                    request.GovernmentId);


                lecturer.CaptureSignature(
                    signatureFilePath,
                    signatureHash,
                    registeringHod.Id);


                var strategy =
                    _context.Database
                        .CreateExecutionStrategy();


                AccountRegistrationResult?
                    registrationResult = null;


                await strategy.ExecuteAsync(
                    async () =>
                    {
                        await using var transaction =
                            await _context.Database
                                .BeginTransactionAsync();


                        try
                        {
                            _context.Lecturers.Add(
                                lecturer);


                            await _context.SaveChangesAsync();


                            _auditLogger.Add(
                                AuditAction.AccountCreated,
                                request.ActorUsername,
                                request.ActorRole,
                                request.RegisteringUserId > 0
                                    ? request.RegisteringUserId
                                    : null,
                                "Lecturer",
                                lecturer.Id,
                                $"Lecturer account created. Username: {username}. Email: {request.Email}.",
                                request.IpAddress);


                            await _context.SaveChangesAsync();


                            await transaction.CommitAsync();


                            registrationResult =
                                AccountRegistrationResult.Success(
                                    $"{request.Name} was registered successfully.",
                                    lecturer.Id,
                                    username);
                        }
                        catch
                        {
                            await transaction.RollbackAsync();

                            throw;
                        }
                    });


                if (registrationResult == null)
                {
                    DeleteSignatureFileIfExists(
                        signatureFilePath);

                    return AccountRegistrationResult.Fail(
                        "The lecturer account could not be created.");
                }


                /*
                 * The database transaction has already been committed.
                 *
                 * Therefore, an email failure does NOT delete or roll back
                 * the lecturer account.
                 */
                try
                {
                    await _emailService
                        .SendWelcomeEmailAsync(
                            request.Email,
                            request.Name,
                            username,
                            password);


                    registrationResult.SuccessMessage =
                        $"{request.Name} was registered successfully and the welcome email was sent.";
                }
                catch (Exception emailException)
                {
                    _logger.LogError(
                        emailException,
                        "Lecturer account was created but welcome email failed for {Email}.",
                        request.Email);


                    registrationResult.SuccessMessage =
                        $"{request.Name} was registered successfully, but the welcome email could not be sent.";
                }


                return registrationResult;
            }
            catch (DbUpdateException dbException)
            {
                _logger.LogError(
                    dbException,
                    "Database error while registering user {Name} / {Email}.",
                    request.Name,
                    request.Email);


                DeleteSignatureFileIfExists(
                    signatureFilePath);


                return AccountRegistrationResult.Fail(
                    GetDatabaseErrorMessage(
                        dbException));
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Unexpected error while registering user {Name} / {Email}.",
                    request.Name,
                    request.Email);


                DeleteSignatureFileIfExists(
                    signatureFilePath);


                return AccountRegistrationResult.Fail(
                    "An unexpected error occurred while creating the account. Check the application logs for details.");
            }
        }


        private async Task<SignatureSaveResult>
            SaveSignatureAsync(
                string signatureData,
                string username)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(
                    signatureData))
                {
                    return SignatureSaveResult.CreateFailure(
                        "Digital signature is required.");
                }


                const string prefix =
                    "data:image/png;base64,";


                if (!signatureData.StartsWith(
                        prefix,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return SignatureSaveResult.CreateFailure(
                        "The signature must be a PNG image.");
                }


                string base64 =
                    signatureData[prefix.Length..];


                byte[] imageBytes;


                try
                {
                    imageBytes =
                        Convert.FromBase64String(
                            base64);
                }
                catch (FormatException)
                {
                    return SignatureSaveResult.CreateFailure(
                        "The digital signature data is invalid.");
                }


                if (imageBytes.Length == 0)
                {
                    return SignatureSaveResult.CreateFailure(
                        "The digital signature is empty.");
                }


                const int maxSignatureSize =
                    500 * 1024;


                if (imageBytes.Length >
                    maxSignatureSize)
                {
                    return SignatureSaveResult.CreateFailure(
                        "The digital signature is too large. Please sign again.");
                }


                if (!IsPng(imageBytes))
                {
                    return SignatureSaveResult.CreateFailure(
                        "The uploaded signature is not a valid PNG image.");
                }


                string webRoot =
                    _environment.WebRootPath;


                if (string.IsNullOrWhiteSpace(
                    webRoot))
                {
                    return SignatureSaveResult.CreateFailure(
                        "The web root directory could not be located.");
                }


                string signatureDirectory =
                    Path.Combine(
                        webRoot,
                        "uploads",
                        "signatures");


                Directory.CreateDirectory(
                    signatureDirectory);


                string safeFileName =
                    $"{Guid.NewGuid():N}.png";


                string physicalPath =
                    Path.Combine(
                        signatureDirectory,
                        safeFileName);


                await File.WriteAllBytesAsync(
                    physicalPath,
                    imageBytes);


                string relativePath =
                    $"/uploads/signatures/{safeFileName}";


                return SignatureSaveResult.CreateSuccess(
                    relativePath);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Error saving signature for {Username}.",
                    username);


                return SignatureSaveResult.CreateFailure(
                    "The digital signature could not be saved.");
            }
        }


        private static string
            ComputeSha256FromBase64Png(
                string signatureData)
        {
            const string prefix =
                "data:image/png;base64,";


            if (!signatureData.StartsWith(
                    prefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Invalid signature format.");
            }


            string base64 =
                signatureData[prefix.Length..];


            byte[] bytes =
                Convert.FromBase64String(
                    base64);


            using SHA256 sha256 =
                SHA256.Create();


            byte[] hash =
                sha256.ComputeHash(bytes);


            return Convert.ToHexString(hash);
        }


        private static bool IsPng(
            byte[] bytes)
        {
            if (bytes.Length < 8)
            {
                return false;
            }


            return bytes[0] == 0x89 &&
                   bytes[1] == 0x50 &&
                   bytes[2] == 0x4E &&
                   bytes[3] == 0x47 &&
                   bytes[4] == 0x0D &&
                   bytes[5] == 0x0A &&
                   bytes[6] == 0x1A &&
                   bytes[7] == 0x0A;
        }


        private static string
            GenerateSecurePassword()
        {
            const string upper =
                "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

            const string lower =
                "abcdefghijklmnopqrstuvwxyz";

            const string numbers =
                "0123456789";

            const string symbols =
                "!@#$%^&*_-+=";

            const string all =
                upper +
                lower +
                numbers +
                symbols;


            const int length =
                14;


            var password =
                new char[length];


            password[0] =
                GetRandomCharacter(
                    upper);

            password[1] =
                GetRandomCharacter(
                    lower);

            password[2] =
                GetRandomCharacter(
                    numbers);

            password[3] =
                GetRandomCharacter(
                    symbols);


            for (int i = 4;
                 i < length;
                 i++)
            {
                password[i] =
                    GetRandomCharacter(
                        all);
            }


            Shuffle(password);


            return new string(
                password);
        }


        private static char
            GetRandomCharacter(
                string characters)
        {
            int index =
                RandomNumberGenerator
                    .GetInt32(
                        characters.Length);


            return characters[index];
        }


        private static void Shuffle(
            char[] characters)
        {
            for (int i =
                    characters.Length - 1;
                 i > 0;
                 i--)
            {
                int j =
                    RandomNumberGenerator
                        .GetInt32(
                            i + 1);


                (characters[i], characters[j]) =
                    (characters[j], characters[i]);
            }
        }


        private static bool IsValidEmail(
            string email)
        {
            try
            {
                var address =
                    new System.Net.Mail.MailAddress(
                        email);


                return address.Address.Equals(
                    email,
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }


        private static string
            GetDatabaseErrorMessage(
                DbUpdateException exception)
        {
            Exception? current =
                exception;


            while (current != null)
            {
                if (!string.IsNullOrWhiteSpace(
                    current.Message))
                {
                    string message =
                        current.Message
                            .ToLowerInvariant();


                    if (message.Contains("unique") ||
                        message.Contains("duplicate") ||
                        message.Contains("ux_") ||
                        message.Contains(
                            "cannot insert duplicate"))
                    {
                        return
                            "The account could not be created because the username or email already exists.";
                    }
                }


                current =
                    current.InnerException;
            }


            return
                "The account could not be saved to the database. Check the application logs for the database error.";
        }


        private static void
            DeleteSignatureFileIfExists(
                string? relativePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(
                    relativePath))
                {
                    return;
                }


                string cleanPath =
                    relativePath.TrimStart(
                        '/',
                        '\\');


                string physicalPath =
                    Path.Combine(
                        Directory.GetCurrentDirectory(),
                        "wwwroot",
                        cleanPath);


                if (File.Exists(
                    physicalPath))
                {
                    File.Delete(
                        physicalPath);
                }
            }
            catch
            {
                // Do not replace the original
                // registration error with a
                // cleanup error.
            }
        }


        private class SignatureSaveResult
        {
            public bool Success
            {
                get;
                private set;
            }


            public string? FilePath
            {
                get;
                private set;
            }


            public string? ErrorMessage
            {
                get;
                private set;
            }


            public static SignatureSaveResult
                CreateSuccess(
                    string filePath)
            {
                return new SignatureSaveResult
                {
                    Success = true,

                    FilePath = filePath
                };
            }


            public static SignatureSaveResult
                CreateFailure(
                    string message)
            {
                return new SignatureSaveResult
                {
                    Success = false,

                    ErrorMessage = message
                };
            }
        }
    }
}