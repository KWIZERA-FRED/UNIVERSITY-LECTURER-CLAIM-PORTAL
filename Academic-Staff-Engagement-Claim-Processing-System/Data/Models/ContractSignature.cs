using System;
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
        public Contract Contract { get; set; } = null!;

        // Enforces required order: the Lecturer signs before any admin
        // counter-signature (HOD/Dean/Management) — same pattern as
        // ClaimApproval.SequenceOrder.
        [Required]
        public int SequenceOrder { get; set; }

        [Required]
        public SignerRole SignerRole { get; set; }

        // Exactly one of these two is ever set, never both — a contract's
        // first signer is always the Lecturer being contracted; any later
        // step is one of the admin roles. Two strongly-typed FKs (rather
        // than one polymorphic one) let EF enforce real referential
        // integrity on whichever one actually applies to this step.
        [ForeignKey(nameof(SignedByLecturer))]
        public int? SignedByLecturerId { get; set; }
        public Lecturer? SignedByLecturer { get; set; }

        [ForeignKey(nameof(SignedByAdminAccount))]
        public int? SignedByAdminAccountId { get; set; }
        public AdminAccount? SignedByAdminAccount { get; set; }

        [Required]
        public SignatureDecision Decision { get; set; } = SignatureDecision.Pending;

        // Snapshot, not a live link — same reasoning as
        // ClaimApproval.SignatureHashAtApproval and
        // Contract.SignatureHashAtSigning: proves exactly which signature
        // image was in effect at this moment, even if the signer's
        // signature is later reissued.
        public string? SignatureHash { get; private set; }

        public string? Comments { get; set; }

        public DateTime? SignedAtUtc { get; set; }

        public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

        public ContractSignature(int id, int contractId, int sequenceOrder, SignerRole signerRole)
        {
            Id = id;
            ContractId = contractId;
            SequenceOrder = sequenceOrder;
            SignerRole = signerRole;
        }

        public void SignAsLecturer(int lecturerId, string signatureHash)
        {
            if (SignerRole != SignerRole.Lecturer)
                throw new InvalidOperationException("This signature step does not belong to the Lecturer role.");

            if (string.IsNullOrWhiteSpace(signatureHash))
                throw new ArgumentException("Signature hash is required to sign.", nameof(signatureHash));

            SignedByLecturerId = lecturerId;
            SignatureHash = signatureHash;
            Decision = SignatureDecision.Signed;
            SignedAtUtc = DateTime.UtcNow;
        }

        public void SignAsAdmin(int adminAccountId, string signatureHash)
        {
            if (SignerRole == SignerRole.Lecturer)
                throw new InvalidOperationException("This signature step belongs to the Lecturer role, not an admin.");

            if (string.IsNullOrWhiteSpace(signatureHash))
                throw new ArgumentException("Signature hash is required to sign.", nameof(signatureHash));

            SignedByAdminAccountId = adminAccountId;
            SignatureHash = signatureHash;
            Decision = SignatureDecision.Signed;
            SignedAtUtc = DateTime.UtcNow;
        }

        public void Decline(string reason)
        {
            Decision = SignatureDecision.Declined;
            Comments = reason;
            SignedAtUtc = DateTime.UtcNow;
        }
    }
}