using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages.HOD
{
    public class StaffModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public StaffModel(ApplicationDbContext context)
        {
            _context = context;
        }

        // ============================================================
        // STAFF LIST MODEL
        // ============================================================
        // Only the fields required by the Staff Management page
        // are selected.
        //
        // IMPORTANT:
        // GovernmentIdEncrypted is intentionally NOT selected.
        //
        // This prevents ASP.NET Core Data Protection from attempting
        // to decrypt Government IDs when the HOD opens this page.
        // ============================================================

        public class StaffListItem
        {
            public int Id { get; set; }

            public string UserName { get; set; } = string.Empty;

            public string Email { get; set; } = string.Empty;

            public UserRole Type { get; set; }

            public LecturerRank? Rank { get; set; }

            public bool IsActive { get; set; }
        }

        public List<StaffListItem> Lecturers { get; set; } = new();

        // ============================================================
        // GET
        // ============================================================

        public async Task OnGetAsync()
        {
            Lecturers = await _context.Lecturers
                .AsNoTracking()
                .Select(l => new StaffListItem
                {
                    Id = l.Id,
                    UserName = l.UserName,
                    Email = l.Email,
                    Type = l.Type,
                    Rank = l.Rank,
                    IsActive = l.IsActive
                })
                .OrderBy(l => l.UserName)
                .ToListAsync();
        }

        // ============================================================
        // DELETE LECTURER
        // ============================================================
        //
        // ExecuteDeleteAsync() performs the DELETE directly in SQL.
        //
        // It does NOT load the Lecturer entity first, meaning
        // GovernmentIdEncrypted is not decrypted during deletion.
        // ============================================================

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            var deletedRows = await _context.Lecturers
                .Where(l => l.Id == id)
                .ExecuteDeleteAsync();

            if (deletedRows == 0)
            {
                return NotFound();
            }

            return RedirectToPage();
        }
    }
}