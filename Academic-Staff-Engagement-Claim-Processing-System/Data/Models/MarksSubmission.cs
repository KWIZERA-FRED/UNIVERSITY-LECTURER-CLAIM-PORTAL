using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;

namespace Academic_Staff_Engagement_Claim_Processing_System.Data.Models
{
    public class MarksSubmission
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string SubmissionReference { get; set; } = string.Empty;

        [Required]
        [ForeignKey(nameof(Lecturer))]
        public int LecturerId { get; set; }

        public Lecturer Lecturer { get; set; } = null!;

        [Required]
        [ForeignKey(nameof(CourseAssignment))]
        public int CourseAssignmentId { get; set; }

        public CourseAssignment CourseAssignment { get; set; } = null!;

        [Required]
        [ForeignKey(nameof(Course))]
        public int CourseId { get; set; }

        public Course Course { get; set; } = null!;

        [Required]
        [MaxLength(20)]
        public string AcademicYear { get; set; } = string.Empty;

        [Required]
        public Semester Semester { get; set; }

        [Required]
        [MaxLength(255)]
        public string FileName { get; set; } = string.Empty;

        [Required]
        [ForeignKey(nameof(StorageFile))]
        public Guid StorageFileId { get; set; }

        public StoredFile StorageFile { get; set; } = null!;

        [Required]
        [MaxLength(64)]
        public string FileHash { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string ContentType { get; set; } = string.Empty;

        public long FileSizeBytes { get; set; }

        public DateTime SubmittedAtUtc { get; set; }

        public MarksSubmissionStatus Status { get; set; }
            = MarksSubmissionStatus.Pending;

        [ForeignKey(nameof(ReviewedByManagement))]
        public int? ReviewedByManagementId { get; set; }

        public Management? ReviewedByManagement { get; set; }

        public DateTime? ReviewedAtUtc { get; set; }

        public DateTime? SignedAtUtc { get; set; }

        [MaxLength(1000)]
        public string? ReviewComment { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}