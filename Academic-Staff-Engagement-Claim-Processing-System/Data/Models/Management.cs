using System.ComponentModel.DataAnnotations;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;

namespace Academic_Staff_Engagement_Claim_Processing_System.Data.Models
{
    public class Management : AdminAccount
    {
        public override ApprovalRole Role => ApprovalRole.Management;

        [Required]
        public ManagementTitle Title { get; set; }

        public Management(int id, string userName, string email, ManagementTitle title)
            : base(id, userName, email)
        {
            Title = title;
        }
    }
}