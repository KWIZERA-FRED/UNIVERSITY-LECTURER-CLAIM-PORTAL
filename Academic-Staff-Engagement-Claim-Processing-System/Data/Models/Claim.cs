using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;

namespace Academic_Staff_Engagement_Claim_Processing_System.Data.Models
{
    public class Claim
    {
        [Key]
        public int Id { get; private set; }
    [Required]
        [ForeignKey(nameof(CourseAssignment))]
        public int CourseAssignmentId { get; set; }

        public CourseAssignment CourseAssignment { get; set; } = null!;

        [Required]
        [ForeignKey(nameof(Contract))]
        public int ContractId { get; set; }

        public Contract Contract { get; set; } = null!;

        [ForeignKey(nameof(MarksSubmission))]
        public int? MarksSubmissionId { get; set; }

        public MarksSubmission? MarksSubmission { get; set; }

        public ClaimAttendance? Attendance { get; set; }

        [Range(0, 500)]
        public decimal HoursClaimed { get; set; }

        public string Description { get; set; } = string.Empty;

        [Required]
        public ClaimStatus Status { get; set; } = ClaimStatus.Draft;

        [Required]
        [MaxLength(64)]
        public string QrCodeToken { get; set; } = Guid.NewGuid().ToString("N");

        public DateTime? SubmittedAtUtc { get; set; }

        public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

        public DateTime? UpdatedAtUtc { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }

        public ICollection<ClaimApproval> Approvals { get; set; } =
            new List<ClaimApproval>();

        public Claim(
            int id,
            int courseAssignmentId,
            int contractId)
        {
            Id = id;
            CourseAssignmentId = courseAssignmentId;
            ContractId = contractId;
        }
    }

}
