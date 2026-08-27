using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
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

        public LecturerViewModel? Lecturer { get; set; }

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            Lecturer = await _context.Lecturers
                .AsNoTracking()
                .Where(l => l.Id == id.Value)
                .Select(l => new LecturerViewModel
                {
                    Id = l.Id,
                    UserName = l.UserName,
                    Email = l.Email,
                    PhoneNumber = l.PhoneNumber,
                    Type = l.Type,
                    Rank = l.Rank,
                    IsActive = l.IsActive,
                    SignatureStatus = l.SignatureStatus,
                    CreatedAtUtc = l.CreatedAtUtc,
                    LastLoginUtc = l.LastLoginUtc,
                    FailedLoginAttempts = l.FailedLoginAttempts
                })
                .FirstOrDefaultAsync();

            if (Lecturer == null)
            {
                return NotFound();
            }

            return Page();
        }

        public class LecturerViewModel
        {
            public int Id { get; set; }

            public string UserName { get; set; } = string.Empty;

            public string Email { get; set; } = string.Empty;

            public string PhoneNumber { get; set; } = string.Empty;

            public UserRole Type { get; set; }

            public LecturerRank? Rank { get; set; }

            public bool IsActive { get; set; }

            public SignatureStatus SignatureStatus { get; set; }

            public DateTime CreatedAtUtc { get; set; }

            public DateTime? LastLoginUtc { get; set; }

            public int FailedLoginAttempts { get; set; }
        }
    }
}

