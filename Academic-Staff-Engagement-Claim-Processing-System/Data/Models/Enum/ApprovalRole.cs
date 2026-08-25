namespace Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums
{
    // Records which role acted on an approval step, independent of who the
    // specific User was — useful for reporting ("how many claims did HODs
    // reject this semester") without joining back to User.Role every time.
    public enum ApprovalRole
    {
        HOD = 1,
        Dean = 2,
        Management = 3
    }
}