using Academic_Staff_Engagement_Claim_Processing_System.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Cryptography;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages.HOD
{
    public class RegisterUserModel : PageModel
    {
        private readonly EmailService _emailService;

        public RegisterUserModel(EmailService emailService)
        {
            _emailService = emailService;
        }

        [BindProperty]
        public string Name { get; set; } = string.Empty;

        [BindProperty]
        public string Email { get; set; } = string.Empty;

        [BindProperty]
        public string Rank { get; set; } = string.Empty;

        [BindProperty]
        public string Role { get; set; } = string.Empty;

        public string? SuccessMessage { get; set; }
        public string? ErrorMessage { get; set; }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrWhiteSpace(Name) ||
                string.IsNullOrWhiteSpace(Email) ||
                string.IsNullOrWhiteSpace(Rank) ||
                string.IsNullOrWhiteSpace(Role))
            {
                ErrorMessage = "Please complete all required fields.";
                return Page();
            }

            // Username is the user's name
            string username = Name.Trim();

            // Generate a secure random password
            string password = GeneratePassword();

            try
            {
                // Send login credentials to the registered email
                await _emailService.SendWelcomeEmailAsync(
                    Email.Trim(),
                    Name.Trim(),
                    username,
                    password);

                SuccessMessage =
                    $"User {Name} was registered successfully. " +
                    $"The login credentials have been sent to {Email}.";

                // Clear form after successful registration
                Name = string.Empty;
                Email = string.Empty;
                Rank = string.Empty;
                Role = string.Empty;

                return Page();
            }
            catch (Exception ex)
            {
                ErrorMessage =
                    "The user could not be registered because the email could not be sent. " +
                    "Please check the email configuration.";

                // Useful while testing locally
                Console.WriteLine($"EMAIL ERROR: {ex.Message}");

                return Page();
            }
        }

        private static string GeneratePassword()
        {
            const string uppercase = "ABCDEFGHJKLMNPQRSTUVWXYZ";
            const string lowercase = "abcdefghijkmnopqrstuvwxyz";
            const string numbers = "23456789";
            const string symbols = "@#$%";

            string allCharacters =
                uppercase + lowercase + numbers + symbols;

            var password = new char[10];

            password[0] = uppercase[
                RandomNumberGenerator.GetInt32(uppercase.Length)];

            password[1] = lowercase[
                RandomNumberGenerator.GetInt32(lowercase.Length)];

            password[2] = numbers[
                RandomNumberGenerator.GetInt32(numbers.Length)];

            password[3] = symbols[
                RandomNumberGenerator.GetInt32(symbols.Length)];

            for (int i = 4; i < password.Length; i++)
            {
                password[i] = allCharacters[
                    RandomNumberGenerator.GetInt32(allCharacters.Length)];
            }

            // Shuffle password
            for (int i = password.Length - 1; i > 0; i--)
            {
                int j = RandomNumberGenerator.GetInt32(i + 1);

                (password[i], password[j]) =
                    (password[j], password[i]);
            }

            return new string(password);
        }
    }
}