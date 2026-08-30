namespace Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums
{
    // Matches the 5 signature lines on the actual UNILAK part-time
    // contract. HOD is deliberately absent — the HOD creates the
    // account and assigns the course, but does not sign this document.
    public enum SignerRole
    {
        Lecturer = 1,
        Dean = 2,
        HROfficer = 3,
        DVCAR = 4,
        ViceChancellor = 5
    }
}