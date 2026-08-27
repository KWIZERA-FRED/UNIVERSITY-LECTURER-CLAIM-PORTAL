using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;

namespace Academic_Staff_Engagement_Claim_Processing_System.Data.Models
{
    public class CourseAssignment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey(nameof(Lecturer))]
        public int LecturerId { get; set; }
        public Lecturer Lecturer { get; set; } = null!;

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
        public Session Session { get; set; }

        [Required]
        public Campus Campus { get; set; }

        [Range(0, 500)]
        public decimal AllocatedHours { get; set; }

        public bool IsApproved { get; set; } = false;

        [ForeignKey(nameof(ApprovedByHod))]
        public int? ApprovedByHodId { get; set; }
        public Hod? ApprovedByHod { get; set; }

        public DateTime? ApprovedAtUtc { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAtUtc { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }

        public ICollection<Claim> Claims { get; set; } = new List<Claim>();
    }
}