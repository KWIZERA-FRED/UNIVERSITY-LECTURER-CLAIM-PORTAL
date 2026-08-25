using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;

namespace Academic_Staff_Engagement_Claim_Processing_System.Data.Models
{
    public class Dean : AdminAccount
    {
        public override ApprovalRole Role => ApprovalRole.Dean;

        public Dean(int id, string userName, string email)
            : base(id, userName, email) { }
    }
}