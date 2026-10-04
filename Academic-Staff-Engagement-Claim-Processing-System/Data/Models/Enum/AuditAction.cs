namespace Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums
{

    public enum AuditAction
    {
        // ----------------------------------------------------------------
        // Original actions (do not renumber)
        // ----------------------------------------------------------------

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
        MarksSigned = 14,
        MarksDeclined = 15,
        ContractDeclined = 16,
        ClaimLinkRegenerated = 17,

        // ----------------------------------------------------------------
        // Authentication and sessions (100s)
        // ----------------------------------------------------------------

        LogoutSucceeded = 100,
        PasswordChangeFailed = 101,

        // ----------------------------------------------------------------
        // Accounts (200s)
        // ----------------------------------------------------------------

        AccountUpdated = 200,
        SignatureUploaded = 201,
        AccountReactivated = 202,

        // ----------------------------------------------------------------
        // Course assignments (300s)
        // ----------------------------------------------------------------

        CourseAssignmentUpdated = 300,
        CourseAssignmentRemoved = 301,

        // ----------------------------------------------------------------
        // Contracts (400s)
        // ----------------------------------------------------------------

        ContractGenerated = 400,
        ContractFullySigned = 401,
        ContractSignatureViewed = 402,

        // ----------------------------------------------------------------
        // Marks (500s)
        // ----------------------------------------------------------------

        MarksDownloaded = 500,

        // ----------------------------------------------------------------
        // Attendance (600s)
        // ----------------------------------------------------------------

        AttendanceRetrieved = 600,

        // ----------------------------------------------------------------
        // Claims (700s)
        // ----------------------------------------------------------------

        ClaimChecklistSaved = 700,
        ClaimFullyApproved = 701,

        // ----------------------------------------------------------------
        // Claim documents (800s)
        // ----------------------------------------------------------------

        ClaimDocumentsViewed = 800,
        ClaimDocumentDownloaded = 801,
        ClaimDocumentDownloadDenied = 802,

        // ----------------------------------------------------------------
        // Email (900s)
        // ----------------------------------------------------------------

        EmailSent = 900,
        EmailFailed = 901,

        // ----------------------------------------------------------------
        // Security events (1000s)
        // ----------------------------------------------------------------

        RateLimitExceeded = 1000,
        InvalidClaimLinkAccessed = 1001
    }
}