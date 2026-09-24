using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Academic_Staff_Engagement_Claim_Processing_System.Data.Models
{
    public class ClaimAttendanceRecord
    {
        [Key]
        public int Id { get; set; }

    [Required]
        [ForeignKey(nameof(ClaimAttendance))]
        public int ClaimAttendanceId { get; set; }

        public ClaimAttendance ClaimAttendance { get; set; } = null!;

        [Required]
        public DateTime SessionDate { get; set; }

        [Required]
        [MaxLength(100)]
        public string SessionTitle { get; set; } = string.Empty;

        public bool Attended { get; set; }
    }

}
