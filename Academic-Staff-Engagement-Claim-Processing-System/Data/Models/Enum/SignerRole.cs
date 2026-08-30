namespace Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums
{
    // Who this particular signature step belongs to. Unlike ApprovalRole
    // (which only ever covers admin roles acting on a Claim), a contract's
    // first required signature is always the Lecturer being contracted —
    // so Lecturer has to be a first-class value here.
    public enum SignerRole
    {
        Lecturer = 1,
        HOD = 2,
        Dean = 3,
        Management = 4
    }
}