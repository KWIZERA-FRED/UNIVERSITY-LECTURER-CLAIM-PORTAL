using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.Contracts;

namespace Academic_Staff_Engagement_Claim_Processing_System.Data.Models
{
    public class ContractSignature
    {
        [Key]
        public int Id { get; set; }

        // ============================================================
        // CONTRACT
        // ============================================================

        [Required]
        [ForeignKey(nameof(Contract))]
        public int ContractId { get; set; }

        public Contract Contract { get; set; } = null!;

        // ============================================================
        // SIGNER
        // ============================================================

        [Required]
        public int SignerId { get; set; }

        [Required]
        [MaxLength(20)]
        public string SignerType { get; set; } = string.Empty;

        [Required]
        public ContractSignerRole SignerRole { get; set; }

        // ============================================================
        // SIGNING ORDER
        // ============================================================

        [Required]
        public int SequenceOrder { get; set; }

        // ============================================================
        // SIGNING STATUS
        // ============================================================

        [Required]
        public ContractSignatureStatus Status { get; set; }
            = ContractSignatureStatus.Pending;

        // ============================================================
        // SIGNATURE SNAPSHOT
        // ============================================================

        [MaxLength(256)]
        public string? SignatureHash { get; private set; }

        public DateTime? SignedAtUtc { get; private set; }

        // ============================================================
        // NOTIFICATION
        // ============================================================

        public bool NotificationSent { get; set; } = false;

        public DateTime? NotificationSentAtUtc { get; set; }

        // ============================================================
        // AUDIT
        // ============================================================

        public DateTime CreatedAtUtc { get; set; }
            = DateTime.UtcNow;

        public DateTime? UpdatedAtUtc { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }

        // ============================================================
        // SIGN
        // ============================================================

        public void Sign(string signatureHash)
        {
            if (Status == ContractSignatureStatus.Signed)
            {
                throw new InvalidOperationException(
                    "This signer has already signed the contract.");
            }

            if (string.IsNullOrWhiteSpace(signatureHash))
            {
                throw new ArgumentException(
                    "Signature hash is required.",
                    nameof(signatureHash));
            }

            SignatureHash = signatureHash;
            SignedAtUtc = DateTime.UtcNow;
            Status = ContractSignatureStatus.Signed;
            UpdatedAtUtc = DateTime.UtcNow;
        }
    }
}