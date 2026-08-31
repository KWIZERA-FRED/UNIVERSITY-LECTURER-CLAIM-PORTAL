using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;

namespace Academic_Staff_Engagement_Claim_Processing_System.Data.Models
{
    public class Contract
    {
        [Key]
        public int Id { get; private set; }

        [Required]
        [ForeignKey(nameof(Lecturer))]
        public int LecturerId { get; set; }
        public Lecturer Lecturer { get; set; } = null!;

        [ForeignKey(nameof(CourseAssignment))]
        public int? CourseAssignmentId { get; set; }
        public CourseAssignment? CourseAssignment { get; set; }

        [Required]
        public string Content { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string Version { get; set; } = string.Empty;

        [Required]
        public ContractStatus Status { get; set; } = ContractStatus.PendingSignature;

        // Snapshot of the lecturer's signature hash at the moment this
        // contract was generated — not a live link to Lecturer.SignatureFileHash,
        // so a later signature reissue can't silently change what an old
        // contract appears to have been signed with.
        public string SignatureHashAtSigning { get; private set; } = string.Empty;
        public DateTime? SignedAtUtc { get; set; }

        public DateTime? StartDateUtc { get; set; }
        public DateTime? EndDateUtc { get; set; }

        public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
        public DateTime? UpdatedAtUtc { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }

        public Contract(int id, string version)
        {
            Id = id;
            Version = version;
        }

        public void StampSignature(string signatureHash)
        {
            if (string.IsNullOrWhiteSpace(signatureHash))
                throw new ArgumentException("Signature hash is required.", nameof(signatureHash));

            SignatureHashAtSigning = signatureHash;
            SignedAtUtc = DateTime.UtcNow;
            Status = ContractStatus.Active;
            UpdatedAtUtc = DateTime.UtcNow;
        }
    }
}