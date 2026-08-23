using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages.HOD
{
    public class AssignCourseModel : PageModel
    {
        [BindProperty]
        public string? SelectedCourse { get; set; }

        [BindProperty]
        public string? SelectedLecturer { get; set; }

        [BindProperty]
        public string? Session { get; set; }

        [BindProperty]
        public string? AcademicYear { get; set; }

        [BindProperty]
        public string? Campus { get; set; }

        [BindProperty]
        public int Hours { get; set; }

        public List<Course> Courses { get; set; } = new();

        public List<Lecturer> Lecturers { get; set; } = new();

        public decimal HourlyRate { get; set; }

        public string? ErrorMessage { get; set; }


        public void OnGet()
        {
            LoadData();
        }


        public IActionResult OnPost()
        {
            LoadData();


            if (string.IsNullOrWhiteSpace(SelectedCourse))
            {
                ErrorMessage = "Please select a course.";
                return Page();
            }


            if (string.IsNullOrWhiteSpace(SelectedLecturer))
            {
                ErrorMessage = "Please select a lecturer.";
                return Page();
            }


            if (string.IsNullOrWhiteSpace(Session))
            {
                ErrorMessage = "Please select a session.";
                return Page();
            }


            if (string.IsNullOrWhiteSpace(AcademicYear))
            {
                ErrorMessage = "Please select an academic year.";
                return Page();
            }


            if (string.IsNullOrWhiteSpace(Campus))
            {
                ErrorMessage = "Please select a campus.";
                return Page();
            }


            if (Hours <= 0)
            {
                ErrorMessage = "Please enter a valid number of teaching hours.";
                return Page();
            }


            var lecturer = Lecturers.FirstOrDefault(
                l => l.Id == SelectedLecturer
            );


            if (lecturer == null)
            {
                ErrorMessage = "Selected lecturer could not be found.";
                return Page();
            }


            var course = Courses.FirstOrDefault(
                c => c.Code == SelectedCourse
            );


            if (course == null)
            {
                ErrorMessage = "Selected course could not be found.";
                return Page();
            }


            HourlyRate = GetRateForRank(lecturer.Rank);


            // IMPORTANT:
            // This is the original working redirect pattern.
            return RedirectToPage(
                "./ContractPreview",
                new
                {
                    Course = course.Code + " - " + course.Name,

                    Lecturer = lecturer.Name,

                    GovernmentId = lecturer.GovernmentId,

                    Rank = lecturer.Rank,

                    Session = Session,

                    AcademicYear = AcademicYear,

                    Campus = Campus,

                    Hours = Hours,

                    Rate = HourlyRate
                }
            );
        }


        private void LoadData()
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


            Lecturers = new List<Lecturer>
            {
                new Lecturer(
                    "L001",
                    "Dr. Ahmed Mohammed",
                    "Assistant Lecturer",
                    "1198780012345678"
                ),

                new Lecturer(
                    "L002",
                    "Dr. Sarah Uwase",
                    "Lecturer",
                    "1198780023456789"
                ),

                new Lecturer(
                    "L003",
                    "Prof. Jean Claude",
                    "Senior Lecturer",
                    "1198780034567890"
                ),

                new Lecturer(
                    "L004",
                    "Dr. Patrick Niyonzima",
                    "Associate Professor",
                    "1198780045678901"
                ),

                new Lecturer(
                    "L005",
                    "Prof. Grace Mukamana",
                    "Professor",
                    "1198780056789012"
                )
            };
        }


        private decimal GetRateForRank(string rank)
        {
            return rank switch
            {
                "Assistant Lecturer" => 5000m,

                "Lecturer" => 7000m,

                "Senior Lecturer" => 9000m,

                "Associate Professor" => 11000m,

                "Professor" => 13000m,

                "Teaching Assistant" => 4000m,

                "Professor Emeritus" => 13000m,

                _ => 5000m
            };
        }


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


        public class Lecturer
        {
            public string Id { get; set; }

            public string Name { get; set; }

            public string Rank { get; set; }

            public string GovernmentId { get; set; }


            public Lecturer(
                string id,
                string name,
                string rank,
                string governmentId)
            {
                Id = id;
                Name = name;
                Rank = rank;
                GovernmentId = governmentId;
            }
        }
    }
}