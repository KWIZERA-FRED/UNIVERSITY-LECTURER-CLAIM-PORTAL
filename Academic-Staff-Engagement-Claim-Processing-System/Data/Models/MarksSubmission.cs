using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Academic_Staff_Engagement_Claim_Processing_System.Data.Models
{
	public class MarksSubmission
	{
		[Key]
		public int Id { get; set; }

		[Required]
		[ForeignKey(nameof(Lecturer))]
		public int LecturerId { get; set; }

		public Lecturer Lecturer { get; set; } = null!;

		[Required]
		[ForeignKey(nameof(CourseAssignment))]
		public int CourseAssignmentId { get; set; }

		public CourseAssignment CourseAssignment { get; set; } = null!;

		[Required]
		[MaxLength(20)]
		public string AcademicYear { get; set; } = string.Empty;

		[Required]
		[MaxLength(255)]
		public string OriginalFileName { get; set; } = string.Empty;

		[Required]
		public string StoredFilePath { get; set; } = string.Empty;

		public long FileSize { get; set; }

		[Required]
		[MaxLength(50)]
		public string SubmissionReference { get; set; } = string.Empty;

		[Required]
		[MaxLength(50)]
		public string Status { get; set; } = "PendingExamOffice";

		public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;

		public DateTime? SignedAtUtc { get; set; }

		public string? SignedBy { get; set; }

		public DateTime? ReviewedAtUtc { get; set; }

		public string? RejectionReason { get; set; }

		public DateTime? UpdatedAtUtc { get; set; }

		[Timestamp]
		public byte[]? RowVersion { get; set; }
	}
}