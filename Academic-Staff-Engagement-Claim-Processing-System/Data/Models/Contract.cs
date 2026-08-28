using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;

namespace Academic_Staff_Engagement_Claim_Processing_System.Data.Models
{
    public class Contract
    {
        [Key]
        public int Id { get; private set; }

        // ============================================================
        // LECTURER
        // ============================================================

        [Required]
        [ForeignKey(nameof(Lecturer))]
        public int LecturerId { get; set; }

        public Lecturer Lecturer { get; set; } = null!;

        // ============================================================
        // COURSE ASSIGNMENT
        // ============================================================

        [ForeignKey(nameof(CourseAssignment))]
        public int? CourseAssignmentId { get; set; }

        public CourseAssignment? CourseAssignment { get; set; }

        // ============================================================
        // CONTRACT CONTENT
        // ============================================================

        [Required]
        public string Content { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string Version { get; set; } = string.Empty;

        // ============================================================
        // CONTRACT STATUS
        // ============================================================

        [Required]
        public ContractStatus Status { get; set; }
            = ContractStatus.PendingSignature;

        // ============================================================
        // CONTRACT DATES
        // ============================================================

        public DateTime? StartDateUtc { get; set; }

        public DateTime? EndDateUtc { get; set; }

        // ============================================================
        // AUDIT
        // ============================================================

        public DateTime CreatedAtUtc { get; private set; }
            = DateTime.UtcNow;

        public DateTime? UpdatedAtUtc { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }

        // ============================================================
        // SIGNERS
        // ============================================================

        public ICollection<ContractSignature> Signatures { get; set; }
            = new List<ContractSignature>();

        // ============================================================
        // CONSTRUCTOR
        // ============================================================

        public Contract(int id, string version)
        {
            Id = id;
            Version = version;
        }
    }
}