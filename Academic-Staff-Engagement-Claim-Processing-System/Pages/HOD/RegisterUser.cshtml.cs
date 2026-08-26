using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Academic_Staff_Engagement_Claim_Processing_System.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

using LecturerModel =
    Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Lecturer;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages.HOD
{
    public class RegisterUserModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly EmailService _emailService;
        private readonly IWebHostEnvironment _environment;

        public RegisterUserModel(
            ApplicationDbContext context,
            EmailService emailService,
            IWebHostEnvironment environment)
        {
            _context = context;
            _emailService = emailService;
            _environment = environment;
        }

        // ============================================================
        // FORM FIELDS
        // ============================================================

        [BindProperty]
        public string Name { get; set; } = string.Empty;

        [BindProperty]
        public string Email { get; set; } = string.Empty;

        [BindProperty]
        public string Department { get; set; } = string.Empty;

        [BindProperty]
        public string Rank { get; set; } = string.Empty;

        [BindProperty]
        public string Role { get; set; } = string.Empty;

        [BindProperty]
        public string GovernmentId { get; set; } = string.Empty;

        [BindProperty]
        public string SignatureData { get; set; } = string.Empty;

        // ============================================================
        // MESSAGES
        // ============================================================

        public string? SuccessMessage { get; set; }

        public string? ErrorMessage { get; set; }

        // ============================================================
        // GET
        // ============================================================

        public void OnGet()
        {
        }

        // ============================================================
        // POST
        // ============================================================

        bool anyHodExists = await _context.Hods.AnyAsync();

        if (anyHodExists && !User.IsInRole("HOD"))
        {
            return Forbid();
        }


        public async Task<IActionResult> OnPostAsync()
        {
            // --------------------------------------------------------
            // BASIC VALIDATION
            // --------------------------------------------------------

            if (string.IsNullOrWhiteSpace(Name))
            {
                ErrorMessage = "Please enter the user's full name.";
                return Page();
            }

            if (string.IsNullOrWhiteSpace(Email))
            {
                ErrorMessage = "Please enter the user's email address.";
                return Page();
            }

            if (string.IsNullOrWhiteSpace(Role))
            {
                ErrorMessage = "Please select the user's system role.";
                return Page();
            }

            if (string.IsNullOrWhiteSpace(SignatureData))
            {
                ErrorMessage = "Please provide the user's digital signature.";
                return Page();
            }

            // --------------------------------------------------------
            // CLEAN INPUT
            // --------------------------------------------------------

            string name = Name.Trim();
            string email = Email.Trim().ToLowerInvariant();
            string role = Role.Trim();

            string rank = Rank?.Trim() ?? string.Empty;
            string department = Department?.Trim() ?? string.Empty;
            string governmentId = GovernmentId?.Trim() ?? string.Empty;

            // --------------------------------------------------------
            // VALIDATE ROLE
            // --------------------------------------------------------

            if (!role.Equals("Lecturer", StringComparison.OrdinalIgnoreCase) &&
                !role.Equals("HOD", StringComparison.OrdinalIgnoreCase) &&
                !role.Equals("Dean", StringComparison.OrdinalIgnoreCase) &&
                !role.Equals("Management", StringComparison.OrdinalIgnoreCase))
            {
                ErrorMessage =
                    "Invalid system role selected. Please select Lecturer, HOD, Dean, or Management.";

                return Page();
            }

            // --------------------------------------------------------
            // LECTURER VALIDATION
            // --------------------------------------------------------

            if (role.Equals("Lecturer", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(governmentId))
                {
                    ErrorMessage =
                        "Please enter the lecturer's Government ID.";

                    return Page();
                }

                if (string.IsNullOrWhiteSpace(rank))
                {
                    ErrorMessage =
                        "Please select the lecturer's academic rank.";

                    return Page();
                }

                if (!Enum.TryParse<LecturerRank>(
                        rank.Replace(" ", ""),
                        true,
                        out _))
                {
                    ErrorMessage =
                        "The selected lecturer rank is invalid.";

                    return Page();
                }
            }

            // --------------------------------------------------------
            // HOD VALIDATION
            // --------------------------------------------------------

            if (role.Equals("HOD", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(department))
                {
                    ErrorMessage =
                        "Please enter the HOD's department.";

                    return Page();
                }
            }

            // --------------------------------------------------------
            // USERNAME
            // --------------------------------------------------------

            string username = name;

            // --------------------------------------------------------
            // DUPLICATE USERNAME
            // --------------------------------------------------------

            bool adminUsernameExists =
                await _context.AdminAccounts
                    .AnyAsync(a => a.UserName == username);

            bool lecturerUsernameExists =
                await _context.Lecturers
                    .AnyAsync(l => l.UserName == username);

            if (adminUsernameExists || lecturerUsernameExists)
            {
                ErrorMessage =
                    $"The username '{username}' already exists. Please use a different name.";

                return Page();
            }

            // --------------------------------------------------------
            // DUPLICATE EMAIL
            // --------------------------------------------------------

            bool adminEmailExists =
                await _context.AdminAccounts
                    .AnyAsync(a => a.Email == email);

            bool lecturerEmailExists =
                await _context.Lecturers
                    .AnyAsync(l => l.Email == email);

            if (adminEmailExists || lecturerEmailExists)
            {
                ErrorMessage =
                    $"An account with the email address '{email}' already exists.";

                return Page();
            }

            // --------------------------------------------------------
            // GENERATE PASSWORD
            // --------------------------------------------------------

            string password = GeneratePassword();

            // --------------------------------------------------------
            // SAVE SIGNATURE
            // --------------------------------------------------------

            string signatureFilePath;

            try
            {
                signatureFilePath =
                    await SaveSignatureAsync(
                        SignatureData,
                        username);
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"SIGNATURE ERROR: {ex}");

                ErrorMessage =
                    "The user's signature could not be saved.";

                return Page();
            }

            // --------------------------------------------------------
            // CALCULATE SIGNATURE HASH
            // --------------------------------------------------------

            string signatureAbsolutePath =
                Path.Combine(
                    _environment.WebRootPath,
                    signatureFilePath
                        .TrimStart('/')
                        .Replace(
                            '/',
                            Path.DirectorySeparatorChar));

            string signatureHash;

            try
            {
                signatureHash =
                    await CalculateFileHashAsync(
                        signatureAbsolutePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"SIGNATURE HASH ERROR: {ex}");

                ErrorMessage =
                    "The user's signature could not be verified.";

                return Page();
            }

            // ========================================================
            // CREATE ACCOUNT
            // ========================================================

            try
            {
                // ====================================================
                // LECTURER
                // ====================================================

                if (role.Equals(
                        "Lecturer",
                        StringComparison.OrdinalIgnoreCase))
                {
                    if (!Enum.TryParse<LecturerRank>(
                            rank.Replace(" ", ""),
                            true,
                            out LecturerRank lecturerRank))
                    {
                        ErrorMessage =
                            "The lecturer rank is invalid.";

                        return Page();
                    }

                    // ------------------------------------------------
                    // FIND REGISTERING HOD
                    //
                    // Current prototype uses the HOD account
                    // "Malaz" as the registering HOD.
                    // ------------------------------------------------

                    var registeringHod =
                        await _context.Hods
                            .FirstOrDefaultAsync(
                                h => h.UserName == "Malaz");

                    if (registeringHod == null)
                    {
                        ErrorMessage =
                            "The registering HOD account 'Malaz' could not be found in the database.";

                        return Page();
                    }

                    // ------------------------------------------------
                    // CREATE LECTURER
                    // ------------------------------------------------

                    var lecturer =
                        new LecturerModel(
                            0,
                            username,
                            email);

                    lecturer.Rank =
                        lecturerRank;

                    // New lecturer accounts are part-time
                    lecturer.Type =
                        UserRole.PartTimeLecturer;

                    // ------------------------------------------------
                    // PASSWORD HASH
                    // ------------------------------------------------

                    var lecturerPasswordHasher =
                        new PasswordHasher<LecturerModel>();

                    string lecturerPasswordHash =
                        lecturerPasswordHasher.HashPassword(
                            lecturer,
                            password);

                    lecturer.SetPasswordHash(
                        lecturerPasswordHash);

                    // ------------------------------------------------
                    // GOVERNMENT ID
                    //
                    // For the current prototype, we store the
                    // Government ID directly in the existing
                    // GovernmentIdEncrypted column.
                    //
                    // No database migration is required.
                    // ------------------------------------------------

                    lecturer.SetGovernmentIdEncrypted(
                        governmentId);

                    // ------------------------------------------------
                    // SIGNATURE
                    // ------------------------------------------------

                    lecturer.CaptureSignature(
                        signatureFilePath,
                        signatureHash,
                        registeringHod.Id);

                    // ------------------------------------------------
                    // SAVE
                    // ------------------------------------------------

                    _context.Lecturers.Add(
                        lecturer);

                    await _context.SaveChangesAsync();

                    // ------------------------------------------------
                    // SEND EMAIL
                    // ------------------------------------------------

                    await SendWelcomeEmailAndReturnResult(
                        email,
                        name,
                        username,
                        password,
                        "Lecturer");

                    return Page();
                }

                // ====================================================
                // HOD
                // ====================================================

                if (role.Equals(
                        "HOD",
                        StringComparison.OrdinalIgnoreCase))
                {
                    var hod =
                        new Hod(
                            0,
                            username,
                            email,
                            department);

                    var hodPasswordHasher =
                        new PasswordHasher<AdminAccount>();

                    string hodPasswordHash =
                        hodPasswordHasher.HashPassword(
                            hod,
                            password);

                    hod.SetPasswordHash(
                        hodPasswordHash);

                    hod.CaptureSignature(
                        signatureFilePath,
                        signatureHash);

                    _context.Hods.Add(
                        hod);

                    await _context.SaveChangesAsync();

                    await SendWelcomeEmailAndReturnResult(
                        email,
                        name,
                        username,
                        password,
                        "HOD");

                    return Page();
                }

                // ====================================================
                // DEAN
                // ====================================================

                if (role.Equals(
                        "Dean",
                        StringComparison.OrdinalIgnoreCase))
                {
                    var dean =
                        new Dean(
                            0,
                            username,
                            email);

                    var deanPasswordHasher =
                        new PasswordHasher<AdminAccount>();

                    string deanPasswordHash =
                        deanPasswordHasher.HashPassword(
                            dean,
                            password);

                    dean.SetPasswordHash(
                        deanPasswordHash);

                    dean.CaptureSignature(
                        signatureFilePath,
                        signatureHash);

                    _context.Deans.Add(
                        dean);

                    await _context.SaveChangesAsync();

                    await SendWelcomeEmailAndReturnResult(
                        email,
                        name,
                        username,
                        password,
                        "Dean");

                    return Page();
                }

                // ====================================================
                // MANAGEMENT
                // ====================================================

                if (role.Equals(
                        "Management",
                        StringComparison.OrdinalIgnoreCase))
                {
                    var management =
                        new Management(
                            0,
                            username,
                            email);

                    var managementPasswordHasher =
                        new PasswordHasher<AdminAccount>();

                    string managementPasswordHash =
                        managementPasswordHasher.HashPassword(
                            management,
                            password);

                    management.SetPasswordHash(
                        managementPasswordHash);

                    management.CaptureSignature(
                        signatureFilePath,
                        signatureHash);

                    _context.ManagementAccounts.Add(
                        management);

                    await _context.SaveChangesAsync();

                    await SendWelcomeEmailAndReturnResult(
                        email,
                        name,
                        username,
                        password,
                        "Management");

                    return Page();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"DATABASE ERROR: {ex}");

                ErrorMessage =
                    "The user account could not be saved to the database.";

                return Page();
            }

            ErrorMessage =
                "The user account could not be created.";

            return Page();
        }

        // ============================================================
        // SEND WELCOME EMAIL
        // ============================================================

        private async Task SendWelcomeEmailAndReturnResult(
            string email,
            string name,
            string username,
            string password,
            string accountType)
        {
            try
            {
                await _emailService.SendWelcomeEmailAsync(
                    email,
                    name,
                    username,
                    password);

                SuccessMessage =
                    $"{accountType} account for {name} was registered successfully. " +
                    $"The login credentials have been sent to {email}.";
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"EMAIL ERROR: {ex}");

                SuccessMessage =
                    $"{accountType} account for {name} was created successfully, " +
                    $"but the welcome email could not be sent. " +
                    $"Username: {username}";
            }

            ClearForm();
        }

        // ============================================================
        // SAVE SIGNATURE
        // ============================================================

        private async Task<string> SaveSignatureAsync(
            string signatureData,
            string username)
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

            string base64Data =
                signatureData.Substring(
                    prefix.Length);

            byte[] imageBytes;

            try
            {
                imageBytes =
                    Convert.FromBase64String(
                        base64Data);
            }
            catch
            {
                throw new InvalidOperationException(
                    "The signature image is invalid.");
            }

            if (imageBytes.Length == 0)
            {
                throw new InvalidOperationException(
                    "The signature image is empty.");
            }

            // --------------------------------------------------------
            // DIRECTORY
            // --------------------------------------------------------

            string signaturesDirectory =
                Path.Combine(
                    _environment.WebRootPath,
                    "uploads",
                    "signatures");

            Directory.CreateDirectory(
                signaturesDirectory);

            // --------------------------------------------------------
            // SAFE USERNAME
            // --------------------------------------------------------

            string safeUsername =
                string.Concat(
                    username.Where(
                        c =>
                            char.IsLetterOrDigit(c) ||
                            c == '_' ||
                            c == '-'));

            if (string.IsNullOrWhiteSpace(
                    safeUsername))
            {
                safeUsername = "user";
            }

            // --------------------------------------------------------
            // UNIQUE FILE NAME
            // --------------------------------------------------------

            string fileName =
                $"{safeUsername}_{Guid.NewGuid():N}.png";

            string absolutePath =
                Path.Combine(
                    signaturesDirectory,
                    fileName);

            // --------------------------------------------------------
            // WRITE FILE
            // --------------------------------------------------------

            await System.IO.File.WriteAllBytesAsync(
                absolutePath,
                imageBytes);

            // --------------------------------------------------------
            // DATABASE PATH
            // --------------------------------------------------------

            return $"/uploads/signatures/{fileName}";
        }

        // ============================================================
        // HASH SIGNATURE
        // ============================================================

        private static async Task<string>
            CalculateFileHashAsync(
                string filePath)
        {
            await using var stream =
                new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read);

            using var sha256 =
                SHA256.Create();

            byte[] hash =
                await sha256.ComputeHashAsync(
                    stream);

            return Convert.ToHexString(
                hash);
        }

        // ============================================================
        // PASSWORD GENERATOR
        // ============================================================

        private static string GeneratePassword()
        {
            const string uppercase =
                "ABCDEFGHJKLMNPQRSTUVWXYZ";

            const string lowercase =
                "abcdefghijkmnopqrstuvwxyz";

            const string numbers =
                "23456789";

            const string symbols =
                "@#$%";

            string allCharacters =
                uppercase +
                lowercase +
                numbers +
                symbols;

            var password =
                new char[10];

            // Guarantee uppercase
            password[0] =
                uppercase[
                    RandomNumberGenerator.GetInt32(
                        uppercase.Length)];

            // Guarantee lowercase
            password[1] =
                lowercase[
                    RandomNumberGenerator.GetInt32(
                        lowercase.Length)];

            // Guarantee number
            password[2] =
                numbers[
                    RandomNumberGenerator.GetInt32(
                        numbers.Length)];

            // Guarantee symbol
            password[3] =
                symbols[
                    RandomNumberGenerator.GetInt32(
                        symbols.Length)];

            // Remaining characters
            for (int i = 4;
                 i < password.Length;
                 i++)
            {
                password[i] =
                    allCharacters[
                        RandomNumberGenerator.GetInt32(
                            allCharacters.Length)];
            }

            // Shuffle
            for (int i = password.Length - 1;
                 i > 0;
                 i--)
            {
                int j =
                    RandomNumberGenerator.GetInt32(
                        i + 1);

                (password[i], password[j]) =
                    (password[j], password[i]);
            }

            return new string(password);
        }

        // ============================================================
        // CLEAR FORM
        // ============================================================

        private void ClearForm()
        {
            Name = string.Empty;
            Email = string.Empty;
            Department = string.Empty;
            Rank = string.Empty;
            Role = string.Empty;
            GovernmentId = string.Empty;
            SignatureData = string.Empty;
        }
    }
}