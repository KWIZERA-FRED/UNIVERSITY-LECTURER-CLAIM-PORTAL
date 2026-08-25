using System;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;

namespace Academic_Staff_Engagement_Claim_Processing_System.Data.Models
{
    public class Lecturer
    {
        public int Id { get; private set; }
        public UserRole Type { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;

        public string PasswordHash { get; private set; } = string.Empty;
        public string GovernmentIdEncrypted { get; private set; } = string.Empty;

        public string SignatureHash { get; set; } = string.Empty;

        public LecturerRank? Rank { get; set; }

        public bool IsActive { get; set; } = true;

        public int FailedLoginAttempts { get; set; } = 0;
        public DateTime? LockoutEndUtc { get; set; }
        public DateTime? LastLoginUtc { get; set; }

        public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
        public DateTime? UpdatedAtUtc { get; set; }

        public byte[]? RowVersion { get; set; }

        public Lecturer(int id, string userName, string email)
        {
            Id = id;
            UserName = userName;
            Email = email;
        }

        public void SetPasswordHash(string newHash)
        {
            if (string.IsNullOrWhiteSpace(newHash))
                throw new ArgumentException("Password hash cannot be empty.", nameof(newHash));

            PasswordHash = newHash;
            UpdatedAtUtc = DateTime.UtcNow;
        }

        public void SetGovernmentIdEncrypted(string cipherText)
        {
            if (string.IsNullOrWhiteSpace(cipherText))
                throw new ArgumentException("Encrypted government ID cannot be empty.", nameof(cipherText));

            GovernmentIdEncrypted = cipherText;
            UpdatedAtUtc = DateTime.UtcNow;
        }
    }
}