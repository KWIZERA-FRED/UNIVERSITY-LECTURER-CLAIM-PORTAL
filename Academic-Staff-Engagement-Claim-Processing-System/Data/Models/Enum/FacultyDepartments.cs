using System.Collections.Generic;

namespace Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums
{
    public static class FacultyDepartments
    {
        private static readonly IReadOnlyDictionary<Faculty, IReadOnlyList<Department>> Mapping =
            new Dictionary<Faculty, IReadOnlyList<Department>>
            {
                {
                    Faculty.ComputingAndInformationSciences,
                    new List<Department>
                    {
                        Department.SoftwareEngineering,
                        Department.InformationSystemsManagement,
                        Department.Multimedia,
                        Department.Networking
                    }
                },
                {
                    Faculty.Law,
                    new List<Department>
                    {
                        Department.PublicLaw,
                        Department.PrivateLaw,
                        Department.InternationalLawEnvironmentAndLandUseLaw
                    }
                },
                {
                    Faculty.EconomicSciencesAndManagement,
                    new List<Department>
                    {
                        Department.Accounting,
                        Department.Finance,
                        Department.Marketing,
                        Department.HumanResourcesManagement,
                        Department.Economics,
                        Department.CooperativeManagement
                    }
                },
                {
                    Faculty.EnvironmentalStudies,
                    new List<Department>
                    {
                        Department.EnvironmentalManagementAndConservation,
                        Department.EmergencyAndDisasterManagement,
                        Department.RuralDevelopment
                    }
                }
            };

        public static IReadOnlyList<Department> GetDepartments(Faculty faculty)
        {
            return Mapping.TryGetValue(faculty, out var departments)
                ? departments
                : [];
        }

        public static bool IsValidDepartment(
            Faculty faculty,
            Department department)
        {
            return Mapping.TryGetValue(faculty, out var departments)
                && departments.Contains(department);
        }
    }
}