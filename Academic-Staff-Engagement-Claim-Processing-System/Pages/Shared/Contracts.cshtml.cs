using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages.Shared
{
    public class ContractsModel : PageModel
    {
        // ============================================================
        // CURRENT USER
        // ============================================================

        public string CurrentUserId { get; set; } = "L001";

        public string CurrentUserName { get; set; } =
            "Dr. Ahmed Mohammed";

        public string CurrentUserRole { get; set; } =
            "Lecturer";


        // ============================================================
        // CONTRACTS
        // ============================================================

        public List<ContractItem> Contracts { get; set; } = new();


        // ============================================================
        // SUMMARY
        // ============================================================

        public int TotalContracts =>
            Contracts.Count;

        public int PendingContracts =>
            Contracts.Count(c => !c.IsSignedByCurrentUser);

        public int SignedContracts =>
            Contracts.Count(c => c.IsSignedByCurrentUser);


        // ============================================================
        // GET
        // ============================================================

        public void OnGet(
            string? userId,
            string? role)
        {
            /*
             * FOR NOW THIS SIMULATES THE LOGGED-IN USER.
             *
             * Later these values will come from the authenticated
             * user/session/database.
             *
             * Example:
             *
             * /Contracts?userId=L001&role=Lecturer
             */

            if (!string.IsNullOrWhiteSpace(userId))
            {
                CurrentUserId = userId;
            }

            SetUserInformation(role);

            LoadContracts();
        }


        // ============================================================
        // USER INFORMATION
        // ============================================================

        private void SetUserInformation(string? role)
        {
            switch (CurrentUserId)
            {
                case "L001":

                    CurrentUserName = "Dr. Ahmed Mohammed";
                    CurrentUserRole = "Lecturer";

                    break;


                case "L002":

                    CurrentUserName = "Dr. Sarah Uwase";
                    CurrentUserRole = "Lecturer";

                    break;


                case "L003":

                    CurrentUserName = "Prof. Jean Claude";
                    CurrentUserRole = "Senior Lecturer";

                    break;


                case "HOD001":

                    CurrentUserName = "Head of Department";
                    CurrentUserRole = "HOD";

                    break;


                case "DEAN001":

                    CurrentUserName = "Dean";
                    CurrentUserRole = "Dean";

                    break;


                case "M001":

                    CurrentUserName = "Management User";
                    CurrentUserRole = "Management";

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
        // LOAD CONTRACTS
        // ============================================================

        private void LoadContracts()
        {
            /*
             * THIS IS MOCK DATA FOR THE PROTOTYPE.
             *
             * The important part is RequiredSignerId.
             *
             * A contract appears on this user's page when:
             *
             * RequiredSignerId == CurrentUserId
             *
             * Later this will come from the database.
             */

            var allContracts = new List<ContractItem>
            {
                new ContractItem
                {
                    Id = 1,

                    ContractNumber = "CON-2026-001",

                    CourseCode = "CS101",

                    CourseName =
                        "Introduction to Computer Science",

                    LecturerName =
                        "Dr. Ahmed Mohammed",

                    GovernmentId =
                        "1198780012345678",

                    Rank =
                        "Assistant Lecturer",

                    AcademicYear =
                        "2026/2027",

                    Session =
                        "Day",

                    Campus =
                        "Kigali",

                    Hours = 80,

                    Rate = 5000,

                    RequiredSignerId = "L001",

                    IsSignedByCurrentUser =
                        CurrentUserId != "L001"
                },


                new ContractItem
                {
                    Id = 2,

                    ContractNumber = "CON-2026-002",

                    CourseCode = "SE201",

                    CourseName =
                        "Software Engineering",

                    LecturerName =
                        "Dr. Sarah Uwase",

                    GovernmentId =
                        "1198780023456789",

                    Rank =
                        "Lecturer",

                    AcademicYear =
                        "2026/2027",

                    Session =
                        "Evening",

                    Campus =
                        "Kigali",

                    Hours = 70,

                    Rate = 7000,

                    RequiredSignerId = "L002",

                    IsSignedByCurrentUser =
                        CurrentUserId != "L002"
                },


                new ContractItem
                {
                    Id = 3,

                    ContractNumber = "CON-2026-003",

                    CourseCode = "DB301",

                    CourseName =
                        "Database Management Systems",

                    LecturerName =
                        "Prof. Jean Claude",

                    GovernmentId =
                        "1198780034567890",

                    Rank =
                        "Senior Lecturer",

                    AcademicYear =
                        "2026/2027",

                    Session =
                        "Day",

                    Campus =
                        "Rwamagana",

                    Hours = 60,

                    Rate = 9000,

                    RequiredSignerId = "L003",

                    IsSignedByCurrentUser =
                        CurrentUserId != "L003"
                },


                new ContractItem
                {
                    Id = 4,

                    ContractNumber = "CON-2026-004",

                    CourseCode = "AI401",

                    CourseName =
                        "Artificial Intelligence",

                    LecturerName =
                        "Dr. Patrick Niyonzima",

                    GovernmentId =
                        "1198780045678901",

                    Rank =
                        "Associate Professor",

                    AcademicYear =
                        "2026/2027",

                    Session =
                        "Day",

                    Campus =
                        "Kigali",

                    Hours = 50,

                    Rate = 11000,

                    RequiredSignerId = "DEAN001",

                    IsSignedByCurrentUser =
                        CurrentUserId != "DEAN001"
                },


                new ContractItem
                {
                    Id = 5,

                    ContractNumber = "CON-2026-005",

                    CourseCode = "IOT301",

                    CourseName =
                        "Internet of Things",

                    LecturerName =
                        "Prof. Grace Mukamana",

                    GovernmentId =
                        "1198780056789012",

                    Rank =
                        "Professor",

                    AcademicYear =
                        "2026/2027",

                    Session =
                        "Evening",

                    Campus =
                        "Nyanza",

                    Hours = 45,

                    Rate = 13000,

                    RequiredSignerId = "M001",

                    IsSignedByCurrentUser =
                        CurrentUserId != "M001"
                }
            };


            /*
             * IMPORTANT:
             *
             * Only contracts requiring THIS user's signature
             * are displayed.
             */

            Contracts = allContracts
                .Where(c =>
                    c.RequiredSignerId ==
                    CurrentUserId)
                .ToList();
        }


        // ============================================================
        // CONTRACT CLASS
        // ============================================================

        public class ContractItem
        {
            public int Id { get; set; }

            public string ContractNumber { get; set; } = "";

            public string CourseCode { get; set; } = "";

            public string CourseName { get; set; } = "";

            public string LecturerName { get; set; } = "";

            public string GovernmentId { get; set; } = "";

            public string Rank { get; set; } = "";

            public string AcademicYear { get; set; } = "";

            public string Session { get; set; } = "";

            public string Campus { get; set; } = "";

            public int Hours { get; set; }

            public decimal Rate { get; set; }

            /*
             * This determines whose signature is required.
             */

            public string RequiredSignerId { get; set; } = "";

            /*
             * Later this will be replaced by a signature
             * record in the database.
             */

            public bool IsSignedByCurrentUser { get; set; }
        }
    }
}