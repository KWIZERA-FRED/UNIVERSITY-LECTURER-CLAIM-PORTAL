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
        [MaxLength(30)]
        public string ClaimReference { get; private set; } = string.Empty;

        [Required]
        [ForeignKey(nameof(Lecturer))]
        public int LecturerId { get; set; }

        public Lecturer Lecturer { get; set; } = null!;

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

        public ClaimChecklist? Checklist { get; set; }

        [Range(0, 500)]
        public decimal HoursClaimed { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        [Range(0, 999999999)]
        public decimal Amount { get; set; }

        [MaxLength(2000)]
        public string? Description { get; set; }

        [MaxLength(2000)]
        public string? LecturerRemarks { get; set; }

        [MaxLength(2000)]
        public string? ReviewerRemarks { get; set; }

        [Required]
        public ClaimStatus Status { get; set; } = ClaimStatus.Draft;

        [Required]
        [MaxLength(64)]
        public string QrCodeToken { get; set; } = SecureToken.Create();

        public DateTime? SubmittedAtUtc { get; private set; }

        public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

        public DateTime? UpdatedAtUtc { get; set; }

        public DateTime? CompletedAtUtc { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }

        public ICollection<ClaimApproval> Approvals { get; set; }
            = new List<ClaimApproval>();

        public Claim(
            int id,
            string claimReference,
            int lecturerId,
            int courseAssignmentId,
            int contractId,
            decimal amount)
        {
            Id = id;
            ClaimReference = claimReference;
            LecturerId = lecturerId;
            CourseAssignmentId = courseAssignmentId;
            ContractId = contractId;
            Amount = amount;
        }

        // Replaces the QR / public-link token. Any link or printed QR code
        // carrying the old token stops working immediately.
        public void RegenerateQrToken()
        {
            QrCodeToken = SecureToken.Create();
            UpdatedAtUtc = DateTime.UtcNow;
        }

        public void Submit()
        {
            if (Status != ClaimStatus.Draft)
                throw new InvalidOperationException(
                    "Only draft claims can be submitted.");

            Status = ClaimStatus.PendingHODApproval;
            SubmittedAtUtc = DateTime.UtcNow;
            UpdatedAtUtc = DateTime.UtcNow;
        }

        public void UpdateAmount(decimal amount)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(amount),
                    "Claim amount cannot be negative.");

            Amount = amount;
            UpdatedAtUtc = DateTime.UtcNow;
        }

        public void SetLecturerRemarks(string? remarks)
        {
            LecturerRemarks = string.IsNullOrWhiteSpace(remarks)
                ? null
                : remarks.Trim();

            UpdatedAtUtc = DateTime.UtcNow;
        }

        public void SetReviewerRemarks(string? remarks)
        {
            ReviewerRemarks = string.IsNullOrWhiteSpace(remarks)
                ? null
                : remarks.Trim();

            UpdatedAtUtc = DateTime.UtcNow;
        }
    }
}