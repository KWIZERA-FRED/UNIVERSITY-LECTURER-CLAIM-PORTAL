using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages.HOD
{
    public class ContractPreviewModel : PageModel
    {
        [BindProperty(SupportsGet = true)]
        public string? Course { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Lecturer { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? GovernmentId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Rank { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Session { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? AcademicYear { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Campus { get; set; }

        [BindProperty(SupportsGet = true)]
        public int Hours { get; set; }

        [BindProperty(SupportsGet = true)]
        public decimal Rate { get; set; }

        public decimal TotalAmount => Hours * Rate;

        public void OnGet()
        {
        }
    }
}