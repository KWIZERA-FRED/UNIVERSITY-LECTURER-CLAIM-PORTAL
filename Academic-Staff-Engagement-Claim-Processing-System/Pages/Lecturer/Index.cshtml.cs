
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Security.Claims;
using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

using LecturerModel =
    Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Lecturer;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages.Lecturer
{
    [Authorize(Roles = "Lecturer")]
    public class IndexModel : PageModel
    {
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
        // LECTURER
        // ============================================================

        public LecturerModel? CurrentLecturer { get; private set; }

        public string DisplayName =>
            CurrentLecturer?.UserName ?? "there";

        public string LecturerTypeName =>
            CurrentLecturer?.Type == UserRole.FullTimeLecturer
                ? "Full-Time Lecturer"
                : "Part-Time Lecturer";

        public bool IsFullTime =>
            CurrentLecturer?.Type == UserRole.FullTimeLecturer;

        public bool IsPartTime =>
            CurrentLecturer?.Type == UserRole.PartTimeLecturer;

        // Mirrors the check in ContractSigningService.SignAsLecturerAsync:
        // without a captured signature the lecturer cannot sign anything.
        public bool CanSign =>
            CurrentLecturer is not null &&
            !string.IsNullOrWhiteSpace(CurrentLecturer.SignatureFilePath) &&
            !string.IsNullOrWhiteSpace(CurrentLecturer.SignatureFileHash);


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
        // SUMMARY
        // ============================================================

        // Items that are waiting on the lecturer.
        public int ActionCount { get; private set; }

        // Items the lecturer has finished with, still moving through approval.
        public int InReviewCount { get; private set; }

        public int ClosedCount { get; private set; }

        public int AllCount { get; private set; }

        // Claims that are fully approved or already paid.
        public int ApprovedPaidCount { get; private set; }

        public decimal ApprovedPaidAmount { get; private set; }

        public string OldestActionText { get; private set; } = string.Empty;

        public QueueItem? NextAction { get; private set; }

        public string NextActionWaitText { get; private set; } = string.Empty;

        // The oldest contract waiting for a signature the lecturer cannot
        // give yet because no signature has been captured for them.
        public QueueItem? SignatureBlockedItem { get; private set; }


        // ============================================================
        // QUEUE + ACTIVITY
        // ============================================================

        // Every contract and claim of this lecturer. The browser filters
        // this list live as the lecturer types or switches tab.
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
            InReview = 1,
            Closed = 2
        }

        public sealed class QueueItem
        {
            public QueueKind Kind { get; set; }

            public int Id { get; set; }

            public string Reference { get; set; } = string.Empty;

            public string CourseCode { get; set; } = string.Empty;

            public string CourseTitle { get; set; } = string.Empty;

            public decimal Hours { get; set; }

            public decimal Amount { get; set; }

            public QueueBucket Bucket { get; set; }

            public string StatusText { get; set; } = string.Empty;

            // action | done | rejected | neutral
            public string PillTone { get; set; } = "neutral";

            public string WaitText { get; set; } = string.Empty;

            // ds-tone-normal | ds-tone-warn | ds-tone-late
            public string WaitTone { get; set; } = "ds-tone-normal";

            public DateTime? SinceUtc { get; set; }

            public DateTime LastActivityUtc { get; set; }

            public int? DaysWaiting { get; set; }

            // Banner wording, e.g. "needs your signature".
            public string PromptText { get; set; } = string.Empty;

            // Banner button label, e.g. "Review & sign".
            public string ActionLabel { get; set; } = "Open";

            public string ReviewUrl { get; set; } = "#";

            public bool IsClaim => Kind == QueueKind.Claim;

            public bool IsContract => Kind == QueueKind.Contract;

            public bool NeedsAction => Bucket == QueueBucket.NeedsAction;
        }

        public sealed class ActivityItem
        {
            public string Verb { get; set; } = string.Empty;

            public string Reference { get; set; } = string.Empty;

            public string Detail { get; set; } = string.Empty;

            public DateTime AtUtc { get; set; }

            public string WhenText { get; set; } = string.Empty;

            public bool Positive { get; set; }
        }


        // ============================================================
        // GET
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
            // Build the queue (contracts + claims)
            // --------------------------------------------------------

            var nowUtc = DateTime.UtcNow;

            var all = new List<QueueItem>();

            all.AddRange(await LoadContractItemsAsync(lecturerId, nowUtc));

            var claimLoad = await LoadClaimItemsAsync(lecturerId, nowUtc);

            all.AddRange(claimLoad.Items);

            // Open items: my turn first, then in review; oldest first
            // inside each group. Closed: newest first.
            var open = all
                .Where(i => i.Bucket != QueueBucket.Closed)
                .OrderBy(i => (int)i.Bucket)
                .ThenBy(i => i.SinceUtc ?? DateTime.MaxValue);

            var closed = all
                .Where(i => i.Bucket == QueueBucket.Closed)
                .OrderByDescending(i => i.LastActivityUtc);

            var ordered = open.Concat(closed).ToList();

            ActionCount = ordered.Count(i => i.Bucket == QueueBucket.NeedsAction);
            InReviewCount = ordered.Count(i => i.Bucket == QueueBucket.InReview);
            ClosedCount = ordered.Count(i => i.Bucket == QueueBucket.Closed);
            AllCount = ordered.Count;

            NextAction = ordered
                .FirstOrDefault(i => i.Bucket == QueueBucket.NeedsAction);

            if (NextAction is not null)
            {
                OldestActionText = AgeWords(NextAction.DaysWaiting);

                NextActionWaitText =
                    NextAction.DaysWaiting is null or 0
                        ? "since today"
                        : $"for {AgeWords(NextAction.DaysWaiting)}";
            }

            if (!CanSign)
            {
                SignatureBlockedItem = ordered
                    .FirstOrDefault(i =>
                        i.Bucket == QueueBucket.NeedsAction &&
                        i.IsContract);
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

            Activity = await LoadActivityAsync(lecturerId, claimLoad.Activity);
        }


        // ============================================================
        // LIVE-FILTER SUPPORT  (same rules the browser script uses)
        // ============================================================

        public string BucketKey(QueueItem item) =>
            item.Bucket switch
            {
                QueueBucket.NeedsAction => "needs",
                QueueBucket.InReview => "review",
                _ => "closed"
            };

        public string SearchKey(QueueItem item) =>
            (
                $"{item.Reference} {item.CourseCode} {item.CourseTitle} " +
                (item.IsClaim ? "claim" : "contract")
            ).ToLowerInvariant();

        // Used so the first paint already shows the right rows,
        // before the browser script runs.
        public bool IsVisibleOnLoad(QueueItem item)
        {
            var inFilter = ActiveFilter switch
            {
                "in-review" => item.Bucket == QueueBucket.InReview,

                "closed" => item.Bucket == QueueBucket.Closed,

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
            int lecturerId,
            DateTime nowUtc)
        {
            var result = new List<QueueItem>();

            var contracts = await _context.Contracts
                .AsNoTracking()
                .Where(c => c.LecturerId == lecturerId)
                .Select(c => new
                {
                    c.Id,
                    c.Status,
                    c.RatePerHour,
                    c.CreatedAtUtc,
                    c.UpdatedAtUtc,
                    c.SignedAtUtc,

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

            if (contracts.Count == 0)
                return result;

            var ids = contracts.Select(c => c.Id).ToList();

            var signatures = await _context.ContractSignatures
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
                var steps = signatures
                    .Where(s => s.ContractId == contract.Id)
                    .OrderBy(s => s.SequenceOrder)
                    .ToList();

                var declined = steps
                    .FirstOrDefault(s =>
                        s.Decision == SignatureDecision.Declined);

                // Same rule the contract page uses to decide whether
                // the lecturer still has to sign.
                var lecturerSigned = steps.Any(s =>
                    s.SignerRole == SignerRole.Lecturer &&
                    s.Decision == SignatureDecision.Signed);

                // With parallel signing (e.g. Dean and HR together) several
                // steps share the same order; show them all.
                var waitingOn = steps
                    .Where(s => s.Decision == SignatureDecision.Pending)
                    .GroupBy(s => s.SequenceOrder)
                    .OrderBy(g => g.Key)
                    .FirstOrDefault();

                QueueBucket bucket;
                string status;
                string tone;

                if (declined is not null)
                {
                    bucket = QueueBucket.Closed;
                    status = $"Declined by {SignerLabel(declined.SignerRole)}";
                    tone = "rejected";
                }
                else if (contract.Status == ContractStatus.Active)
                {
                    bucket = QueueBucket.Closed;
                    status = "Active · fully signed";
                    tone = "done";
                }
                else if (contract.Status == ContractStatus.Renewed)
                {
                    bucket = QueueBucket.Closed;
                    status = "Renewed";
                    tone = "done";
                }
                else if (contract.Status == ContractStatus.Expired)
                {
                    bucket = QueueBucket.Closed;
                    status = "Expired";
                    tone = "neutral";
                }
                else if (contract.Status == ContractStatus.Terminated)
                {
                    bucket = QueueBucket.Closed;
                    status = "Terminated";
                    tone = "rejected";
                }
                else if (!lecturerSigned)
                {
                    bucket = QueueBucket.NeedsAction;
                    status = "Needs your signature";
                    tone = "action";
                }
                else
                {
                    bucket = QueueBucket.InReview;
                    status = waitingOn is null
                        ? "With approvers"
                        : "With " + string.Join(
                            " & ",
                            waitingOn.Select(s => SignerLabel(s.SignerRole)));
                    tone = "neutral";
                }

                var lastDone = steps
                    .Where(s =>
                        s.Decision == SignatureDecision.Signed &&
                        s.SignedAtUtc.HasValue)
                    .Select(s => s.SignedAtUtc!.Value)
                    .DefaultIfEmpty(contract.CreatedAtUtc)
                    .Max();

                var lastActivity = steps
                    .Where(s => s.SignedAtUtc.HasValue)
                    .Select(s => s.SignedAtUtc!.Value)
                    .Append(contract.CreatedAtUtc)
                    .Append(contract.UpdatedAtUtc ?? contract.CreatedAtUtc)
                    .Append(contract.SignedAtUtc ?? contract.CreatedAtUtc)
                    .Max();

                var item = new QueueItem
                {
                    Kind = QueueKind.Contract,
                    Id = contract.Id,
                    Reference = $"CON-{contract.Id:D6}",
                    CourseCode = contract.CourseCode,
                    CourseTitle = contract.CourseTitle,
                    Hours = contract.Hours,
                    Amount = contract.Hours * contract.RatePerHour,
                    Bucket = bucket,
                    StatusText = status,
                    PillTone = tone,
                    PromptText = "needs your signature",
                    ActionLabel = "Review & sign",
                    ReviewUrl =
                        Url.Page(
                            "/Lecturer/ContractDetails",
                            new { id = contract.Id })
                        ?? "#"
                };

                ApplyTiming(item, lastDone, lastActivity, nowUtc);

                result.Add(item);
            }

            return result;
        }


        // ============================================================
        // CLAIMS
        // ============================================================

        private async Task<(List<QueueItem> Items, List<ActivityItem> Activity)>
            LoadClaimItemsAsync(int lecturerId, DateTime nowUtc)
        {
            var result = new List<QueueItem>();
            var activity = new List<ActivityItem>();

            var claims = await _context.Claims
                .AsNoTracking()
                .Where(c => c.CourseAssignment.LecturerId == lecturerId)
                .Select(c => new
                {
                    c.Id,
                    c.Status,
                    c.HoursClaimed,
                    c.Amount,
                    c.CreatedAtUtc,
                    c.SubmittedAtUtc,
                    c.UpdatedAtUtc,
                    c.CompletedAtUtc,

                    CourseTitle = c.CourseAssignment.Course.Title,
                    CourseCode = c.CourseAssignment.Course.Code
                })
                .ToListAsync();

            if (claims.Count == 0)
                return (result, activity);

            var ids = claims.Select(c => c.Id).ToList();

            var approvals = await _context.ClaimApprovals
                .AsNoTracking()
                .Where(a => ids.Contains(a.ClaimId))
                .Select(a => new
                {
                    a.ClaimId,
                    a.ApprovalRole,
                    a.Decision,
                    a.DecidedAtUtc
                })
                .ToListAsync();

            foreach (var claim in claims)
            {
                var steps = approvals
                    .Where(a => a.ClaimId == claim.Id)
                    .ToList();

                var rejection = steps
                    .FirstOrDefault(a =>
                        a.Decision == ApprovalDecision.Rejected);

                QueueBucket bucket;
                string status;
                string tone;
                string prompt = string.Empty;
                string actionLabel = "Open claim";

                switch (claim.Status)
                {
                    case ClaimStatus.Draft:
                        bucket = QueueBucket.NeedsAction;
                        status = "Draft · not submitted";
                        tone = "action";
                        prompt = "is still a draft";
                        break;

                    case ClaimStatus.Rejected:
                        bucket = QueueBucket.Closed;
                        status = rejection is not null
                            ? $"Rejected by {ApproverLabel(rejection.ApprovalRole)}"
                            : "Rejected";
                        tone = "rejected";
                        break;

                    case ClaimStatus.Approved:
                        bucket = QueueBucket.Closed;
                        status = "Fully approved";
                        tone = "done";
                        break;

                    case ClaimStatus.Paid:
                        bucket = QueueBucket.Closed;
                        status = "Paid";
                        tone = "done";
                        break;

                    case ClaimStatus.Submitted:
                        bucket = QueueBucket.InReview;
                        status = "Submitted";
                        tone = "neutral";
                        break;

                    case ClaimStatus.PendingHODApproval:
                        bucket = QueueBucket.InReview;
                        status = "With HOD";
                        tone = "neutral";
                        break;

                    case ClaimStatus.PendingDeanApproval:
                        bucket = QueueBucket.InReview;
                        status = "With Dean";
                        tone = "neutral";
                        break;

                    case ClaimStatus.PendingDirectorOfQualityApproval:
                        bucket = QueueBucket.InReview;
                        status = "With Director of Quality";
                        tone = "neutral";
                        break;

                    case ClaimStatus.PendingDVCARApproval:
                        bucket = QueueBucket.InReview;
                        status = "With DVCAR";
                        tone = "neutral";
                        break;

                    default:
                        bucket = QueueBucket.InReview;
                        status = claim.Status.ToString();
                        tone = "neutral";
                        break;
                }

                var started = claim.SubmittedAtUtc ?? claim.CreatedAtUtc;

                var lastDone = steps
                    .Where(a =>
                        a.Decision == ApprovalDecision.Approved &&
                        a.DecidedAtUtc.HasValue)
                    .Select(a => a.DecidedAtUtc!.Value)
                    .DefaultIfEmpty(started)
                    .Max();

                // A draft has been waiting on the lecturer since it was created.
                if (claim.Status == ClaimStatus.Draft)
                    lastDone = claim.CreatedAtUtc;

                var lastActivity = steps
                    .Where(a => a.DecidedAtUtc.HasValue)
                    .Select(a => a.DecidedAtUtc!.Value)
                    .Append(claim.CreatedAtUtc)
                    .Append(started)
                    .Append(claim.UpdatedAtUtc ?? claim.CreatedAtUtc)
                    .Append(claim.CompletedAtUtc ?? claim.CreatedAtUtc)
                    .Max();

                var reference = $"CLM-{claim.Id:D6}";

                var item = new QueueItem
                {
                    Kind = QueueKind.Claim,
                    Id = claim.Id,
                    Reference = reference,
                    CourseCode = claim.CourseCode,
                    CourseTitle = claim.CourseTitle,
                    Hours = claim.HoursClaimed,
                    Amount = claim.Amount,
                    Bucket = bucket,
                    StatusText = status,
                    PillTone = tone,
                    PromptText = prompt,
                    ActionLabel = actionLabel,
                    ReviewUrl =
                        Url.Page(
                            "/Lecturer/ClaimDetail",
                            new { ClaimId = claim.Id })
                        ?? "#"
                };

                ApplyTiming(item, lastDone, lastActivity, nowUtc);

                result.Add(item);

                if (claim.Status is ClaimStatus.Approved or ClaimStatus.Paid)
                {
                    ApprovedPaidCount++;
                    ApprovedPaidAmount += claim.Amount;
                }


                // ---- activity entries for this claim ----

                if (claim.SubmittedAtUtc.HasValue)
                {
                    activity.Add(new ActivityItem
                    {
                        Verb = "You submitted",
                        Reference = reference,
                        Detail = claim.CourseTitle,
                        AtUtc = claim.SubmittedAtUtc.Value,
                        Positive = true
                    });
                }

                if (rejection is not null && rejection.DecidedAtUtc.HasValue)
                {
                    activity.Add(new ActivityItem
                    {
                        Verb = $"Rejected by {ApproverLabel(rejection.ApprovalRole)}",
                        Reference = reference,
                        Detail = claim.CourseTitle,
                        AtUtc = rejection.DecidedAtUtc.Value,
                        Positive = false
                    });
                }

                if (claim.Status is ClaimStatus.Approved or ClaimStatus.Paid)
                {
                    activity.Add(new ActivityItem
                    {
                        Verb = claim.Status == ClaimStatus.Paid
                            ? "Paid"
                            : "Fully approved",
                        Reference = reference,
                        Detail = claim.CourseTitle,
                        AtUtc =
                            claim.CompletedAtUtc ??
                            claim.UpdatedAtUtc ??
                            claim.CreatedAtUtc,
                        Positive = true
                    });
                }
            }

            return (result, activity);
        }


        // ============================================================
        // TIMING  (how long an open item has waited)
        // ============================================================

        private static void ApplyTiming(
            QueueItem item,
            DateTime lastDoneUtc,
            DateTime lastActivityUtc,
            DateTime nowUtc)
        {
            item.LastActivityUtc = lastActivityUtc;

            if (item.Bucket == QueueBucket.Closed)
            {
                item.WaitText = $"Updated {Ago(lastActivityUtc)}";
                return;
            }

            var days = Math.Max(
                0,
                (int)Math.Floor((nowUtc - lastDoneUtc).TotalDays));

            item.SinceUtc = lastDoneUtc;
            item.DaysWaiting = days;

            item.WaitText = days == 0
                ? "Waiting since today"
                : $"Waiting {AgeWords(days)}";

            item.WaitTone = days >= 7
                ? "ds-tone-late"
                : days >= 3
                    ? "ds-tone-warn"
                    : "ds-tone-normal";
        }


        // ============================================================
        // RECENT ACTIVITY  (this lecturer's last steps)
        // ============================================================

        private async Task<List<ActivityItem>> LoadActivityAsync(
            int lecturerId,
            List<ActivityItem> claimActivity)
        {
            var list = new List<ActivityItem>(claimActivity);

            // ---- contracts the lecturer signed ----

            var signed = await _context.ContractSignatures
                .AsNoTracking()
                .Where(s =>
                    s.SignedByLecturerId == lecturerId &&
                    s.Decision != SignatureDecision.Pending &&
                    s.SignedAtUtc != null)
                .OrderByDescending(s => s.SignedAtUtc)
                .Take(6)
                .Select(s => new
                {
                    s.ContractId,
                    s.Decision,
                    s.SignedAtUtc,

                    CourseTitle =
                        s.Contract.CourseAssignment != null
                            ? s.Contract.CourseAssignment.Course.Title
                            : "—"
                })
                .ToListAsync();

            foreach (var s in signed)
            {
                list.Add(new ActivityItem
                {
                    Verb = s.Decision == SignatureDecision.Signed
                        ? "You signed"
                        : "You declined",
                    Reference = $"CON-{s.ContractId:D6}",
                    Detail = s.CourseTitle,
                    AtUtc = s.SignedAtUtc!.Value,
                    Positive = s.Decision == SignatureDecision.Signed
                });
            }

            // ---- marks the lecturer submitted ----

            var marks = await _context.MarksSubmissions
                .AsNoTracking()
                .Where(m => m.LecturerId == lecturerId)
                .OrderByDescending(m => m.SubmittedAtUtc)
                .Take(6)
                .Select(m => new
                {
                    m.SubmissionReference,
                    m.Status,
                    m.SubmittedAtUtc,
                    m.ReviewedAtUtc,
                    m.SignedAtUtc,
                    CourseTitle = m.Course.Title
                })
                .ToListAsync();

            foreach (var m in marks)
            {
                var verb = m.Status switch
                {
                    MarksSubmissionStatus.Signed => "Marks signed off",
                    MarksSubmissionStatus.Declined => "Marks declined",
                    _ => "You submitted marks"
                };

                var at = m.Status switch
                {
                    MarksSubmissionStatus.Signed =>
                        m.SignedAtUtc ?? m.ReviewedAtUtc ?? m.SubmittedAtUtc,

                    MarksSubmissionStatus.Declined =>
                        m.ReviewedAtUtc ?? m.SubmittedAtUtc,

                    _ => m.SubmittedAtUtc
                };

                list.Add(new ActivityItem
                {
                    Verb = verb,
                    Reference = m.SubmissionReference,
                    Detail = m.CourseTitle,
                    AtUtc = at,
                    Positive = m.Status != MarksSubmissionStatus.Declined
                });
            }

            var latest = list
                .OrderByDescending(a => a.AtUtc)
                .Take(6)
                .ToList();

            foreach (var entry in latest)
                entry.WhenText = Ago(entry.AtUtc);

            return latest;
        }


        // ============================================================
        // HELPERS
        // ============================================================

        private static string NormalizeFilter(string? value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "in-review" => "in-review",
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
    }
}