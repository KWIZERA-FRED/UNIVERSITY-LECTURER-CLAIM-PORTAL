<<<<<<< HEAD
 
=======
using System.ComponentModel.DataAnnotations;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;

namespace Academic_Staff_Engagement_Claim_Processing_System.Data.Models
{
    public class Hod : AdminAccount
    {
        public override ApprovalRole Role => ApprovalRole.HOD;

        [Required]
        [MaxLength(100)]
        public string Department { get; set; } = string.Empty;

        public Hod(int id, string userName, string email, string department)
            : base(id, userName, email)
        {
            Department = department;
        }
    }
}
>>>>>>> cf0e0096cb88d96f2be5daaf1b7f4640c1782cad
