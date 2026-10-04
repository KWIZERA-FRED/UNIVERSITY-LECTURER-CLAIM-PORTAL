using System.Globalization;
using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Academic_Staff_Engagement_Claim_Processing_System.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages.HOD
{
    [Authorize(Roles = "HOD")]
    public class IndexModel : PageModel
    {
        private const int ClosedWindowDays = 30;
        private const int MaxRowsShown = 300;

        private readonly ApplicationDbContext _context;

        private string[] _searchTerms = Array.Empty<string>();

        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }


        // ============================================================
        // QUERY STRING
        // ============================================================

        [BindProperty(SupportsGet = true)]
        public string? Filter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Q { get; set; }

        public string ActiveFilter { get; private set; } = "needs-action";


        // ============================================================
        // HOD IDENTITY
        // ============================================================

        public int CurrentHodId { get; private set; }

        public string DisplayName { get; private set; } = "there";

        public string HodFaculty { get; private set; } = "Faculty";

        // "ComputingAndInformationSciences" → "Computing & Information Sciences"
        public string HodFacultyDisplay { get; private set; } = "Faculty";


        // ============================================================
        // SUMMARY — claims queue
        // ============================================================

        public int ReadyCount { get; private set; }

        public int WaitingCount { get; private set; }

        public int WithLaterCount { get; private set; }

        public int ClosedCount { get; private set; }

        public int AllCount { get; private set; }

        public int PipelineCount => WaitingCount + WithLaterCount;

        public decimal ReadyValue { get; private set; }

        public string OldestReadyText { get; private set; } = string.Empty;

        public QueueItem? NextAction { get; private set; }

        public string NextActionWaitText { get; private set; } = string.Empty;


        // ============================================================
        // SUMMARY — faculty snapshot (informational)
        // ============================================================

        public int ActiveContractsCount { get; private set; }

        public int PendingSignatureContractsCount { get; private set; }

        public int AcademicStaffCount { get; private set; }


        // ============================================================
        // QUEUE + ACTIVITY
        // ============================================================

        public List<QueueItem> Items { get; private set; } = new();

        public bool IsTruncated { get; private set; }

        public List<ActivityItem> Activity { get; private set; } = new();


        // ============================================================
        // VIEW MODELS
        // ============================================================

        public enum QueueKind
        {
            Claim,
            Contract
        }

        public enum QueueBucket
        {
            NeedsAction = 0,
            WaitingOnEarlier = 1,
            WithLater = 2,
            Closed = 3
        }

        public sealed class QueueItem
        {
            public QueueKind Kind { get; set; }

            public int Id { get; set; }

            public string Reference { get; set; } = string.Empty;

            public string LecturerName { get; set; } = string.Empty;

            public string CourseCode { get; set; } = string.Empty;

            public string CourseTitle { get; set; } = string.Empty;

            public decimal Hours { get; set; }

            public decimal Amount { get; set; }

            public QueueBucket Bucket { get; set; }

            public string StatusText { get; set; } = string.Empty;

            public string PillTone { get; set; } = "info";

            public string WaitText { get; set; } = string.Empty;

            public string WaitTone { get; set; } = "ds-tone-normal";

            public DateTime? SinceUtc { get; set; }

            public DateTime LastActivityUtc { get; set; }

            public int? DaysWaiting { get; set; }

            public List<WorkflowStep> Journey { get; set; } = new();

            public string ReviewUrl { get; set; } = "#";
        }

        public sealed class ActivityItem
        {
            public string Verb { get; set; } = string.Empty;

            public string Reference { get; set; } = string.Empty;

            public string LecturerName { get; set; } = string.Empty;

            public DateTime AtUtc { get; set; }

            public string WhenText { get; set; } = string.Empty;

            public bool Positive { get; set; }
        }

        private enum Outcome
        {
            Pending,
            Done,
            Rejected
        }

        private sealed record StepRaw(
            int Order,
            string Label,
            string ShortLabel,
            Outcome Outcome,
            DateTime? AtUtc,
            bool IsYou,
            string? DoneVerb = null);


        // ============================================================
        // GET
        // ============================================================

        public async Task<IActionResult> OnGetAsync()
        {
            var username = User.Identity?.Name;

            if (string.IsNullOrWhiteSpace(username))
            {
                return RedirectToPage("/Login");
            }

            var hod = await _context.Hods
                .AsNoTracking()
                .FirstOrDefaultAsync(h =>
                    h.UserName == username &&
                    h.IsActive);

            if (hod is null)
            {
                return RedirectToPage("/Login");
            }

            CurrentHodId = hod.Id;
            DisplayName = hod.UserName;
            HodFaculty = hod.Faculty.ToString();
            HodFacultyDisplay = FacultyDisplayName(hod.Faculty);

            // Prefer the login claim if present; fall back to the
            // matched HOD account id.
            if (int.TryParse(
                    User.FindFirst("UserId")?.Value,
                    out int claimId) &&
                claimId > 0)
            {
                CurrentHodId = claimId;
            }

            var nowUtc = DateTime.UtcNow;
            var cutoffUtc = nowUtc.AddDays(-ClosedWindowDays);

            var facultyDepartments = FacultyDepartments
                .GetDepartments(hod.Faculty)
                .Select(d => d.ToString())
                .ToList();

            var all = await LoadClaimItemsAsync(
                facultyDepartments,
                nowUtc,
                cutoffUtc);

            var open = all
                .Where(i => i.Bucket != QueueBucket.Closed)
                .OrderBy(i => (int)i.Bucket)
                .ThenBy(i => i.SinceUtc ?? DateTime.MaxValue);

            var closed = all
                .Where(i => i.Bucket == QueueBucket.Closed)
                .OrderByDescending(i => i.LastActivityUtc);

            var ordered = open.Concat(closed).ToList();

            ReadyCount = ordered.Count(i => i.Bucket == QueueBucket.NeedsAction);
            WaitingCount = ordered.Count(i => i.Bucket == QueueBucket.WaitingOnEarlier);
            WithLaterCount = ordered.Count(i => i.Bucket == QueueBucket.WithLater);
            ClosedCount = ordered.Count(i => i.Bucket == QueueBucket.Closed);
            AllCount = ordered.Count;

            ReadyValue = ordered
                .Where(i => i.Bucket == QueueBucket.NeedsAction)
                .Sum(i => i.Amount);

            NextAction = ordered
                .FirstOrDefault(i => i.Bucket == QueueBucket.NeedsAction);

            if (NextAction is not null)
            {
                OldestReadyText = AgeWords(NextAction.DaysWaiting);

                NextActionWaitText =
                    NextAction.DaysWaiting is null or 0
                        ? "since today"
                        : $"for {AgeWords(NextAction.DaysWaiting)}";
            }

            ActiveFilter = NormalizeFilter(Filter);

            _searchTerms =
                (Q ?? string.Empty)
                    .ToLowerInvariant()
                    .Split(
                        ' ',
                        StringSplitOptions.RemoveEmptyEntries |
                        StringSplitOptions.TrimEntries);

            IsTruncated = ordered.Count > MaxRowsShown;
            Items = ordered.Take(MaxRowsShown).ToList();

            // Faculty snapshot — informational counts.
            ActiveContractsCount = await _context.Contracts
                .AsNoTracking()
                .CountAsync(c =>
                    c.CourseAssignment != null &&
                    facultyDepartments.Contains(c.CourseAssignment.Course.Department) &&
                    c.Status == ContractStatus.Active);

            PendingSignatureContractsCount = await _context.Contracts
                .AsNoTracking()
                .CountAsync(c =>
                    c.CourseAssignment != null &&
                    facultyDepartments.Contains(c.CourseAssignment.Course.Department) &&
                    c.Status == ContractStatus.PendingSignature);

            AcademicStaffCount = await _context.CourseAssignments
                .AsNoTracking()
                .Where(ca =>
                    ca.IsActive &&
                    facultyDepartments.Contains(ca.Course.Department) &&
                    ca.Lecturer.IsActive)
                .Select(ca => ca.LecturerId)
                .Distinct()
                .CountAsync();

            Activity = await LoadActivityAsync(CurrentHodId);

            return Page();
        }


        // ============================================================
        // LIVE-FILTER SUPPORT
        // ============================================================

        public string BucketKey(QueueItem item) =>
            item.Bucket switch
            {
                QueueBucket.NeedsAction => "needs",
                QueueBucket.WaitingOnEarlier => "waiting",
                QueueBucket.WithLater => "later",
                _ => "closed"
            };

        public string SearchKey(QueueItem item) =>
            $"{item.Reference} {item.LecturerName} {item.CourseCode} {item.CourseTitle}"
                .ToLowerInvariant();

        public bool IsVisibleOnLoad(QueueItem item)
        {
            var inFilter = ActiveFilter switch
            {
                "pipeline" =>
                    item.Bucket == QueueBucket.WaitingOnEarlier ||
                    item.Bucket == QueueBucket.WithLater,

                "closed" =>
                    item.Bucket == QueueBucket.Closed,

                "all" => true,

                _ => item.Bucket == QueueBucket.NeedsAction
            };

            if (!inFilter)
                return false;

            var key = SearchKey(item);

            return _searchTerms.All(t =>
                key.Contains(t, StringComparison.Ordinal));
        }


        // ============================================================
        // CLAIMS — the only queue HOD can act on
        // ============================================================

        private async Task<List<QueueItem>> LoadClaimItemsAsync(
            List<string> facultyDepartments,
            DateTime nowUtc,
            DateTime cutoffUtc)
        {
            var ids = await _context.ClaimApprovals
                .AsNoTracking()
                .Where(a =>
                    a.ApprovalRole == ApprovalRole.HOD &&
                    a.Claim.CourseAssignment != null &&
                    facultyDepartments.Contains(
                        a.Claim.CourseAssignment.Course.Department) &&
                    (a.Decision == ApprovalDecision.Pending ||
                     (a.DecidedAtUtc != null && a.DecidedAtUtc >= cutoffUtc) ||
                     a.Claim.CreatedAtUtc >= cutoffUtc))
                .Select(a => a.ClaimId)
                .Distinct()
                .ToListAsync();

            var result = new List<QueueItem>();

            if (ids.Count == 0)
            {
                return result;
            }

            var claims = await _context.Claims
                .AsNoTracking()
                .Where(c => ids.Contains(c.Id))
                .Select(c => new
                {
                    c.Id,
                    c.HoursClaimed,
                    c.Amount,
                    c.CreatedAtUtc,
                    c.SubmittedAtUtc,
                    c.Status,

                    Lecturer = c.CourseAssignment.Lecturer.UserName,
                    CourseTitle = c.CourseAssignment.Course.Title,
                    CourseCode = c.CourseAssignment.Course.Code
                })
                .ToListAsync();

            var steps = await _context.ClaimApprovals
                .AsNoTracking()
                .Where(a => ids.Contains(a.ClaimId))
                .Select(a => new
                {
                    a.ClaimId,
                    a.SequenceOrder,
                    a.ApprovalRole,
                    a.Decision,
                    a.DecidedAtUtc
                })
                .ToListAsync();

            foreach (var claim in claims)
            {
                var raw = new List<StepRaw>
                {
                    new StepRaw(
                        0,
                        "Lecturer",
                        "Lecturer",
                        claim.SubmittedAtUtc is null
                            ? Outcome.Pending
                            : Outcome.Done,
                        claim.SubmittedAtUtc ?? claim.CreatedAtUtc,
                        false,
                        "Submitted")
                };

                raw.AddRange(
                    steps
                        .Where(s => s.ClaimId == claim.Id)
                        .Select(s => new StepRaw(
                            s.SequenceOrder,
                            ApproverLabel(s.ApprovalRole),
                            ApproverShort(s.ApprovalRole),
                            s.Decision switch
                            {
                                ApprovalDecision.Approved => Outcome.Done,
                                ApprovalDecision.Rejected => Outcome.Rejected,
                                _ => Outcome.Pending
                            },
                            s.DecidedAtUtc,
                            s.ApprovalRole == ApprovalRole.HOD)));

                var item = Assemble(
                    QueueKind.Claim,
                    claim.Id,
                    claim.Lecturer,
                    claim.CourseCode,
                    claim.CourseTitle,
                    claim.HoursClaimed,
                    claim.Amount,
                    claim.CreatedAtUtc,
                    raw,
                    nowUtc);

                if (item.Bucket == QueueBucket.Closed &&
                    item.LastActivityUtc < cutoffUtc)
                    continue;

                result.Add(item);
            }

            return result;
        }


        // ============================================================
        // TURN RAW STEPS INTO A QUEUE ITEM
        // ============================================================

        private QueueItem Assemble(
            QueueKind kind,
            int id,
            string lecturer,
            string courseCode,
            string courseTitle,
            decimal hours,
            decimal amount,
            DateTime createdUtc,
            List<StepRaw> steps,
            DateTime nowUtc)
        {
            var ordered = steps
                .OrderBy(s => s.Order)
                .ToList();

            var rejected = ordered
                .FirstOrDefault(s => s.Outcome == Outcome.Rejected);

            var current = rejected is null
                ? ordered.FirstOrDefault(s => s.Outcome == Outcome.Pending)
                : null;

            var hod = ordered.FirstOrDefault(s => s.IsYou);

            var completed = rejected is null && current is null;

            // ---- journey ----

            var journey = new List<WorkflowStep>();

            foreach (var s in ordered)
            {
                var state = s.Outcome switch
                {
                    Outcome.Done => StepState.Done,
                    Outcome.Rejected => StepState.Rejected,
                    _ => ReferenceEquals(s, current)
                        ? StepState.Current
                        : StepState.Waiting
                };

                journey.Add(new WorkflowStep(
                    s.Label,
                    s.ShortLabel,
                    state,
                    BuildTip(s, state, kind),
                    s.IsYou));
            }

            // ---- bucket + wording ----

            QueueBucket bucket;
            string status;
            string tone;

            if (rejected is not null)
            {
                bucket = QueueBucket.Closed;
                tone = "rejected";
                status = $"Rejected by {rejected.Label}";
            }
            else if (completed)
            {
                bucket = QueueBucket.Closed;
                tone = "done";
                status = "Fully approved";
            }
            else if (current!.IsYou)
            {
                bucket = QueueBucket.NeedsAction;
                tone = "current";
                status = "Needs your approval";
            }
            else if (hod is not null && hod.Outcome == Outcome.Done)
            {
                bucket = QueueBucket.WithLater;
                tone = "info";
                status = $"With {current.Label}";
            }
            else
            {
                bucket = QueueBucket.WaitingOnEarlier;
                tone = "waiting";
                status = $"Waiting for {current.Label}";
            }

            // ---- timing ----

            var lastDone = ordered
                .Where(s => s.Outcome == Outcome.Done && s.AtUtc.HasValue)
                .Select(s => s.AtUtc!.Value)
                .DefaultIfEmpty(createdUtc)
                .Max();

            var lastActivity = ordered
                .Where(s => s.AtUtc.HasValue)
                .Select(s => s.AtUtc!.Value)
                .DefaultIfEmpty(createdUtc)
                .Append(createdUtc)
                .Max();

            DateTime? since = null;
            int? days = null;
            string waitText;
            string waitTone = "ds-tone-normal";

            if (bucket == QueueBucket.Closed)
            {
                waitText = $"Updated {Ago(lastActivity)}";
            }
            else
            {
                since = lastDone;

                days = Math.Max(
                    0,
                    (int)Math.Floor((nowUtc - lastDone).TotalDays));

                waitText = days == 0
                    ? "Waiting since today"
                    : $"Waiting {AgeWords(days)}";

                waitTone = days >= 7
                    ? "ds-tone-late"
                    : days >= 3
                        ? "ds-tone-warn"
                        : "ds-tone-normal";
            }

            return new QueueItem
            {
                Kind = kind,
                Id = id,

                Reference = $"CLM-{id:D6}",

                LecturerName = lecturer,
                CourseCode = courseCode,
                CourseTitle = courseTitle,
                Hours = hours,
                Amount = amount,

                Bucket = bucket,
                StatusText = status,
                PillTone = tone,

                WaitText = waitText,
                WaitTone = waitTone,
                SinceUtc = since,
                LastActivityUtc = lastActivity,
                DaysWaiting = days,

                Journey = journey,

                ReviewUrl =
                    Url.Page("/HOD/ClaimDetails", new { claimId = id })
                    ?? "#"
            };
        }

        private static string BuildTip(
            StepRaw step,
            StepState state,
            QueueKind kind)
        {
            var label = step.IsYou
                ? $"{step.Label} (you)"
                : step.Label;

            var verb = state switch
            {
                StepState.Done => step.DoneVerb ?? "Approved",
                StepState.Rejected => "Rejected",
                StepState.Current => "Current step",
                _ => "Waiting"
            };

            var when =
                (state == StepState.Done || state == StepState.Rejected) &&
                step.AtUtc.HasValue
                    ? " · " + step.AtUtc.Value.ToLocalTime()
                        .ToString("d MMM HH:mm", CultureInfo.InvariantCulture)
                    : string.Empty;

            return $"{label} · {verb}{when}";
        }


        // ============================================================
        // RECENT ACTIVITY
        // ============================================================

        private async Task<List<ActivityItem>> LoadActivityAsync(
            int hodId)
        {
            if (hodId <= 0)
                return new List<ActivityItem>();

            var claimActs = await _context.ClaimApprovals
                .AsNoTracking()
                .Where(a =>
                    a.ApprovedByAdminAccountId == hodId &&
                    a.ApprovalRole == ApprovalRole.HOD &&
                    a.Decision != ApprovalDecision.Pending &&
                    a.DecidedAtUtc != null)
                .OrderByDescending(a => a.DecidedAtUtc)
                .Take(6)
                .Select(a => new
                {
                    a.ClaimId,
                    a.Decision,
                    a.DecidedAtUtc,
                    Lecturer = a.Claim.CourseAssignment.Lecturer.UserName
                })
                .ToListAsync();

            return claimActs
                .Select(a => new ActivityItem
                {
                    Verb = a.Decision == ApprovalDecision.Approved
                        ? "You approved"
                        : "You rejected",
                    Reference = $"CLM-{a.ClaimId:D6}",
                    LecturerName = a.Lecturer,
                    AtUtc = a.DecidedAtUtc!.Value,
                    WhenText = Ago(a.DecidedAtUtc!.Value),
                    Positive = a.Decision == ApprovalDecision.Approved
                })
                .OrderByDescending(a => a.AtUtc)
                .Take(6)
                .ToList();
        }


        // ============================================================
        // HELPERS
        // ============================================================

        private static string FacultyDisplayName(Faculty faculty) =>
            faculty switch
            {
                Faculty.ComputingAndInformationSciences =>
                    "Computing & Information Sciences",
                Faculty.EconomicSciencesAndManagement =>
                    "Economic Sciences & Management",
                Faculty.EnvironmentalStudies =>
                    "Environmental Studies",
                Faculty.Law =>
                    "Law",
                _ => faculty.ToString()
            };

        private static string NormalizeFilter(string? value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "pipeline" => "pipeline",
                "closed" => "closed",
                "all" => "all",
                _ => "needs-action"
            };

        private static string AgeWords(int? days) =>
            days switch
            {
                null => "—",
                0 => "today",
                1 => "1 day",
                _ => $"{days} days"
            };

        private static string Ago(DateTime utc)
        {
            var local = utc.ToLocalTime();

            var diff = (DateTime.Now.Date - local.Date).Days;

            return diff switch
            {
                <= 0 => "today",
                1 => "yesterday",
                < 7 => $"{diff} days ago",
                _ => local.ToString("d MMM yyyy", CultureInfo.InvariantCulture)
            };
        }

        private static string ApproverLabel(ApprovalRole role) =>
            role switch
            {
                ApprovalRole.HOD => "HOD",
                ApprovalRole.Dean => "Dean",
                ApprovalRole.DirectorOfQuality => "Director of Quality",
                ApprovalRole.DVCAR => "DVCAR",
                ApprovalRole.HROfficer => "HR Officer",
                ApprovalRole.ViceChancellor => "Vice Chancellor",
                ApprovalRole.Management => "Management",
                _ => role.ToString()
            };

        private static string ApproverShort(ApprovalRole role) =>
            role switch
            {
                ApprovalRole.HOD => "HOD",
                ApprovalRole.Dean => "Dean",
                ApprovalRole.DirectorOfQuality => "Quality",
                ApprovalRole.DVCAR => "DVCAR",
                ApprovalRole.HROfficer => "HR",
                ApprovalRole.ViceChancellor => "VC",
                ApprovalRole.Management => "Mgmt",
                _ => role.ToString()
            };
    }
}