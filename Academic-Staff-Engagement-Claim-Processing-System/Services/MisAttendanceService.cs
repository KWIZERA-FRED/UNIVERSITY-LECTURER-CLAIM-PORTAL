using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Academic_Staff_Engagement_Claim_Processing_System.Services
{
    public interface IMisAttendanceService
    {
        Task<MisAttendanceResult?> GetAttendanceAsync(
        int lecturerId,
        int courseAssignmentId);
    }

public sealed class MisAttendanceService : IMisAttendanceService
    {
        private readonly ApplicationDbContext _context;

        public MisAttendanceService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<MisAttendanceResult?> GetAttendanceAsync(
            int lecturerId,
            int courseAssignmentId)
        {
            var assignment = await _context.CourseAssignments
                .AsNoTracking()
                .Include(a => a.Lecturer)
                .Include(a => a.Course)
                .FirstOrDefaultAsync(a =>
                    a.Id == courseAssignmentId &&
                    a.LecturerId == lecturerId &&
                    a.IsActive &&
                    a.Course.IsActive);

            if (assignment is null)
                return null;

            var startDate = DateTime.UtcNow.Date.AddDays(-42);

            var records = new List<MisAttendanceRecord>();

            for (int i = 0; i < 12; i++)
            {
                var sessionDate = startDate.AddDays(i * 3);

                records.Add(
                    new MisAttendanceRecord
                    {
                        SessionDate = sessionDate,
                        SessionTitle = $"Teaching Session {i + 1}",
                        Attended = i != 4 && i != 9
                    });
            }

            return new MisAttendanceResult
            {
                MisReference =
                    $"MIS-ATT-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}".Substring(0, 28),

                LecturerId = lecturerId,
                CourseAssignmentId = assignment.Id,
                LecturerName = assignment.Lecturer.UserName,
                CourseCode = assignment.Course.Code,
                CourseTitle = assignment.Course.Title,
                AcademicYear = assignment.AcademicYear,
                Semester = assignment.Semester.ToString(),
                TotalSessions = records.Count,
                AttendedSessions = records.Count(r => r.Attended),
                RetrievedAtUtc = DateTime.UtcNow,
                Records = records
            };
        }
    }

    public sealed class MisAttendanceResult
    {
        public string MisReference { get; set; } = string.Empty;

        public int LecturerId { get; set; }

        public int CourseAssignmentId { get; set; }

        public string LecturerName { get; set; } = string.Empty;

        public string CourseCode { get; set; } = string.Empty;

        public string CourseTitle { get; set; } = string.Empty;

        public string AcademicYear { get; set; } = string.Empty;

        public string Semester { get; set; } = string.Empty;

        public int TotalSessions { get; set; }

        public int AttendedSessions { get; set; }

        public DateTime RetrievedAtUtc { get; set; }

        public List<MisAttendanceRecord> Records { get; set; } = new();
    }

    public sealed class MisAttendanceRecord
    {
        public DateTime SessionDate { get; set; }

        public string SessionTitle { get; set; } = string.Empty;

        public bool Attended { get; set; }
    }

}
