using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Claims;

namespace Academic_Staff_Engagement_Claim_Processing_System.Data.Models
{
    public class ClaimAttendance
    {
        [Key]
        public int Id { get; set; }
    [Required]
        [ForeignKey(nameof(Claim))]
        public int ClaimId { get; set; }

        public Claim Claim { get; set; } = null!;

        [Required]
        [MaxLength(100)]
        public string MisReference { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string LecturerName { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string CourseCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string CourseTitle { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string AcademicYear { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Semester { get; set; } = string.Empty;

        public int TotalSessions { get; set; }

        public int AttendedSessions { get; set; }

        public DateTime RetrievedAtUtc { get; set; } = DateTime.UtcNow;

        public ICollection<ClaimAttendanceRecord> Records { get; set; } =
            new List<ClaimAttendanceRecord>();
    }

}
