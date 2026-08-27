using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Academic_Staff_Engagement_Claim_Processing_System.Data.Models
{
    public class Course
    {
        [Key]
        public int Id { get; private set; }

        [Required]
        [MaxLength(20)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Department { get; set; } = string.Empty;

        [Range(0, 30)]
        public decimal CreditHours { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
        public DateTime? UpdatedAtUtc { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }

        public ICollection<CourseAssignment> CourseAssignments { get; set; } = new List<CourseAssignment>();

        public Course(int id, string code, string title, string department, decimal creditHours)
        {
            Id = id;
            Code = code;
            Title = title;
            Department = department;
            CreditHours = creditHours;
        }
    }
}