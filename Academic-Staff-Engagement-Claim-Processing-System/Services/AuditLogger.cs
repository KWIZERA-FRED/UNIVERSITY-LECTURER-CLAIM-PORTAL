using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;

namespace Academic_Staff_Engagement_Claim_Processing_System.Services
{
    // Thin wrapper around the create + save pattern for AuditLog.
    // Deliberately does its own SaveChangesAsync() call, separate from
    // whatever the caller is doing — audit writes are insert-only and
    // shouldn't get tangled up in a caller's concurrency-retry logic
    // (e.g. Login's optimistic-concurrency retry on the user entity).
    public class AuditLogger
    {
        private readonly ApplicationDbContext _context;

        public AuditLogger(ApplicationDbContext context)
        {
            _context = context;
        }

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

            await _context.SaveChangesAsync();
        }
    }
}