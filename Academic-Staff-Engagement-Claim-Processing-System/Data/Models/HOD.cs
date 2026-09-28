using System.ComponentModel.DataAnnotations;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;

namespace Academic_Staff_Engagement_Claim_Processing_System.Data.Models
{
    public class Hod : AdminAccount
    {
        public override ApprovalRole Role => ApprovalRole.HOD;

        [Required]
        public Faculty Faculty { get; set; }

        public Hod(
            int id,
            string userName,
            string email,
            Faculty faculty)
            : base(id, userName, email)
        {
            Faculty = faculty;
        }
    }
}