using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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

        public string Faculty { get; set; } = string.Empty;

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

        public static AccountRegistrationResult Fail(string message)
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
                    "Registration request is required.");
            }

            request.Name = request.Name?.Trim() ?? string.Empty;
            request.Email = request.Email?.Trim() ?? string.Empty;
            request.Department = request.Department?.Trim() ?? string.Empty;
            request.Faculty = request.Faculty?.Trim() ?? string.Empty;
            request.Rank = request.Rank?.Trim() ?? string.Empty;
            request.Role = request.Role?.Trim() ?? string.Empty;
            request.GovernmentId = request.GovernmentId?.Trim() ?? string.Empty;
            request.SignatureData = request.SignatureData?.Trim() ?? string.Empty;
            request.ManagementTitle = request.ManagementTitle?.Trim();

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

            if (!IsValidEmail(request.Email))
            {
                return AccountRegistrationResult.Fail(
                    "Please provide a valid email address.");
            }

            if (string.IsNullOrWhiteSpace(request.Role))
            {
                return AccountRegistrationResult.Fail(
                    "Account role is required.");
            }

            bool isLecturer = string.Equals(
                request.Role,
                "Lecturer",
                StringComparison.OrdinalIgnoreCase);

            bool isHod = string.Equals(
                request.Role,
                "HOD",
                StringComparison.OrdinalIgnoreCase);

            bool isManagement = string.Equals(
                request.Role,
                "Management",
                StringComparison.OrdinalIgnoreCase);

            bool isDean = string.Equals(
                request.Role,
                "Dean",
                StringComparison.OrdinalIgnoreCase);

            if (!isLecturer && !isHod && !isManagement && !isDean)
            {
                return AccountRegistrationResult.Fail(
                    "The selected account role is not supported.");
            }

            if (string.IsNullOrWhiteSpace(request.SignatureData))
            {
                return AccountRegistrationResult.Fail(
                    "A digital signature is required.");
            }

            if (isLecturer)
            {
                if (string.IsNullOrWhiteSpace(request.GovernmentId))
                {
                    return AccountRegistrationResult.Fail(
                        "Government ID is required for lecturer registration.");
                }

                if (string.IsNullOrWhiteSpace(request.Rank))
                {
                    return AccountRegistrationResult.Fail(
                        "Academic rank is required for lecturer registration.");
                }

                if (!request.LecturerType.HasValue)
                {
                    return AccountRegistrationResult.Fail(
                        "Employment type is required for lecturer registration.");
                }

                if (request.LecturerType.Value != UserRole.PartTimeLecturer &&
                    request.LecturerType.Value != UserRole.FullTimeLecturer)
                {
                    return AccountRegistrationResult.Fail(
                        "Only Part-Time or Full-Time Lecturer is allowed.");
                }

                if (string.IsNullOrWhiteSpace(request.Department))
                {
                    return AccountRegistrationResult.Fail(
                        "Department is required for lecturer registration.");
                }
            }

            Faculty? parsedFaculty = null;

            if (isHod)
            {
                if (string.IsNullOrWhiteSpace(request.Faculty))
                {
                    return AccountRegistrationResult.Fail(
                        "Faculty is required for HOD registration.");
                }

                if (!Enum.TryParse<Faculty>(
                        request.Faculty,
                        true,
                        out Faculty hodFaculty))
                {
                    return AccountRegistrationResult.Fail(
                        "The selected faculty is invalid.");
                }

                parsedFaculty = hodFaculty;
            }

            ManagementTitle? parsedManagementTitle = null;

            if (isManagement)
            {
                if (string.IsNullOrWhiteSpace(request.ManagementTitle))
                {
                    return AccountRegistrationResult.Fail(
                        "Management title is required.");
                }

                if (!Enum.TryParse<ManagementTitle>(
                        request.ManagementTitle,
                        true,
                        out ManagementTitle managementTitle))
                {
                    return AccountRegistrationResult.Fail(
                        "The selected management title is invalid.");
                }

                parsedManagementTitle = managementTitle;
            }

            if (isDean)
            {
                bool deanExists =
                    await _context.Deans.AnyAsync();

                if (deanExists)
                {
                    return AccountRegistrationResult.Fail(
                        "A Dean account already exists. Only the initial Dean account can be created through this registration process.");
                }
            }

            Hod? registeringHod = null;

            if (isLecturer)
            {
                registeringHod = await _context.Hods
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        h => h.Id == request.RegisteringUserId);

                if (registeringHod == null)
                {
                    return AccountRegistrationResult.Fail(
                        "The registering HOD account could not be found.");
                }

                if (!string.Equals(
                        registeringHod.UserName,
                        request.ActorUsername,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return AccountRegistrationResult.Fail(
                        "The registering HOD account could not be verified.");
                }

                if (!Enum.TryParse<Department>(
                        request.Department,
                        true,
                        out Department selectedDepartment))
                {
                    return AccountRegistrationResult.Fail(
                        "The selected department is invalid.");
                }

                if (!FacultyDepartments.IsValidDepartment(
                    registeringHod.Faculty,
                    selectedDepartment))
                {
                    return AccountRegistrationResult.Fail(
                        "The selected department does not belong to your faculty.");
                }
            }
            else if (!isDean)
            {
                var registeringDean = await _context.Deans
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        d => d.Id == request.RegisteringUserId);

                if (registeringDean == null)
                {
                    return AccountRegistrationResult.Fail(
                        "The registering Dean account could not be found.");
                }

                if (!string.Equals(
                        registeringDean.UserName,
                        request.ActorUsername,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return AccountRegistrationResult.Fail(
                        "The registering Dean account could not be verified.");
                }
            }

            bool emailExists =
                await _context.Lecturers.AnyAsync(
                    x => x.Email == request.Email) ||
                await _context.Hods.AnyAsync(
                    x => x.Email == request.Email) ||
                await _context.Deans.AnyAsync(
                    x => x.Email == request.Email) ||
                await _context.ManagementAccounts.AnyAsync(
                    x => x.Email == request.Email);

            if (emailExists)
            {
                return AccountRegistrationResult.Fail(
                    "An account with this email address already exists.");
            }

            string username =
                GenerateUsername(request.Name);

            bool usernameExists =
                await _context.Lecturers.AnyAsync(
                    x => x.UserName == username) ||
                await _context.Hods.AnyAsync(
                    x => x.UserName == username) ||
                await _context.Deans.AnyAsync(
                    x => x.UserName == username) ||
                await _context.ManagementAccounts.AnyAsync(
                    x => x.UserName == username);

            if (usernameExists)
            {
                username =
                    $"{username}{RandomNumberGenerator.GetInt32(100, 1000)}";
            }

            string temporaryPassword =
                GenerateTemporaryPassword();

            var passwordHasher =
                new PasswordHasher<object>();

            string signatureRelativePath = string.Empty;
            string signatureAbsolutePath = string.Empty;

            try
            {
                string signatureFolder =
                    Path.Combine(
                        _environment.WebRootPath,
                        "uploads",
                        "signatures");

                Directory.CreateDirectory(
                    signatureFolder);

                string safeUsername =
                    SanitizeFileName(username);

                string signatureFileName =
                    $"{safeUsername}_{Guid.NewGuid():N}.png";

                signatureAbsolutePath =
                    Path.Combine(
                        signatureFolder,
                        signatureFileName);

                string base64Signature =
                    request.SignatureData;

                if (base64Signature.Contains(","))
                {
                    base64Signature =
                        base64Signature[
                            (base64Signature.IndexOf(',') + 1)..];
                }

                byte[] signatureBytes =
                    Convert.FromBase64String(
                        base64Signature);

                if (signatureBytes.Length == 0)
                {
                    return AccountRegistrationResult.Fail(
                        "The digital signature cannot be empty.");
                }

                const int maxSignatureBytes = 500_000;

                if (signatureBytes.Length > maxSignatureBytes)
                {
                    return AccountRegistrationResult.Fail(
                        "The digital signature is too large. Please capture a smaller signature.");
                }

                byte[] pngSignature =
                {
                    0x89,
                    0x50,
                    0x4E,
                    0x47,
                    0x0D,
                    0x0A,
                    0x1A,
                    0x0A
                };

                if (signatureBytes.Length < pngSignature.Length ||
                    !signatureBytes
                        .AsSpan(0, pngSignature.Length)
                        .SequenceEqual(pngSignature))
                {
                    return AccountRegistrationResult.Fail(
                        "The digital signature must be a valid PNG image.");
                }

                await File.WriteAllBytesAsync(
                    signatureAbsolutePath,
                    signatureBytes);

                signatureRelativePath =
                    Path.Combine(
                        "uploads",
                        "signatures",
                        signatureFileName)
                    .Replace("\\", "/");

                string signatureHash;

                using (var sha256 = SHA256.Create())
                {
                    byte[] hashBytes =
                        sha256.ComputeHash(
                            signatureBytes);

                    signatureHash =
                        Convert.ToHexString(
                            hashBytes);
                }

                var strategy =
                    _context.Database.CreateExecutionStrategy();

                AccountRegistrationResult? registrationResult =
                    null;

                await strategy.ExecuteAsync(async () =>
                {
                    await using var transaction =
                        await _context.Database.BeginTransactionAsync();

                    try
                    {
                        if (isLecturer)
                        {
                            if (!Enum.TryParse<LecturerRank>(
                                    request.Rank,
                                    true,
                                    out LecturerRank lecturerRank))
                            {
                                throw new InvalidOperationException(
                                    "The selected academic rank is invalid.");
                            }

                            if (!request.LecturerType.HasValue)
                            {
                                throw new InvalidOperationException(
                                    "Employment type is required.");
                            }

                            var transactionHod =
                                await _context.Hods
                                    .FirstOrDefaultAsync(
                                        h => h.Id == request.RegisteringUserId);

                            if (transactionHod == null)
                            {
                                throw new InvalidOperationException(
                                    "The registering HOD account could not be found.");
                            }

                            if (!string.Equals(
                                    transactionHod.UserName,
                                    request.ActorUsername,
                                    StringComparison.OrdinalIgnoreCase))
                            {
                                throw new InvalidOperationException(
                                    "The registering HOD account could not be verified.");
                            }

                            if (!Enum.TryParse<Department>(
                                    request.Department,
                                    true,
                                    out Department selectedDepartment))
                            {
                                throw new InvalidOperationException(
                                    "The selected department is invalid.");
                            }

                            if (!FacultyDepartments.IsValidDepartment(
                                    transactionHod.Faculty,
                                    selectedDepartment))
                            {
                                throw new InvalidOperationException(
                                    "The selected department does not belong to the HOD's faculty.");
                            }

                            Faculty lecturerFaculty =
                                transactionHod.Faculty;

                            var lecturer =
                                new Lecturer(
                                    0,
                                    username,
                                    request.Email)
                                {
                                    Rank = lecturerRank,
                                    Type = request.LecturerType.Value,
                                    Department = selectedDepartment.ToString(),
                                    Faculty = lecturerFaculty
                                };

                            lecturer.SetPasswordHash(
                                passwordHasher.HashPassword(
                                    lecturer,
                                    temporaryPassword));

                            lecturer.SetGovernmentIdEncrypted(
                                request.GovernmentId);

                            lecturer.CaptureSignature(
                                signatureRelativePath,
                                signatureHash,
                                transactionHod.Id);

                            _context.Lecturers.Add(
                                lecturer);

                            await _context.SaveChangesAsync();

                            await _auditLogger.LogAsync(
                                AuditAction.AccountCreated,
                                request.ActorUsername,
                                request.ActorRole,
                                request.RegisteringUserId,
                                "Lecturer",
                                lecturer.Id,
                                $"Lecturer account created for {lecturer.UserName}. " +
                                $"Department: {lecturer.Department}. " +
                                $"Faculty: {lecturer.Faculty}. " +
                                $"Employment Type: {lecturer.Type}.",
                                request.IpAddress);

                            await transaction.CommitAsync();

                            registrationResult =
                                AccountRegistrationResult.Success(
                                    $"{request.Name} was registered successfully as a {lecturer.Type}.",
                                    lecturer.Id,
                                    lecturer.UserName);
                        }
                        else if (isHod)
                        {
                            if (!parsedFaculty.HasValue)
                            {
                                throw new InvalidOperationException(
                                    "A valid faculty is required for HOD registration.");
                            }

                            var hod =
                                new Hod(
                                    0,
                                    username,
                                    request.Email,
                                    parsedFaculty.Value);

                            hod.SetPasswordHash(
                                passwordHasher.HashPassword(
                                    hod,
                                    temporaryPassword));

                            hod.CaptureSignature(
                                signatureRelativePath,
                                signatureHash);

                            _context.Hods.Add(hod);

                            await _context.SaveChangesAsync();

                            await _auditLogger.LogAsync(
                                AuditAction.AccountCreated,
                                request.ActorUsername,
                                request.ActorRole,
                                request.RegisteringUserId,
                                "HOD",
                                hod.Id,
                                $"HOD account created for {hod.UserName}. " +
                                $"Faculty: {hod.Faculty}.",
                                request.IpAddress);

                            await transaction.CommitAsync();

                            registrationResult =
                                AccountRegistrationResult.Success(
                                    $"{request.Name} was registered successfully as HOD.",
                                    hod.Id,
                                    hod.UserName);
                        }
                        else if (isManagement)
                        {
                            if (!parsedManagementTitle.HasValue)
                            {
                                throw new InvalidOperationException(
                                    "A valid management title is required.");
                            }

                            var management =
                                new Management(
                                    0,
                                    username,
                                    request.Email,
                                    parsedManagementTitle.Value);

                            management.SetPasswordHash(
                                passwordHasher.HashPassword(
                                    management,
                                    temporaryPassword));

                            management.CaptureSignature(
                                signatureRelativePath,
                                signatureHash);

                            _context.ManagementAccounts.Add(
                                management);

                            await _context.SaveChangesAsync();

                            await _auditLogger.LogAsync(
                                AuditAction.AccountCreated,
                                request.ActorUsername,
                                request.ActorRole,
                                request.RegisteringUserId,
                                "Management",
                                management.Id,
                                $"Management account created for {management.UserName}. " +
                                $"Management Title: {management.Title}.",
                                request.IpAddress);

                            await transaction.CommitAsync();

                            registrationResult =
                                AccountRegistrationResult.Success(
                                    $"{request.Name} was registered successfully.",
                                    management.Id,
                                    management.UserName);
                        }
                        else if (isDean)
                        {
                            var dean =
                                new Dean(
                                    0,
                                    username,
                                    request.Email);

                            dean.SetPasswordHash(
                                passwordHasher.HashPassword(
                                    dean,
                                    temporaryPassword));

                            dean.CaptureSignature(
                                signatureRelativePath,
                                signatureHash);

                            _context.Deans.Add(dean);

                            await _context.SaveChangesAsync();

                            await _auditLogger.LogAsync(
                                AuditAction.AccountCreated,
                                request.ActorUsername,
                                request.ActorRole,
                                request.RegisteringUserId,
                                "Dean",
                                dean.Id,
                                $"Dean account created for {dean.UserName}.",
                                request.IpAddress);

                            await transaction.CommitAsync();

                            registrationResult =
                                AccountRegistrationResult.Success(
                                    $"{request.Name} was registered successfully as Dean.",
                                    dean.Id,
                                    dean.UserName);
                        }
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                });

                if (registrationResult == null)
                {
                    DeleteSignatureFile(
                        signatureAbsolutePath);

                    return AccountRegistrationResult.Fail(
                        "The registration could not be completed.");
                }

                if (registrationResult.Succeeded)
                {
                    try
                    {
                        await _emailService.SendWelcomeEmailAsync(
                            request.Email,
                            request.Name,
                            registrationResult.Username ?? username,
                            temporaryPassword);
                    }
                    catch (Exception emailException)
                    {
                        _logger.LogError(
                            emailException,
                            "Account {Username} was created, but the welcome email could not be sent.",
                            registrationResult.Username ?? username);

                        registrationResult.SuccessMessage =
                            $"{registrationResult.SuccessMessage} " +
                            "The account was created, but the welcome email could not be sent. " +
                            "Please provide the login credentials manually.";
                    }
                }

                return registrationResult;
            }
            catch (DbUpdateException exception)
            {
                _logger.LogError(
                    exception,
                    "Database error while registering account for {Email}.",
                    request.Email);

                DeleteSignatureFile(
                    signatureAbsolutePath);

                return AccountRegistrationResult.Fail(
                    "The account could not be created because of a database error. Please try again.");
            }
            catch (InvalidOperationException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Registration validation failed for {Email}.",
                    request.Email);

                DeleteSignatureFile(
                    signatureAbsolutePath);

                return AccountRegistrationResult.Fail(
                    exception.Message);
            }
            catch (FormatException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Invalid signature format supplied for {Email}.",
                    request.Email);

                DeleteSignatureFile(
                    signatureAbsolutePath);

                return AccountRegistrationResult.Fail(
                    "The digital signature format is invalid. Please capture the signature again.");
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Unexpected error while registering account for {Email}.",
                    request.Email);

                DeleteSignatureFile(
                    signatureAbsolutePath);

                return AccountRegistrationResult.Fail(
                    "An unexpected error occurred while creating the account. Please try again.");
            }
        }

        private static void DeleteSignatureFile(
            string signatureAbsolutePath)
        {
            if (!string.IsNullOrWhiteSpace(signatureAbsolutePath) &&
                File.Exists(signatureAbsolutePath))
            {
                try
                {
                    File.Delete(signatureAbsolutePath);
                }
                catch
                {
                }
            }
        }

        private static bool IsValidEmail(string email)
        {
            try
            {
                var address =
                    new System.Net.Mail.MailAddress(email);

                return string.Equals(
                    address.Address,
                    email,
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static string GenerateUsername(string name)
        {
            string[] parts =
                name.Trim()
                    .Split(
                        ' ',
                        StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 0)
            {
                return
                    $"user{RandomNumberGenerator.GetInt32(1000, 9999)}";
            }

            string firstName =
                SanitizeUsernamePart(parts[0]);

            string lastName =
                parts.Length > 1
                    ? SanitizeUsernamePart(parts[^1])
                    : string.Empty;

            string username;

            if (!string.IsNullOrWhiteSpace(lastName))
            {
                username =
                    $"{firstName}.{lastName}";
            }
            else
            {
                username =
                    firstName;
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                username =
                    $"user{RandomNumberGenerator.GetInt32(1000, 9999)}";
            }

            return username.ToLowerInvariant();
        }

        private static string SanitizeUsernamePart(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            char[] characters =
                value.Trim()
                    .ToLowerInvariant()
                    .ToCharArray();

            var result =
                new System.Text.StringBuilder();

            foreach (char character in characters)
            {
                if (char.IsLetterOrDigit(character))
                {
                    result.Append(character);
                }
            }

            return result.ToString();
        }

        private static string SanitizeFileName(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "signature";
            }

            foreach (char invalidCharacter in
                     Path.GetInvalidFileNameChars())
            {
                value =
                    value.Replace(
                        invalidCharacter.ToString(),
                        string.Empty);
            }

            return string.IsNullOrWhiteSpace(value)
                ? "signature"
                : value;
        }

        private static string GenerateTemporaryPassword()
        {
            const string upper =
                "ABCDEFGHJKLMNPQRSTUVWXYZ";

            const string lower =
                "abcdefghijkmnopqrstuvwxyz";

            const string numbers =
                "23456789";

            const string special =
                "@#$%";

            string password =
                $"{GetRandomCharacter(upper)}" +
                $"{GetRandomCharacter(lower)}" +
                $"{GetRandomCharacter(numbers)}" +
                $"{GetRandomCharacter(special)}";

            const string all =
                upper + lower + numbers + special;

            for (int i = password.Length; i < 12; i++)
            {
                password +=
                    GetRandomCharacter(all);
            }

            return Shuffle(password);
        }

        private static char GetRandomCharacter(
            string characters)
        {
            int index =
                RandomNumberGenerator.GetInt32(
                    0,
                    characters.Length);

            return characters[index];
        }

        private static string Shuffle(
            string value)
        {
            char[] characters =
                value.ToCharArray();

            for (int i = characters.Length - 1; i > 0; i--)
            {
                int j =
                    RandomNumberGenerator.GetInt32(
                        0,
                        i + 1);

                (characters[i], characters[j]) =
                    (characters[j], characters[i]);
            }

            return new string(characters);
        }
    }
}