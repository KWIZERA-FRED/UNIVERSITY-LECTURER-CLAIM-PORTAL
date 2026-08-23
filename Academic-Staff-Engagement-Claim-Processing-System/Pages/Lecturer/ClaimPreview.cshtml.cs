using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages.Lecturer
{
    public class ClaimPreviewModel : PageModel
    {
        // =====================================================
        // CLAIM DATA
        // =====================================================

        [BindProperty(SupportsGet = true)]
        public string? Course { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Lecturer { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? LecturerId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? StartDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? FinishDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public int Hours { get; set; }

        [BindProperty(SupportsGet = true)]
        public bool MarksSubmitted { get; set; }

        [BindProperty(SupportsGet = true)]
        public bool MarksSigned { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Marks { get; set; }

        public decimal RatePerHour { get; set; }

        public decimal TotalAmount { get; set; }

        public string ClaimReference { get; set; } = "";

        public void OnGet()
        {
            // -------------------------------------------------
            // Default values for direct access to the page
            // -------------------------------------------------

            Lecturer ??= "Dr. Ahmed Mohammed";
            LecturerId ??= "L001";

            Course ??= "CS101 - Introduction to Computer Science";

            StartDate ??= DateTime.Today
                .AddMonths(-1)
                .ToString("yyyy-MM-dd");

            FinishDate ??= DateTime.Today
                .ToString("yyyy-MM-dd");

            if (Hours <= 0)
            {
                Hours = 40;
            }

            // -------------------------------------------------
            // Simulated lecturer remuneration rate
            // -------------------------------------------------

            RatePerHour = 5000m;

            TotalAmount = Hours * RatePerHour;

            // -------------------------------------------------
            // Simulated claim reference
            // -------------------------------------------------

            ClaimReference =
                "CLM-" +
                DateTime.Now.ToString("yyyyMMdd") +
                "-" +
                LecturerId;
        }

        // =====================================================
        // SIGN CLAIM
        // =====================================================

        public IActionResult OnPostSign()
        {
            // Recalculate amount
            RatePerHour = 5000m;

            TotalAmount = Hours * RatePerHour;

            // In the real system this is where the lecturer's
            // signature stored in the database will be applied
            // to the claim document.

            // For now we simulate successful signing.

            return RedirectToPage(
                "/Shared/Claims"
            );
        }
    }
}