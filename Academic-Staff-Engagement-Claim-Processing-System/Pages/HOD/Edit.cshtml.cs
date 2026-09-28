using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages.HOD
{
    [Authorize(Roles = "HOD")]
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
        [Required]
        [StringLength(100)]
        public string UserName { get; set; } = string.Empty;

        [BindProperty]
        [Required]
        [StringLength(150)]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [BindProperty]
        [StringLength(20)]
        public string PhoneNumber { get; set; } = string.Empty;

        [BindProperty]
        [Required]
        public UserRole Type { get; set; }

        [BindProperty]
        public LecturerRank? Rank { get; set; }

        [BindProperty]
        public string Department { get; set; } = string.Empty;

        [BindProperty]
        public bool IsActive { get; set; }

        public Faculty Faculty { get; private set; }

        public string FacultyDisplayName =>
            GetEnumDisplayName(Faculty);

        public IReadOnlyList<DepartmentOption> Departments { get; private set; }
            = Array.Empty<DepartmentOption>();

        public IReadOnlyList<CountryCodeOption> CountryCodes { get; } =
            new List<CountryCodeOption>
            {
                new("Afghanistan", "+93", "AF"),
                new("Albania", "+355", "AL"),
                new("Algeria", "+213", "DZ"),
                new("Andorra", "+376", "AD"),
                new("Angola", "+244", "AO"),
                new("Argentina", "+54", "AR"),
                new("Armenia", "+374", "AM"),
                new("Australia", "+61", "AU"),
                new("Austria", "+43", "AT"),
                new("Azerbaijan", "+994", "AZ"),
                new("Bahamas", "+1", "BS"),
                new("Bahrain", "+973", "BH"),
                new("Bangladesh", "+880", "BD"),
                new("Barbados", "+1", "BB"),
                new("Belarus", "+375", "BY"),
                new("Belgium", "+32", "BE"),
                new("Belize", "+501", "BZ"),
                new("Benin", "+229", "BJ"),
                new("Bhutan", "+975", "BT"),
                new("Bolivia", "+591", "BO"),
                new("Bosnia and Herzegovina", "+387", "BA"),
                new("Botswana", "+267", "BW"),
                new("Brazil", "+55", "BR"),
                new("Brunei", "+673", "BN"),
                new("Bulgaria", "+359", "BG"),
                new("Burkina Faso", "+226", "BF"),
                new("Burundi", "+257", "BI"),
                new("Cambodia", "+855", "KH"),
                new("Cameroon", "+237", "CM"),
                new("Canada", "+1", "CA"),
                new("Cape Verde", "+238", "CV"),
                new("Central African Republic", "+236", "CF"),
                new("Chad", "+235", "TD"),
                new("Chile", "+56", "CL"),
                new("China", "+86", "CN"),
                new("Colombia", "+57", "CO"),
                new("Comoros", "+269", "KM"),
                new("Congo", "+242", "CG"),
                new("Costa Rica", "+506", "CR"),
                new("Croatia", "+385", "HR"),
                new("Cuba", "+53", "CU"),
                new("Cyprus", "+357", "CY"),
                new("Czech Republic", "+420", "CZ"),
                new("Denmark", "+45", "DK"),
                new("Djibouti", "+253", "DJ"),
                new("Dominica", "+1", "DM"),
                new("Dominican Republic", "+1", "DO"),
                new("Ecuador", "+593", "EC"),
                new("Egypt", "+20", "EG"),
                new("El Salvador", "+503", "SV"),
                new("Equatorial Guinea", "+240", "GQ"),
                new("Eritrea", "+291", "ER"),
                new("Estonia", "+372", "EE"),
                new("Eswatini", "+268", "SZ"),
                new("Ethiopia", "+251", "ET"),
                new("Fiji", "+679", "FJ"),
                new("Finland", "+358", "FI"),
                new("France", "+33", "FR"),
                new("Gabon", "+241", "GA"),
                new("Gambia", "+220", "GM"),
                new("Georgia", "+995", "GE"),
                new("Germany", "+49", "DE"),
                new("Ghana", "+233", "GH"),
                new("Greece", "+30", "GR"),
                new("Grenada", "+1", "GD"),
                new("Guatemala", "+502", "GT"),
                new("Guinea", "+224", "GN"),
                new("Guinea-Bissau", "+245", "GW"),
                new("Guyana", "+592", "GY"),
                new("Haiti", "+509", "HT"),
                new("Honduras", "+504", "HN"),
                new("Hungary", "+36", "HU"),
                new("Iceland", "+354", "IS"),
                new("India", "+91", "IN"),
                new("Indonesia", "+62", "ID"),
                new("Iran", "+98", "IR"),
                new("Iraq", "+964", "IQ"),
                new("Ireland", "+353", "IE"),
                new("Israel", "+972", "IL"),
                new("Italy", "+39", "IT"),
                new("Jamaica", "+1", "JM"),
                new("Japan", "+81", "JP"),
                new("Jordan", "+962", "JO"),
                new("Kazakhstan", "+7", "KZ"),
                new("Kenya", "+254", "KE"),
                new("Kiribati", "+686", "KI"),
                new("Kuwait", "+965", "KW"),
                new("Kyrgyzstan", "+996", "KG"),
                new("Laos", "+856", "LA"),
                new("Latvia", "+371", "LV"),
                new("Lebanon", "+961", "LB"),
                new("Lesotho", "+266", "LS"),
                new("Liberia", "+231", "LR"),
                new("Libya", "+218", "LY"),
                new("Liechtenstein", "+423", "LI"),
                new("Lithuania", "+370", "LT"),
                new("Luxembourg", "+352", "LU"),
                new("Madagascar", "+261", "MG"),
                new("Malawi", "+265", "MW"),
                new("Malaysia", "+60", "MY"),
                new("Maldives", "+960", "MV"),
                new("Mali", "+223", "ML"),
                new("Malta", "+356", "MT"),
                new("Marshall Islands", "+692", "MH"),
                new("Mauritania", "+222", "MR"),
                new("Mauritius", "+230", "MU"),
                new("Mexico", "+52", "MX"),
                new("Micronesia", "+691", "FM"),
                new("Moldova", "+373", "MD"),
                new("Monaco", "+377", "MC"),
                new("Mongolia", "+976", "MN"),
                new("Montenegro", "+382", "ME"),
                new("Morocco", "+212", "MA"),
                new("Mozambique", "+258", "MZ"),
                new("Myanmar", "+95", "MM"),
                new("Namibia", "+264", "NA"),
                new("Nauru", "+674", "NR"),
                new("Nepal", "+977", "NP"),
                new("Netherlands", "+31", "NL"),
                new("New Zealand", "+64", "NZ"),
                new("Nicaragua", "+505", "NI"),
                new("Niger", "+227", "NE"),
                new("Nigeria", "+234", "NG"),
                new("North Korea", "+850", "KP"),
                new("North Macedonia", "+389", "MK"),
                new("Norway", "+47", "NO"),
                new("Oman", "+968", "OM"),
                new("Pakistan", "+92", "PK"),
                new("Palau", "+680", "PW"),
                new("Palestine", "+970", "PS"),
                new("Panama", "+507", "PA"),
                new("Papua New Guinea", "+675", "PG"),
                new("Paraguay", "+595", "PY"),
                new("Peru", "+51", "PE"),
                new("Philippines", "+63", "PH"),
                new("Poland", "+48", "PL"),
                new("Portugal", "+351", "PT"),
                new("Qatar", "+974", "QA"),
                new("Romania", "+40", "RO"),
                new("Russia", "+7", "RU"),
                new("Rwanda", "+250", "RW"),
                new("Saint Kitts and Nevis", "+1", "KN"),
                new("Saint Lucia", "+1", "LC"),
                new("Saint Vincent and the Grenadines", "+1", "VC"),
                new("Samoa", "+685", "WS"),
                new("San Marino", "+378", "SM"),
                new("Sao Tome and Principe", "+239", "ST"),
                new("Saudi Arabia", "+966", "SA"),
                new("Senegal", "+221", "SN"),
                new("Serbia", "+381", "RS"),
                new("Seychelles", "+248", "SC"),
                new("Sierra Leone", "+232", "SL"),
                new("Singapore", "+65", "SG"),
                new("Slovakia", "+421", "SK"),
                new("Slovenia", "+386", "SI"),
                new("Solomon Islands", "+677", "SB"),
                new("Somalia", "+252", "SO"),
                new("South Africa", "+27", "ZA"),
                new("South Korea", "+82", "KR"),
                new("South Sudan", "+211", "SS"),
                new("Spain", "+34", "ES"),
                new("Sri Lanka", "+94", "LK"),
                new("Sudan", "+249", "SD"),
                new("Suriname", "+597", "SR"),
                new("Sweden", "+46", "SE"),
                new("Switzerland", "+41", "CH"),
                new("Syria", "+963", "SY"),
                new("Taiwan", "+886", "TW"),
                new("Tajikistan", "+992", "TJ"),
                new("Tanzania", "+255", "TZ"),
                new("Thailand", "+66", "TH"),
                new("Timor-Leste", "+670", "TL"),
                new("Togo", "+228", "TG"),
                new("Tonga", "+676", "TO"),
                new("Trinidad and Tobago", "+1", "TT"),
                new("Tunisia", "+216", "TN"),
                new("Turkey", "+90", "TR"),
                new("Turkmenistan", "+993", "TM"),
                new("Tuvalu", "+688", "TV"),
                new("Uganda", "+256", "UG"),
                new("Ukraine", "+380", "UA"),
                new("United Arab Emirates", "+971", "AE"),
                new("United Kingdom", "+44", "GB"),
                new("United States", "+1", "US"),
                new("Uruguay", "+598", "UY"),
                new("Uzbekistan", "+998", "UZ"),
                new("Vanuatu", "+678", "VU"),
                new("Vatican City", "+39", "VA"),
                new("Venezuela", "+58", "VE"),
                new("Vietnam", "+84", "VN"),
                new("Yemen", "+967", "YE"),
                new("Zambia", "+260", "ZM"),
                new("Zimbabwe", "+263", "ZW")
            };

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var hod = await GetCurrentHodAsync();

            if (hod == null)
            {
                return Forbid();
            }

            Faculty = hod.Faculty;
            LoadDepartments();

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
                    l.Faculty,
                    l.Department,
                    l.IsActive
                })
                .FirstOrDefaultAsync();

            if (lecturer == null)
            {
                return NotFound();
            }

            if (lecturer.Faculty != hod.Faculty)
            {
                return Forbid();
            }

            LecturerId = lecturer.Id;
            UserName = lecturer.UserName;
            Email = lecturer.Email;
            PhoneNumber = lecturer.PhoneNumber;
            Type = lecturer.Type;
            Rank = lecturer.Rank;
            Department = lecturer.Department;
            IsActive = lecturer.IsActive;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            UserName = UserName?.Trim() ?? string.Empty;
            Email = Email?.Trim() ?? string.Empty;
            PhoneNumber = NormalizePhoneNumber(PhoneNumber);
            Department = Department?.Trim() ?? string.Empty;

            var hod = await GetCurrentHodAsync();

            if (hod == null)
            {
                return Forbid();
            }

            Faculty = hod.Faculty;
            LoadDepartments();

            if (!ModelState.IsValid)
            {
                return Page();
            }

            var lecturer = await _context.Lecturers
                .AsNoTracking()
                .Where(l => l.Id == LecturerId)
                .Select(l => new
                {
                    l.Id,
                    l.Faculty
                })
                .FirstOrDefaultAsync();

            if (lecturer == null)
            {
                return NotFound();
            }

            if (lecturer.Faculty != hod.Faculty)
            {
                return Forbid();
            }

            if (!Enum.TryParse<Department>(
                    Department,
                    true,
                    out var selectedDepartment) ||
                !FacultyDepartments.IsValidDepartment(
                    hod.Faculty,
                    selectedDepartment))
            {
                ModelState.AddModelError(
                    nameof(Department),
                    "The selected department does not belong to your faculty.");

                return Page();
            }

            if (Type != UserRole.PartTimeLecturer &&
                Type != UserRole.FullTimeLecturer)
            {
                ModelState.AddModelError(
                    nameof(Type),
                    "Select a valid lecturer type.");

                return Page();
            }

            if (!string.IsNullOrWhiteSpace(PhoneNumber))
            {
                var phoneDigits = new string(
                    PhoneNumber.Where(char.IsDigit).ToArray());

                if (phoneDigits.Length < 7 ||
                    phoneDigits.Length > 15)
                {
                    ModelState.AddModelError(
                        nameof(PhoneNumber),
                        "Enter a valid international phone number.");

                    return Page();
                }

                if (!PhoneNumber.StartsWith("+"))
                {
                    ModelState.AddModelError(
                        nameof(PhoneNumber),
                        "The phone number must include an international country code.");

                    return Page();
                }
            }

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

            var duplicateAdminUsername = await _context.AdminAccounts
                .AsNoTracking()
                .AnyAsync(a => a.UserName == UserName);

            if (duplicateAdminUsername)
            {
                ModelState.AddModelError(
                    nameof(UserName),
                    "This username is already being used by an administrator.");

                return Page();
            }

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

            var duplicateAdminEmail = await _context.AdminAccounts
                .AsNoTracking()
                .AnyAsync(a => a.Email == Email);

            if (duplicateAdminEmail)
            {
                ModelState.AddModelError(
                    nameof(Email),
                    "This email address is already being used by an administrator.");

                return Page();
            }

            var lecturerEntity = await _context.Lecturers
                .FirstOrDefaultAsync(l => l.Id == LecturerId);

            if (lecturerEntity == null)
            {
                return NotFound();
            }

            if (lecturerEntity.Faculty != hod.Faculty)
            {
                return Forbid();
            }

            lecturerEntity.UserName = UserName;
            lecturerEntity.Email = Email;
            lecturerEntity.PhoneNumber = PhoneNumber;
            lecturerEntity.Type = Type;
            lecturerEntity.Rank = Rank;
            lecturerEntity.Faculty = hod.Faculty;
            lecturerEntity.Department = Department;
            lecturerEntity.IsActive = IsActive;
            lecturerEntity.UpdatedAtUtc = DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "This lecturer record was modified or deleted by another user. Please reload the page and try again.");

                LoadDepartments();

                return Page();
            }

            TempData["SuccessMessage"] =
                $"Lecturer record for '{lecturerEntity.UserName}' was updated successfully.";

            return RedirectToPage(
                "/HOD/View",
                new { id = LecturerId });
        }

        private async Task<Hod?> GetCurrentHodAsync()
        {
            var userName = User.Identity?.Name;

            if (string.IsNullOrWhiteSpace(userName))
            {
                return null;
            }

            return await _context.Hods
                .AsNoTracking()
                .FirstOrDefaultAsync(h =>
                    h.UserName == userName &&
                    h.IsActive);
        }

        private void LoadDepartments()
        {
            Departments = FacultyDepartments
                .GetDepartments(Faculty)
                .Select(department => new DepartmentOption(
                    department,
                    GetEnumDisplayName(department)))
                .ToList();
        }

        private static string NormalizePhoneNumber(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var cleaned = value.Trim();

            var hasPlus = cleaned.StartsWith("+");

            var digits = new string(
                cleaned.Where(char.IsDigit).ToArray());

            if (digits.Length == 0)
            {
                return string.Empty;
            }

            return hasPlus ? "+" + digits : digits;
        }

        private static string GetEnumDisplayName<TEnum>(TEnum value)
            where TEnum : struct, Enum
        {
            var member = typeof(TEnum)
                .GetMember(value.ToString())
                .FirstOrDefault();

            var displayAttribute = member?
                .GetCustomAttributes(
                    typeof(DisplayAttribute),
                    false)
                .Cast<DisplayAttribute>()
                .FirstOrDefault();

            return displayAttribute?.GetName()
                ?? SplitPascalCase(value.ToString());
        }

        private static string SplitPascalCase(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            var result = new System.Text.StringBuilder();

            for (var i = 0; i < value.Length; i++)
            {
                if (i > 0 &&
                    char.IsUpper(value[i]) &&
                    !char.IsUpper(value[i - 1]))
                {
                    result.Append(' ');
                }

                result.Append(value[i]);
            }

            return result.ToString();
        }

        public sealed record CountryCodeOption(
            string Name,
            string Code,
            string IsoCode);

        public sealed record DepartmentOption(
            Department Value,
            string Name);
    }
}