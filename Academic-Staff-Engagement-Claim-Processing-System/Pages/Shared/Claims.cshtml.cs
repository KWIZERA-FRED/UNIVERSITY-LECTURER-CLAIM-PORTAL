using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages.Shared
{
    public class ClaimsModel : PageModel
    {
        // ============================================================
        // CURRENT USER
        // ============================================================

        public string CurrentUserId { get; set; } = "L001";

        public string CurrentUserName { get; set; }
            = "Dr. Ahmed Mohammed";

        public string CurrentUserRole { get; set; }
            = "Lecturer";


        // ============================================================
        // CLAIMS
        // ============================================================

        public List<ClaimItem> Claims { get; set; } = new();


        // ============================================================
        // PERMISSIONS
        // ============================================================

        public bool CanReviewClaims
        {
            get
            {
                return CurrentUserRole == "HOD"
                    || CurrentUserRole == "Dean"
                    || CurrentUserRole == "Management";
            }
        }


        // ============================================================
        // SUMMARY
        // ============================================================

        public int TotalClaims =>
            Claims.Count;

        public int PendingClaims =>
            Claims.Count(c =>
                c.Status == "Pending" ||
                c.Status == "Under Review");

        public int ApprovedClaims =>
            Claims.Count(c =>
                c.Status == "Approved");

        public decimal TotalAmount =>
            Claims.Sum(c => c.Amount);


        // ============================================================
        // GET
        // ============================================================

        public void OnGet(
            string? userId,
            string? role)
        {
            /*
             * Prototype user identification.
             *
             * Later this will come from the authenticated
             * user and database.
             */

            if (!string.IsNullOrWhiteSpace(userId))
            {
                CurrentUserId = userId;
            }

            SetUserInformation(role);

            LoadClaims();
        }


        // ============================================================
        // USER INFORMATION
        // ============================================================

        private void SetUserInformation(string? role)
        {
            switch (CurrentUserId)
            {
                case "L001":

                    CurrentUserName =
                        "Dr. Ahmed Mohammed";

                    CurrentUserRole =
                        "Lecturer";

                    break;


                case "L002":

                    CurrentUserName =
                        "Dr. Sarah Uwase";

                    CurrentUserRole =
                        "Lecturer";

                    break;


                case "L003":

                    CurrentUserName =
                        "Prof. Jean Claude";

                    CurrentUserRole =
                        "Senior Lecturer";

                    break;


                case "HOD001":

                    CurrentUserName =
                        "Head of Department";

                    CurrentUserRole =
                        "HOD";

                    break;


                case "DEAN001":

                    CurrentUserName =
                        "Dean";

                    CurrentUserRole =
                        "Dean";

                    break;


                case "M001":

                    CurrentUserName =
                        "Management User";

                    CurrentUserRole =
                        "Management";

                    break;


                default:

                    if (!string.IsNullOrWhiteSpace(role))
                    {
                        CurrentUserRole = role;
                    }

                    break;
            }
        }


        // ============================================================
        // LOAD CLAIMS
        // ============================================================

        private void LoadClaims()
        {
            var allClaims = new List<ClaimItem>
            {
                new ClaimItem
                {
                    Id = 1,

                    ClaimNumber =
                        "CLM-2026-001",

                    LecturerId =
                        "L001",

                    LecturerName =
                        "Dr. Ahmed Mohammed",

                    CourseCode =
                        "CS101",

                    CourseName =
                        "Introduction to Computer Science",

                    AcademicYear =
                        "2026/2027",

                    Campus =
                        "Kigali",

                    Hours = 80,

                    Rate = 5000,

                    Amount = 400000,

                    SubmittedDate =
                        "20 August 2026",

                    Status =
                        "Pending"
                },


                new ClaimItem
                {
                    Id = 2,

                    ClaimNumber =
                        "CLM-2026-002",

                    LecturerId =
                        "L002",

                    LecturerName =
                        "Dr. Sarah Uwase",

                    CourseCode =
                        "SE201",

                    CourseName =
                        "Software Engineering",

                    AcademicYear =
                        "2026/2027",

                    Campus =
                        "Kigali",

                    Hours = 70,

                    Rate = 7000,

                    Amount = 490000,

                    SubmittedDate =
                        "19 August 2026",

                    Status =
                        "Under Review"
                },


                new ClaimItem
                {
                    Id = 3,

                    ClaimNumber =
                        "CLM-2026-003",

                    LecturerId =
                        "L003",

                    LecturerName =
                        "Prof. Jean Claude",

                    CourseCode =
                        "DB301",

                    CourseName =
                        "Database Management Systems",

                    AcademicYear =
                        "2026/2027",

                    Campus =
                        "Rwamagana",

                    Hours = 60,

                    Rate = 9000,

                    Amount = 540000,

                    SubmittedDate =
                        "18 August 2026",

                    Status =
                        "Approved"
                },


                new ClaimItem
                {
                    Id = 4,

                    ClaimNumber =
                        "CLM-2026-004",

                    LecturerId =
                        "L004",

                    LecturerName =
                        "Dr. Patrick Niyonzima",

                    CourseCode =
                        "AI401",

                    CourseName =
                        "Artificial Intelligence",

                    AcademicYear =
                        "2026/2027",

                    Campus =
                        "Kigali",

                    Hours = 50,

                    Rate = 11000,

                    Amount = 550000,

                    SubmittedDate =
                        "17 August 2026",

                    Status =
                        "Pending"
                },


                new ClaimItem
                {
                    Id = 5,

                    ClaimNumber =
                        "CLM-2026-005",

                    LecturerId =
                        "L005",

                    LecturerName =
                        "Prof. Grace Mukamana",

                    CourseCode =
                        "IOT301",

                    CourseName =
                        "Internet of Things",

                    AcademicYear =
                        "2026/2027",

                    Campus =
                        "Nyanza",

                    Hours = 45,

                    Rate = 13000,

                    Amount = 585000,

                    SubmittedDate =
                        "16 August 2026",

                    Status =
                        "Approved"
                }
            };


            /*
             * LECTURERS:
             *
             * Only see their own claims.
             *
             * HOD / Dean / Management:
             *
             * Can see claims relevant to processing.
             */

            if (CurrentUserRole == "Lecturer" ||
                CurrentUserRole == "Senior Lecturer")
            {
                Claims = allClaims
                    .Where(c =>
                        c.LecturerId ==
                        CurrentUserId)
                    .ToList();
            }
            else
            {
                Claims = allClaims;
            }
        }


        // ============================================================
        // CLAIM CLASS
        // ============================================================

        public class ClaimItem
        {
            public int Id { get; set; }

            public string ClaimNumber { get; set; }
                = "";

            public string LecturerId { get; set; }
                = "";

            public string LecturerName { get; set; }
                = "";

            public string CourseCode { get; set; }
                = "";

            public string CourseName { get; set; }
                = "";

            public string AcademicYear { get; set; }
                = "";

            public string Campus { get; set; }
                = "";

            public int Hours { get; set; }

            public decimal Rate { get; set; }

            public decimal Amount { get; set; }

            public string SubmittedDate { get; set; }
                = "";

            public string Status { get; set; }
                = "";


            // ========================================================
            // STATUS CSS
            // ========================================================

            public string StatusClass
            {
                get
                {
                    return Status switch
                    {
                        "Approved" =>
                            "approved",

                        "Rejected" =>
                            "rejected",

                        "Under Review" =>
                            "processing",

                        _ =>
                            "pending"
                    };
                }
            }


            // ========================================================
            // STATUS ICON
            // ========================================================

            public string StatusIcon
            {
                get
                {
                    return Status switch
                    {
                        "Approved" =>
                            "bi-check-circle",

                        "Rejected" =>
                            "bi-x-circle",

                        "Under Review" =>
                            "bi-arrow-repeat",

                        _ =>
                            "bi-clock"
                    };
                }
            }
        }
    }
}