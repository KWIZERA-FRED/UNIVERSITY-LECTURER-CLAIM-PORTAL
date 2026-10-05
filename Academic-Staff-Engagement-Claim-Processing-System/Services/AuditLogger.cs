using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using System.Threading.Tasks;

namespace Academic_Staff_Engagement_Claim_Processing_System.Services
{
    public class AuditLogger
    {
        private readonly ApplicationDbContext _context;

        public AuditLogger(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Adds an audit record to the current DbContext without saving it.
        /// This is useful when the audit record must be committed together
        /// with another database operation inside the same transaction.
        /// </summary>
        public void Add(
            AuditAction action,
            string actorUsername,
            string actorRole,
            int? actorId = null,
            string? entityType = null,
            int? entityId = null,
            string? details = null,
            string? ipAddress = null)
        {
            // Column limits (AuditLogs): ActorUsername 100, ActorRole 20,
            // EntityType 50, Details 500, IpAddress 45. A value that is too
            // long must never make the audited action itself fail.
            var entry = new AuditLog(
                action,
                Limit(actorUsername, 100) ?? string.Empty,
                Limit(actorRole, 20) ?? string.Empty,
                actorId,
                Limit(entityType, 50),
                entityId,
                Limit(details, 500),
                Limit(ipAddress, 45));

            _context.AuditLogs.Add(entry);
        }

        private static string? Limit(string? value, int max) =>
            value is null || value.Length <= max
                ? value
                : value.Substring(0, max);

        /// <summary>
        /// Adds and immediately saves an audit record.
        /// Existing workflows can continue using this method.
        /// </summary>
        public async Task LogAsync(
            AuditAction action,
            string actorUsername,
            string actorRole,
            int? actorId = null,
            string? entityType = null,
            int? entityId = null,
            string? details = null,
            string? ipAddress = null)
        {
            Add(
                action,
                actorUsername,
                actorRole,
                actorId,
                entityType,
                entityId,
                details,
                ipAddress);

            await _context.SaveChangesAsync();
        }
    }
}