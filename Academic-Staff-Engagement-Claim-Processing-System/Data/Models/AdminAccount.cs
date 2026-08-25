using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;

namespace Academic_Staff_Engagement_Claim_Processing_System.Data.Models
{
    public abstract class AdminAccount
    {
        [Key]
        public int Id { get; protected set; }

        [Required]
        [MaxLength(100)]
        public string UserName { get; set; } = string.Empty;

        [Required]
        [MaxLength(150)]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; protected set; } = string.Empty;

        [Required]
        public abstract ApprovalRole Role { get; }

        public string? SignatureFilePath { get; protected set; }
        public string? SignatureFileHash { get; protected set; }
        public SignatureStatus SignatureStatus { get; protected set; } = SignatureStatus.NotCaptured;
        public DateTime? SignatureCapturedAtUtc { get; protected set; }

        public bool IsActive { get; set; } = true;

        public int FailedLoginAttempts { get; set; } = 0;
        public DateTime? LockoutEndUtc { get; set; }
        public DateTime? LastLoginUtc { get; set; }

        public DateTime CreatedAtUtc { get; protected set; } = DateTime.UtcNow;
        public DateTime? UpdatedAtUtc { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }

        protected AdminAccount(int id, string userName, string email)
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

        public void CaptureSignature(string filePath, string fileHash)
        {
            if (SignatureStatus != SignatureStatus.NotCaptured)
                throw new InvalidOperationException("A signature has already been captured for this account.");

            if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(fileHash))
                throw new ArgumentException("Signature file path and hash are required.");

            SignatureFilePath = filePath;
            SignatureFileHash = fileHash;
            SignatureCapturedAtUtc = DateTime.UtcNow;
            SignatureStatus = SignatureStatus.Verified;
            UpdatedAtUtc = DateTime.UtcNow;
        }
    }
}