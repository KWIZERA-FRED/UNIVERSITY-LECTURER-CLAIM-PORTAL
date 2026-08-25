namespace Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums
{
    public enum ClaimStatus
    {
        Draft = 1,
        Submitted = 2,
        PendingHODApproval = 3,
        PendingDeanApproval = 4,
        Approved = 5,
        Rejected = 6,
        Paid = 7
    }
}