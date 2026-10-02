using System.Globalization;
using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Academic_Staff_Engagement_Claim_Processing_System.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages.DEAN
{
    [Authorize(Roles = "Dean")]
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
        // QUERY STRING  (initial state; the browser takes over after load)
        // ============================================================

        [BindProperty(SupportsGet = true)]
        public string? Filter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Q { get; set; }

        public string ActiveFilter { get; private set; } = "needs-action";


        // ============================================================
        // NEW: display name — shown in the header greeting.
        // ============================================================

        public string DisplayName { get; private set; } = "there";


        // ============================================================
        // SUMMARY
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

        // The second needs-action item, shown on the banner's
        // secondary line so the Dean can see what comes next.
        public QueueItem? NextAfterAction { get; private set; }

        // How many items follow the second one (may be zero).
        public int RemainingAfterNext { get; private set; }


        // ============================================================
        // QUEUE + ACTIVITY
        // ============================================================

        // Every open item plus recent closed ones. The browser filters
        // this list live as the Dean types or switches tab.
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

        // Order matters: it is the display order.
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

            // done | current | waiting | rejected | info
            public string PillTone { get; set; } = "info";

            public string WaitText { get; set; } = string.Empty;

            // ds-tone-normal | ds-tone-warn | ds-tone-late
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
            bool IsDean,
            string? DoneVerb = null);


        // ============================================================
        // GET
        // ============================================================

        public async Task OnGetAsync()
        {
            int.TryParse(
                User.FindFirst("UserId")?.Value,
                out int currentDeanId);

            // NEW: resolve the logged-in user's display name.
            // Prefers the cookie identity (set at login), falls back to
            // the database UserName, then to a generic fallback so the
            // header never renders as "Welcome back, ".
            DisplayName =
                User.Identity?.Name
                ?? (currentDeanId > 0
                    ? await _context.AdminAccounts
                        .AsNoTracking()
                        .Where(a => a.Id == currentDeanId)
                        .Select(a => a.UserName)
                        .FirstOrDefaultAsync()
                    : null)
                ?? "there";

            var nowUtc = DateTime.UtcNow;
            var cutoffUtc = nowUtc.AddDays(-ClosedWindowDays);

            var all = new List<QueueItem>();

            all.AddRange(await LoadContractItemsAsync(nowUtc, cutoffUtc));
            all.AddRange(await LoadClaimItemsAsync(nowUtc, cutoffUtc));

            // Open items: my turn first, then waiting, then past me;
            // oldest first inside each group. Closed: newest first.
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

            // Second needs-action item + remaining count.
            // Uses a fresh Where/Skip pass so the existing NextAction
            // assignment above is left untouched.
            NextAfterAction = ordered
                .Where(i => i.Bucket == QueueBucket.NeedsAction)
                .Skip(1)
                .FirstOrDefault();

            RemainingAfterNext = Math.Max(0, ReadyCount - 2);

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

            Activity = await LoadActivityAsync(currentDeanId);
        }


        // ============================================================
        // LIVE-FILTER SUPPORT  (same rules the browser script uses)
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

        // Used so the first paint already shows the right rows,
        // before the browser script runs.
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
        // CONTRACTS
        // ============================================================

        private async Task<List<QueueItem>> LoadContractItemsAsync(
            DateTime nowUtc,
            DateTime cutoffUtc)
        {
            var ids = await _context.ContractSignatures
                .AsNoTracking()
                .Where(s =>
                    s.SignerRole == SignerRole.Dean &&
                    (s.Decision == SignatureDecision.Pending ||
                     (s.SignedAtUtc != null && s.SignedAtUtc >= cutoffUtc) ||
                     s.Contract.CreatedAtUtc >= cutoffUtc))
                .Select(s => s.ContractId)
                .Distinct()
                .ToListAsync();

            var result = new List<QueueItem>();

            if (ids.Count == 0)
                return result;

            var contracts = await _context.Contracts
                .AsNoTracking()
                .Where(c => ids.Contains(c.Id))
                .Select(c => new
                {
                    c.Id,
                    c.RatePerHour,
                    c.CreatedAtUtc,

                    Lecturer = c.Lecturer.UserName,

                    CourseTitle =
                        c.CourseAssignment != null
                            ? c.CourseAssignment.Course.Title
                            : "—",

                    CourseCode =
                        c.CourseAssignment != null
                            ? c.CourseAssignment.Course.Code
                            : string.Empty,

                    Hours =
                        c.CourseAssignment != null
                            ? c.CourseAssignment.AllocatedHours
                            : 0m
                })
                .ToListAsync();

            var steps = await _context.ContractSignatures
                .AsNoTracking()
                .Where(s => ids.Contains(s.ContractId))
                .Select(s => new
                {
                    s.ContractId,
                    s.SequenceOrder,
                    s.SignerRole,
                    s.Decision,
                    s.SignedAtUtc
                })
                .ToListAsync();

            foreach (var contract in contracts)
            {
                var raw = steps
                    .Where(s => s.ContractId == contract.Id)
                    .Select(s => new StepRaw(
                        s.SequenceOrder,
                        SignerLabel(s.SignerRole),
                        SignerShort(s.SignerRole),
                        s.Decision switch
                        {
                            SignatureDecision.Signed => Outcome.Done,
                            SignatureDecision.Declined => Outcome.Rejected,
                            _ => Outcome.Pending
                        },
                        s.SignedAtUtc,
                        s.SignerRole == SignerRole.Dean))
                    .ToList();

                var item = Assemble(
                    QueueKind.Contract,
                    contract.Id,
                    contract.Lecturer,
                    contract.CourseCode,
                    contract.CourseTitle,
                    contract.Hours,
                    contract.Hours * contract.RatePerHour,
                    contract.CreatedAtUtc,
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
        // CLAIMS
        // ============================================================

        private async Task<List<QueueItem>> LoadClaimItemsAsync(
            DateTime nowUtc,
            DateTime cutoffUtc)
        {
            var ids = await _context.ClaimApprovals
                .AsNoTracking()
                .Where(a =>
                    a.ApprovalRole == ApprovalRole.Dean &&
                    (a.Decision == ApprovalDecision.Pending ||
                     (a.DecidedAtUtc != null && a.DecidedAtUtc >= cutoffUtc) ||
                     a.Claim.CreatedAtUtc >= cutoffUtc))
                .Select(a => a.ClaimId)
                .Distinct()
                .ToListAsync();

            var result = new List<QueueItem>();

            if (ids.Count == 0)
                return result;

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
                // The lecturer's submission is the first step of the journey.
                var raw = new List<StepRaw>
                {
                    new StepRaw(
                        0,
                        "Lecturer",
                        "Lecturer",
                        Outcome.Done,
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
                            s.ApprovalRole == ApprovalRole.Dean)));

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

            var dean = ordered.FirstOrDefault(s => s.IsDean);

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
                    s.IsDean));
            }

            // ---- bucket + wording ----

            QueueBucket bucket;
            string status;
            string tone;

            if (rejected is not null)
            {
                bucket = QueueBucket.Closed;
                tone = "rejected";
                status = kind == QueueKind.Claim
                    ? $"Rejected by {rejected.Label}"
                    : $"Declined by {rejected.Label}";
            }
            else if (completed)
            {
                bucket = QueueBucket.Closed;
                tone = "done";
                status = kind == QueueKind.Claim
                    ? "Fully approved"
                    : "Fully signed";
            }
            else if (current!.IsDean)
            {
                bucket = QueueBucket.NeedsAction;
                tone = "current";
                status = "Needs your signature";
            }
            else if (dean is not null && dean.Outcome == Outcome.Done)
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

                Reference = kind == QueueKind.Claim
                    ? $"CLM-{id:D6}"
                    : $"CON-{id:D6}",

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
                    (kind == QueueKind.Claim
                        ? Url.Page("/DEAN/ClaimDetails", new { claimId = id })
                        : Url.Page("/DEAN/ContractDetails", new { contractId = id }))
                    ?? "#"
            };
        }

        private static string BuildTip(
            StepRaw step,
            StepState state,
            QueueKind kind)
        {
            var label = step.IsDean
                ? $"{step.Label} (you)"
                : step.Label;

            var verb = state switch
            {
                StepState.Done => step.DoneVerb ??
                    (kind == QueueKind.Claim ? "Approved" : "Signed"),

                StepState.Rejected =>
                    kind == QueueKind.Claim ? "Rejected" : "Declined",

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
        // RECENT ACTIVITY  (this Dean's last decisions)
        // ============================================================

        private async Task<List<ActivityItem>> LoadActivityAsync(
            int deanId)
        {
            if (deanId <= 0)
                return new List<ActivityItem>();

            var contractActs = await _context.ContractSignatures
                .AsNoTracking()
                .Where(s =>
                    s.SignedByAdminAccountId == deanId &&
                    s.Decision != SignatureDecision.Pending &&
                    s.SignedAtUtc != null)
                .OrderByDescending(s => s.SignedAtUtc)
                .Take(6)
                .Select(s => new
                {
                    s.ContractId,
                    s.Decision,
                    s.SignedAtUtc,
                    Lecturer = s.Contract.Lecturer.UserName
                })
                .ToListAsync();

            var claimActs = await _context.ClaimApprovals
                .AsNoTracking()
                .Where(a =>
                    a.ApprovedByAdminAccountId == deanId &&
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

            var list = new List<ActivityItem>();

            foreach (var c in contractActs)
            {
                var at = c.SignedAtUtc!.Value;

                list.Add(new ActivityItem
                {
                    Verb = c.Decision == SignatureDecision.Signed
                        ? "You signed"
                        : "You declined",
                    Reference = $"CON-{c.ContractId:D6}",
                    LecturerName = c.Lecturer,
                    AtUtc = at,
                    WhenText = Ago(at),
                    Positive = c.Decision == SignatureDecision.Signed
                });
            }

            foreach (var c in claimActs)
            {
                var at = c.DecidedAtUtc!.Value;

                list.Add(new ActivityItem
                {
                    Verb = c.Decision == ApprovalDecision.Approved
                        ? "You approved"
                        : "You rejected",
                    Reference = $"CLM-{c.ClaimId:D6}",
                    LecturerName = c.Lecturer,
                    AtUtc = at,
                    WhenText = Ago(at),
                    Positive = c.Decision == ApprovalDecision.Approved
                });
            }

            return list
                .OrderByDescending(a => a.AtUtc)
                .Take(6)
                .ToList();
        }


        // ============================================================
        // HELPERS
        // ============================================================

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

        // "today", "yesterday", "3 days ago", or a date.
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

        private static string SignerLabel(SignerRole role) =>
            role switch
            {
                SignerRole.Lecturer => "Lecturer",
                SignerRole.Dean => "Dean",
                SignerRole.HROfficer => "HR Officer",
                SignerRole.DVCAR => "DVCAR",
                SignerRole.ViceChancellor => "Vice Chancellor",
                SignerRole.ExamOffice => "Exam Office",
                _ => role.ToString()
            };

        private static string SignerShort(SignerRole role) =>
            role switch
            {
                SignerRole.Lecturer => "Lecturer",
                SignerRole.Dean => "Dean",
                SignerRole.HROfficer => "HR",
                SignerRole.DVCAR => "DVCAR",
                SignerRole.ViceChancellor => "VC",
                SignerRole.ExamOffice => "Exam",
                _ => role.ToString()
            };

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