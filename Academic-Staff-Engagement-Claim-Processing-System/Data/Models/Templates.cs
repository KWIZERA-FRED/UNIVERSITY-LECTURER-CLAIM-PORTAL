using System.ComponentModel.DataAnnotations;

namespace Academic_Staff_Engagement_Claim_Processing_System.Data.Models
{
    public class Template
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Contract { get; set; } = string.Empty;

        [Required]
        public string Claim { get; set; } = string.Empty;

        [Required]
        public string Letter { get; set; } = string.Empty;
    }
}