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

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var lecturer = await _context.Lecturers
                .AsNoTracking()
                .Where(l => l.Id == id.Value)
                .Select(l => new
                {
                    l.Id,
                    l.UserName,
                    l.Email,
                    l.PhoneNumber,
                    l.Type,
                    l.Rank,
                    l.IsActive
                })
                .FirstOrDefaultAsync();

            if (lecturer == null)
            {
                return NotFound();
            }

            LecturerId = lecturer.Id;
            UserName = lecturer.UserName;
            Email = lecturer.Email;
            PhoneNumber = lecturer.PhoneNumber;
            Type = lecturer.Type;
            Rank = lecturer.Rank;
            IsActive = lecturer.IsActive;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            UserName = UserName?.Trim() ?? string.Empty;
            Email = Email?.Trim() ?? string.Empty;
            PhoneNumber = PhoneNumber?.Trim() ?? string.Empty;

            if (!ModelState.IsValid)
            {
                return Page();
            }

            // Check that the lecturer exists without loading
            // the encrypted GovernmentIdEncrypted property.
            var lecturerExists = await _context.Lecturers
                .AsNoTracking()
                .AnyAsync(l => l.Id == LecturerId);

            if (!lecturerExists)
            {
                return NotFound();
            }

            // Check duplicate username.
            var duplicateUsername = await _context.Lecturers
                .AsNoTracking()
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

            // Check duplicate email.
            var duplicateEmail = await _context.Lecturers
                .AsNoTracking()
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

            /*
             * Do NOT load the existing Lecturer entity here.
             *
             * Loading it would cause EF Core to decrypt
             * GovernmentIdEncrypted and produce the missing-key error.
             *
             * Instead, attach a lightweight Lecturer entity and mark
             * only the editable properties as modified.
             */

            var lecturer = new Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Lecturer(
                LecturerId,
                UserName,
                Email);

            lecturer.PhoneNumber = PhoneNumber;
            lecturer.Type = Type;
            lecturer.Rank = Rank;
            lecturer.IsActive = IsActive;
            lecturer.UpdatedAtUtc = DateTime.UtcNow;

            _context.Lecturers.Attach(lecturer);

            _context.Entry(lecturer)
                .Property(l => l.UserName)
                .IsModified = true;

            _context.Entry(lecturer)
                .Property(l => l.Email)
                .IsModified = true;

            _context.Entry(lecturer)
                .Property(l => l.PhoneNumber)
                .IsModified = true;

            _context.Entry(lecturer)
                .Property(l => l.Type)
                .IsModified = true;

            _context.Entry(lecturer)
                .Property(l => l.Rank)
                .IsModified = true;

            _context.Entry(lecturer)
                .Property(l => l.IsActive)
                .IsModified = true;

            _context.Entry(lecturer)
                .Property(l => l.UpdatedAtUtc)
                .IsModified = true;

            await _context.SaveChangesAsync();

            return RedirectToPage(
                "/HOD/View",
                new { id = LecturerId });
        }
    }
}

