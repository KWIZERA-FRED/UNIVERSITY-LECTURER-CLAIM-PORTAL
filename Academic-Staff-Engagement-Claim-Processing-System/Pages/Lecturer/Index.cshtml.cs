using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

using LecturerModel =
    Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Lecturer;

using ClaimModel =
    Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Claim;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages.Lecturer
{
    [Authorize(Roles = "Lecturer")]
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }

        // ============================================================
        // LECTURER
        // ============================================================

        public LecturerModel? CurrentLecturer { get; set; }

        public string LecturerTypeName =>
            CurrentLecturer?.Type == UserRole.FullTimeLecturer
                ? "Full-Time Lecturer"
                : "Part-Time Lecturer";

        public bool IsFullTime =>
            CurrentLecturer?.Type == UserRole.FullTimeLecturer;

        public bool IsPartTime =>
            CurrentLecturer?.Type == UserRole.PartTimeLecturer;


        // ============================================================
        // COURSE ASSIGNMENTS
        // ============================================================

        public List<CourseAssignment> CourseAssignments { get; private set; }
            = new();

        public int ActiveCourseCount =>
            CourseAssignments.Count(ca => ca.IsActive);

        public decimal TotalAllocatedHours =>
            CourseAssignments
                .Where(ca => ca.IsActive)
                .Sum(ca => ca.AllocatedHours);


        // ============================================================
        // CONTRACTS
        // ============================================================

        public List<Contract> Contracts { get; private set; }
            = new();

        public int ContractCount =>
            Contracts.Count;

        public int PendingSignatureCount =>
            Contracts.Count(c =>
                c.Status == ContractStatus.PendingSignature);

        public int ActiveContractCount =>
            Contracts.Count(c =>
                c.Status == ContractStatus.Active);


        // ============================================================
        // CLAIMS
        // ============================================================

        public List<ClaimModel> Claims { get; private set; }
            = new();

        public int ClaimCount =>
            Claims.Count;

        public int PendingClaimCount =>
            Claims.Count(c =>
                c.Status == ClaimStatus.Submitted ||
                c.Status == ClaimStatus.PendingHODApproval ||
                c.Status == ClaimStatus.PendingDeanApproval);

        public int ApprovedClaimCount =>
            Claims.Count(c =>
                c.Status == ClaimStatus.Approved);

        public int PaidClaimCount =>
            Claims.Count(c =>
                c.Status == ClaimStatus.Paid);

        public decimal TotalHoursClaimed =>
            Claims.Sum(c => c.HoursClaimed);


        // ============================================================
        // PAGE LOAD
        // ============================================================

        public async Task OnGetAsync()
        {
            // --------------------------------------------------------
            // Get the lecturer ID from the authentication cookie.
            // Login.cshtml.cs stores this as "UserId".
            // --------------------------------------------------------

            string? userIdValue =
                User.FindFirstValue("UserId");

            if (!int.TryParse(userIdValue, out int lecturerId))
            {
                throw new InvalidOperationException(
                    "The logged-in lecturer ID could not be found.");
            }


            // --------------------------------------------------------
            // Load the logged-in lecturer
            // --------------------------------------------------------

            CurrentLecturer = await _context.Lecturers
                .AsNoTracking()
                .FirstOrDefaultAsync(l =>
                    l.Id == lecturerId &&
                    l.IsActive);

            if (CurrentLecturer == null)
            {
                throw new InvalidOperationException(
                    "The logged-in lecturer could not be found.");
            }


            // --------------------------------------------------------
            // Load course assignments
            // --------------------------------------------------------

            CourseAssignments = await _context.CourseAssignments
                .AsNoTracking()
                .Include(ca => ca.Course)
                .Where(ca =>
                    ca.LecturerId == lecturerId &&
                    ca.IsActive)
                .OrderByDescending(ca => ca.CreatedAtUtc)
                .ToListAsync();


            // --------------------------------------------------------
            // Load contracts
            // --------------------------------------------------------

            Contracts = await _context.Contracts
                .AsNoTracking()
                .Include(c => c.CourseAssignment)
                    .ThenInclude(ca => ca!.Course)
                .Where(c =>
                    c.LecturerId == lecturerId)
                .OrderByDescending(c => c.CreatedAtUtc)
                .ToListAsync();


            // --------------------------------------------------------
            // Load claims
            // --------------------------------------------------------

            Claims = await _context.Claims
                .AsNoTracking()
                .Include(c => c.CourseAssignment)
                    .ThenInclude(ca => ca.Course)
                .Include(c => c.Contract)
                .Where(c =>
                    c.CourseAssignment.LecturerId == lecturerId)
                .OrderByDescending(c => c.CreatedAtUtc)
                .ToListAsync();
        }
    }
}