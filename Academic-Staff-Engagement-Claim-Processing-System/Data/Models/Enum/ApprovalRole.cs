namespace Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums
{
    // Claim signing chain (in SequenceOrder): HOD -> Dean -> DirectorOfQuality -> DVCAR.
    // HR Officer and Vice Chancellor do NOT sign claims — HR only ever
    // signed contracts, and VC's role is contract-only (final step there).
    // "Management" (=3) is kept only so the existing integer value isn't
    // silently reused/reassigned by EF for any historical row; nothing
    // new should be created with it.
    public enum ApprovalRole
    {
        HOD = 1,
        Dean = 2,
        Management = 3,
        HROfficer = 4,
        DVCAR = 5,
        ViceChancellor = 6,
        DirectorOfQuality = 7
    }
}