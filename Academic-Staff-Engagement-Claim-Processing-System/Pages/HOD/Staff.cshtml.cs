using Academic_Staff_Engagement_Claim_Processing_System.Data;
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

        public List<Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Lecturer> Lecturers
        {
            get;
            set;
        } = new();

        public async Task OnGetAsync()
        {
            Lecturers = await _context.Lecturers
                .AsNoTracking()
                .OrderBy(l => l.UserName)
                .ToListAsync();
        }

        // ============================================================
        // DELETE LECTURER
        // ============================================================

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            var lecturer = await _context.Lecturers
                .FirstOrDefaultAsync(l => l.Id == id);

            if (lecturer == null)
            {
                return NotFound();
            }

            _context.Lecturers.Remove(lecturer);

            await _context.SaveChangesAsync();

            return RedirectToPage();
        }
    }
}