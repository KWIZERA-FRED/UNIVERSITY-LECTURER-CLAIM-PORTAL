
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;

namespace Academic_Staff_Engagement_Claim_Processing_System.Data.Models
{
    public class ContractSignature
    {
        [Key]
        public int Id { get; private set; }

        [Required]
        [ForeignKey(nameof(Contract))]
        public int ContractId { get; set; }

        // IMPORTANT:
        // The actual contract entity in this project is Contract,
        // not ContractModel.
        public Contract Contract { get; set; } = null!;

        [Required]
        public int SequenceOrder { get; set; }

        [Required]
        public SignerRole SignerRole { get; set; }

        [ForeignKey(nameof(SignedByLecturer))]
        public int? SignedByLecturerId { get; set; }

        public Lecturer? SignedByLecturer { get; set; }

        [ForeignKey(nameof(SignedByAdminAccount))]
        public int? SignedByAdminAccountId { get; set; }

        public AdminAccount? SignedByAdminAccount { get; set; }

        [Required]
        public SignatureDecision Decision { get; set; } =
            SignatureDecision.Pending;

        public string? SignatureFilePath { get; private set; }

        public string? SignatureHash { get; private set; }

        public string? Comments { get; set; }

        public DateTime? SignedAtUtc { get; set; }

        public DateTime CreatedAtUtc { get; private set; } =
            DateTime.UtcNow;

        public ContractSignature(
            int id,
            int contractId,
            int sequenceOrder,
            SignerRole signerRole)
        {
            Id = id;
            ContractId = contractId;
            SequenceOrder = sequenceOrder;
            SignerRole = signerRole;
        }

        public void SignAsLecturer(
            int lecturerId,
            string signatureFilePath,
            string signatureHash)
        {
            if (SignerRole != SignerRole.Lecturer)
            {
                throw new InvalidOperationException(
                    "This signature step does not belong to the Lecturer role.");
            }

            if (string.IsNullOrWhiteSpace(signatureFilePath))
            {
                throw new ArgumentException(
                    "Signature file path is required.",
                    nameof(signatureFilePath));
            }

            if (string.IsNullOrWhiteSpace(signatureHash))
            {
                throw new ArgumentException(
                    "Signature hash is required.",
                    nameof(signatureHash));
            }

            SignedByLecturerId = lecturerId;
            SignatureFilePath = signatureFilePath;
            SignatureHash = signatureHash;
            Decision = SignatureDecision.Signed;
            SignedAtUtc = DateTime.UtcNow;
        }

        public void SignAsAdmin(
            int adminAccountId,
            string signatureFilePath,
            string signatureHash)
        {
            if (SignerRole == SignerRole.Lecturer)
            {
                throw new InvalidOperationException(
                    "This signature step belongs to the Lecturer role, not an admin.");
            }

            if (string.IsNullOrWhiteSpace(signatureFilePath))
            {
                throw new ArgumentException(
                    "Signature file path is required.",
                    nameof(signatureFilePath));
            }

            if (string.IsNullOrWhiteSpace(signatureHash))
            {
                throw new ArgumentException(
                    "Signature hash is required.",
                    nameof(signatureHash));
            }

            SignedByAdminAccountId = adminAccountId;
            SignatureFilePath = signatureFilePath;
            SignatureHash = signatureHash;
            Decision = SignatureDecision.Signed;
            SignedAtUtc = DateTime.UtcNow;
        }

        public void Decline(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException(
                    "A decline reason is required.",
                    nameof(reason));
            }

            Decision = SignatureDecision.Declined;
            Comments = reason.Trim();
            SignedAtUtc = DateTime.UtcNow;
        }
    }
}
