using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;

namespace Academic_Staff_Engagement_Claim_Processing_System.Data.Models
{
    public class ClaimApproval
    {
        [Key]
        public int Id { get; private set; }

        [Required]
        [ForeignKey(nameof(Claim))]
        public int ClaimId { get; set; }
        public Claim Claim { get; set; } = null!;

        // Enforces the required order: HOD must approve before Dean,
        // Dean before Management.
        [Required]
        public int SequenceOrder { get; set; }

        [Required]
        public ApprovalRole ApprovalRole { get; set; }

        // Points at whichever AdminAccount (Hod/Dean/Management) actually
        // approved — requires AdminAccount to be mapped as TPH (one shared
        // table) so a single FK can reference any of the three subtypes.
        [ForeignKey(nameof(ApprovedByAdminAccount))]
        public int? ApprovedByAdminAccountId { get; set; }
        public AdminAccount? ApprovedByAdminAccount { get; set; }

        [Required]
        public ApprovalDecision Decision { get; set; } = ApprovalDecision.Pending;

        // Snapshot, not a live link — proves exactly which signature was in
        // effect for this approval, even if the approver's signature is
        // later reissued.
        public string? SignatureHashAtApproval { get; private set; }

        public string? Comments { get; set; }

        public DateTime? DecidedAtUtc { get; set; }

        public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

        public ClaimApproval(int id, int claimId, int sequenceOrder, ApprovalRole approvalRole)
        {
            Id = id;
            ClaimId = claimId;
            SequenceOrder = sequenceOrder;
            ApprovalRole = approvalRole;
        }

        public void Approve(int approvedByAdminAccountId, string signatureHash)
        {
            if (string.IsNullOrWhiteSpace(signatureHash))
                throw new ArgumentException("Signature hash is required to approve.", nameof(signatureHash));

            ApprovedByAdminAccountId = approvedByAdminAccountId;
            SignatureHashAtApproval = signatureHash;
            Decision = ApprovalDecision.Approved;
            DecidedAtUtc = DateTime.UtcNow;
        }

        public void Reject(int rejectedByAdminAccountId, string reason)
        {
            ApprovedByAdminAccountId = rejectedByAdminAccountId;
            Decision = ApprovalDecision.Rejected;
            Comments = reason;
            DecidedAtUtc = DateTime.UtcNow;
        }
    }
}