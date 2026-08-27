using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages.HOD
{
    public class EditModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public EditModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public int LecturerId { get; set; }

        [BindProperty]
        public string UserName { get; set; } = string.Empty;

        [BindProperty]
        public string Email { get; set; } = string.Empty;

        [BindProperty]
        public string PhoneNumber { get; set; } = string.Empty;

        [BindProperty]
        public UserRole Type { get; set; }

        [BindProperty]
        public LecturerRank? Rank { get; set; }

        [BindProperty]
        public bool IsActive { get; set; }

        public Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Lecturer? Lecturer { get; set; }

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            Lecturer = await _context.Lecturers
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == id.Value);

            if (Lecturer == null)
            {
                return NotFound();
            }

            LecturerId = Lecturer.Id;
            UserName = Lecturer.UserName;
            Email = Lecturer.Email;
            PhoneNumber = Lecturer.PhoneNumber;
            Type = Lecturer.Type;
            Rank = Lecturer.Rank;
            IsActive = Lecturer.IsActive;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var lecturer = await _context.Lecturers
                .FirstOrDefaultAsync(l => l.Id == LecturerId);

            if (lecturer == null)
            {
                return NotFound();
            }

            bool duplicateUsername = await _context.Lecturers
                .AnyAsync(l =>
                    l.Id != LecturerId &&
                    l.UserName == UserName);

            if (duplicateUsername)
            {
                ModelState.AddModelError(
                    nameof(UserName),
                    "This username is already being used by another lecturer.");

                return Page();
            }

            bool duplicateEmail = await _context.Lecturers
                .AnyAsync(l =>
                    l.Id != LecturerId &&
                    l.Email == Email);

            if (duplicateEmail)
            {
                ModelState.AddModelError(
                    nameof(Email),
                    "This email address is already being used by another lecturer.");

                return Page();
            }

            lecturer.UserName = UserName.Trim();
            lecturer.Email = Email.Trim();
            lecturer.PhoneNumber = PhoneNumber?.Trim() ?? string.Empty;
            lecturer.Type = Type;
            lecturer.Rank = Rank;
            lecturer.IsActive = IsActive;
            lecturer.UpdatedAtUtc = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return RedirectToPage(
                "/HOD/View",
                new { id = LecturerId });
        }
    }
}