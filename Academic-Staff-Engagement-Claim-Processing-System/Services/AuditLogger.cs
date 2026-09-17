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
            var entry = new AuditLog(
                action,
                actorUsername,
                actorRole,
                actorId,
                entityType,
                entityId,
                details,
                ipAddress);

            _context.AuditLogs.Add(entry);
        }

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