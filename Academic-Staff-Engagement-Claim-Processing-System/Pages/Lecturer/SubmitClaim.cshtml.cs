using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages.Lecturer
{
    public class SubmitClaimModel : PageModel
    {
        [BindProperty]
        public string? SelectedCourse { get; set; }

    [BindProperty]
        public DateTime? StartDate { get; set; }

        [BindProperty]
        public DateTime? FinishDate { get; set; }

        [BindProperty]
        public int Hours { get; set; }

        public string LecturerName { get; set; } = "Dr. Ahmed Mohammed";

        public string LecturerId { get; set; } = "L001";

        public string? ErrorMessage { get; set; }

        public List<CourseItem> Courses { get; set; } = new();

        public List<ClaimRecord> PreviousClaims { get; set; } = new();

        public void OnGet()
        {
            LoadData();
        }

        public IActionResult OnPost()
        {
            LoadData();

            // Validate course
            if (string.IsNullOrWhiteSpace(SelectedCourse))
            {
                ErrorMessage = "Please select the course you want to claim for.";
                return Page();
            }

            // Validate start date
            if (!StartDate.HasValue)
            {
                ErrorMessage = "Please select the claim start date.";
                return Page();
            }

            // Validate finish date
            if (!FinishDate.HasValue)
            {
                ErrorMessage = "Please select the claim finish date.";
                return Page();
            }

            // Validate dates
            if (FinishDate.Value.Date < StartDate.Value.Date)
            {
                ErrorMessage = "The finish date cannot be earlier than the start date.";
                return Page();
            }

            // Validate hours
            if (Hours <= 0)
            {
                ErrorMessage = "Please enter a valid number of teaching hours.";
                return Page();
            }

            // Find selected course
            var course = Courses.FirstOrDefault(
                c => c.Code == SelectedCourse
            );

            if (course == null)
            {
                ErrorMessage = "The selected course could not be found.";
                return Page();
            }

            // Redirect to claim preview
            return RedirectToPage(
                "/Lecturer/ClaimPreview",
                new
                {
                    Course = course.Code + " - " + course.Name,
                    Lecturer = LecturerName,
                    LecturerId = LecturerId,
                    StartDate = StartDate.Value.ToString("yyyy-MM-dd"),
                    FinishDate = FinishDate.Value.ToString("yyyy-MM-dd"),
                    Hours = Hours
                }
            );
        }

        private void LoadData()
        {
            Courses = new List<CourseItem>
        {
            new CourseItem(
                "CS101",
                "Introduction to Computer Science"
            ),

            new CourseItem(
                "SE201",
                "Software Engineering"
            ),

            new CourseItem(
                "DB301",
                "Database Management Systems"
            ),

            new CourseItem(
                "NET202",
                "Computer Networks"
            ),

            new CourseItem(
                "AI401",
                "Artificial Intelligence"
            ),

            new CourseItem(
                "IOT301",
                "Internet of Things"
            ),

            new CourseItem(
                "WD302",
                "Web Development"
            ),

            new CourseItem(
                "CYB401",
                "Cybersecurity"
            ),

            new CourseItem(
                "OS301",
                "Operating Systems"
            ),

            new CourseItem(
                "MOB302",
                "Mobile Application Development"
            )
        };

            PreviousClaims = new List<ClaimRecord>
        {
            new ClaimRecord
            {
                Course = "CS101 - Introduction to Computer Science",
                StartDate = "2026-02-01",
                FinishDate = "2026-05-30",
                Hours = 40,
                Status = "Paid"
            }
        };
        }

        // =====================================================
        // MODELS
        // =====================================================

        public class CourseItem
        {
            public string Code { get; set; }

            public string Name { get; set; }

            public CourseItem(string code, string name)
            {
                Code = code;
                Name = name;
            }
        }

        public class ClaimRecord
        {
            public string Course { get; set; } = "";

            public string StartDate { get; set; } = "";

            public string FinishDate { get; set; } = "";

            public int Hours { get; set; }

            public string Status { get; set; } = "";
        }
    }


}
