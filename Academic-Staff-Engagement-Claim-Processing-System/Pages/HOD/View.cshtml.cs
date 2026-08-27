using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages.HOD
{
    public class ViewModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public ViewModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Lecturer? Lecturer { get; set; }

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            Lecturer = await _context.Lecturers
                .Include(l => l.CourseAssignments)
                    .ThenInclude(ca => ca.Course)
                .Include(l => l.Contracts)
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == id.Value);

            if (Lecturer == null)
            {
                return NotFound();
            }

            return Page();
        }
    }
}