namespace Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums
{
    public enum AuditAction
    {
        LoginSucceeded = 1,
        LoginFailed = 2,
        AccountLockedOut = 3,
        AccountCreated = 4,
        AccountDeactivated = 5,
        CourseAssigned = 6,
        ContractSigned = 7,
        ClaimSubmitted = 8,
        ClaimApproved = 9,
        ClaimRejected = 10,
        AccessDenied = 11,
        PasswordChanged = 12,
        MarksSubmitted = 13,
    }
}