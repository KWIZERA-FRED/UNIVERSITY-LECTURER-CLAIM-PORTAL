using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages.Lecturer
{
    public class SubmitMarksModel : PageModel
    {
        // =====================================================
        // FORM PROPERTIES
        // =====================================================

        [BindProperty]
        public string? SelectedCourse { get; set; }

        [BindProperty]
        public string? AcademicYear { get; set; }

        [BindProperty]
        public IFormFile? MarksFile { get; set; }

        public string? ErrorMessage { get; set; }

        public string? SuccessMessage { get; set; }

        public List<Course> Courses { get; set; } = new();

        // =====================================================
        // SIMULATED LECTURER
        // =====================================================

        public string LecturerName { get; set; } =
            "Dr. Ahmed Mohammed";

        public string LecturerId { get; set; } =
            "L001";

        // =====================================================
        // GET
        // =====================================================

        public void OnGet()
        {
            LoadCourses();
        }

        // =====================================================
        // POST
        // =====================================================

        public IActionResult OnPost()
        {
            LoadCourses();

            // -------------------------------------------------
            // Validate course
            // -------------------------------------------------

            if (string.IsNullOrWhiteSpace(SelectedCourse))
            {
                ErrorMessage = "Please select the course.";
                return Page();
            }

            // -------------------------------------------------
            // Validate academic year
            // -------------------------------------------------

            if (string.IsNullOrWhiteSpace(AcademicYear))
            {
                ErrorMessage = "Please select the academic year.";
                return Page();
            }

            // -------------------------------------------------
            // Validate file
            // -------------------------------------------------

            if (MarksFile == null || MarksFile.Length == 0)
            {
                ErrorMessage = "Please upload the Excel marks sheet.";
                return Page();
            }

            // -------------------------------------------------
            // Validate extension
            // -------------------------------------------------

            var extension =
                Path.GetExtension(MarksFile.FileName)
                    .ToLowerInvariant();

            if (extension != ".xlsx")
            {
                ErrorMessage =
                    "Only Excel .xlsx files are accepted.";

                return Page();
            }

            // -------------------------------------------------
            // Validate file size
            // -------------------------------------------------

            const long maxFileSize = 10 * 1024 * 1024;

            if (MarksFile.Length > maxFileSize)
            {
                ErrorMessage =
                    "The Excel file cannot be larger than 10 MB.";

                return Page();
            }

            // -------------------------------------------------
            // Find course
            // -------------------------------------------------

            var course = Courses.FirstOrDefault(
                c => c.Code == SelectedCourse
            );

            if (course == null)
            {
                ErrorMessage =
                    "The selected course could not be found.";

                return Page();
            }

            // -------------------------------------------------
            // Generate submission reference
            // -------------------------------------------------

            var submissionReference =
                "MRK-" +
                DateTime.Now.ToString("yyyyMMddHHmmss") +
                "-" +
                LecturerId;

            // -------------------------------------------------
            // In the prototype we do not permanently save the
            // Excel file yet.
            //
            // Later this will be stored securely in the database
            // and linked to the lecturer/course.
            // -------------------------------------------------

            return RedirectToPage(
                "/Lecturer/MarksPreview",
                new
                {
                    Course =
                        course.Code + " - " + course.Name,

                    Lecturer = LecturerName,

                    LecturerId = LecturerId,

                    AcademicYear = AcademicYear,

                    FileName = MarksFile.FileName,

                    FileSize = MarksFile.Length,

                    SubmissionReference =
                        submissionReference
                }
            );
        }

        // =====================================================
        // LOAD COURSES
        // =====================================================

        private void LoadCourses()
        {
            Courses = new List<Course>
            {
                new Course(
                    "CS101",
                    "Introduction to Computer Science"
                ),

                new Course(
                    "SE201",
                    "Software Engineering"
                ),

                new Course(
                    "DB301",
                    "Database Management Systems"
                ),

                new Course(
                    "NET202",
                    "Computer Networks"
                ),

                new Course(
                    "AI401",
                    "Artificial Intelligence"
                ),

                new Course(
                    "IOT301",
                    "Internet of Things"
                ),

                new Course(
                    "WD302",
                    "Web Development"
                ),

                new Course(
                    "CYB401",
                    "Cybersecurity"
                ),

                new Course(
                    "OS301",
                    "Operating Systems"
                ),

                new Course(
                    "MOB302",
                    "Mobile Application Development"
                )
            };
        }

        // =====================================================
        // COURSE CLASS
        // =====================================================

        public class Course
        {
            public string Code { get; set; }

            public string Name { get; set; }

            public Course(
                string code,
                string name)
            {
                Code = code;
                Name = name;
            }
        }
    }
}