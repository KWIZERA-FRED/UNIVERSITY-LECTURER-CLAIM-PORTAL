using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.Contracts;

namespace Academic_Staff_Engagement_Claim_Processing_System.Data.Models
{
    public class Lecturer
    {
        [Key]
        public int Id { get; private set; }

        [Required]
        public UserRole Type { get; set; } = UserRole.PartTimeLecturer;

        [Required]
        [MaxLength(100)]
        public string UserName { get; set; } = string.Empty;

        [Required]
        [MaxLength(150)]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [MaxLength(20)]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; private set; } = string.Empty;

        public bool MustChangePassword { get; set; } = true;

        public string GovernmentIdEncrypted { get; private set; } = string.Empty;

        public LecturerRank? Rank { get; set; }

        // Signature
        public string? SignatureFilePath { get; private set; }

        public string? SignatureFileHash { get; private set; }

        public SignatureStatus SignatureStatus { get; set; }
            = SignatureStatus.NotCaptured;

        public DateTime? SignatureCapturedAtUtc { get; set; }

        [ForeignKey(nameof(SignatureCapturedByHod))]
        public int? SignatureCapturedByHodId { get; set; }

        public Hod? SignatureCapturedByHod { get; set; }

        // Account status
        public bool IsActive { get; set; } = true;

        public int FailedLoginAttempts { get; set; } = 0;

        public DateTime? LockoutEndUtc { get; set; }

        public DateTime? LastLoginUtc { get; set; }

        // Audit fields
        public DateTime CreatedAtUtc { get; private set; }
            = DateTime.UtcNow;

        public DateTime? UpdatedAtUtc { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }

        // Relationships
        public ICollection<CourseAssignment> CourseAssignments { get; set; }
            = new List<CourseAssignment>();

        public ICollection<Contract> Contracts { get; set; }
            = new List<Contract>();

        public Lecturer(int id, string userName, string email)
        {
            Id = id;
            UserName = userName;
            Email = email;
        }

        public void SetPasswordHash(string newHash)
        {
            if (string.IsNullOrWhiteSpace(newHash))
                throw new ArgumentException(
                    "Password hash cannot be empty.",
                    nameof(newHash));

            PasswordHash = newHash;
            UpdatedAtUtc = DateTime.UtcNow;
        }

        public void SetGovernmentIdEncrypted(string cipherText)
        {
            if (string.IsNullOrWhiteSpace(cipherText))
                throw new ArgumentException(
                    "Encrypted government ID cannot be empty.",
                    nameof(cipherText));

            GovernmentIdEncrypted = cipherText;
            UpdatedAtUtc = DateTime.UtcNow;
        }

        public void CaptureSignature(
            string filePath,
            string fileHash,
            int capturedByHodId)
        {
            if (SignatureStatus != SignatureStatus.NotCaptured)
                throw new InvalidOperationException(
                    "A signature has already been captured for this lecturer.");

            if (string.IsNullOrWhiteSpace(filePath) ||
                string.IsNullOrWhiteSpace(fileHash))
            {
                throw new ArgumentException(
                    "Signature file path and hash are required.");
            }

            SignatureFilePath = filePath;
            SignatureFileHash = fileHash;
            SignatureCapturedByHodId = capturedByHodId;
            SignatureCapturedAtUtc = DateTime.UtcNow;
            SignatureStatus = SignatureStatus.Verified;
            UpdatedAtUtc = DateTime.UtcNow;
        }
    }
}