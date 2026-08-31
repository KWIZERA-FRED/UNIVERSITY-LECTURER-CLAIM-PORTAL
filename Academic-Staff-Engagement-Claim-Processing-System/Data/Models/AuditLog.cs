using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;

namespace Academic_Staff_Engagement_Claim_Processing_System.Data.Models
{
    // Insert-only. Never updated, never deleted, no setters exposed
    // beyond the constructor — this table exists specifically to be a
    // trustworthy record of what happened, even if the account that
    // did it is later disabled, renamed, or deleted.
    public class AuditLog
    {
        [Key]
        public long Id { get; private set; }

        [Required]
        public AuditAction Action { get; private set; }

        // Denormalized snapshot — survives account deletion/rename,
        // so the log always reads clearly on its own.
        [Required]
        [MaxLength(100)]
        public string ActorUsername { get; private set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string ActorRole { get; private set; } = string.Empty;

        // Nullable: a failed login attempt against a username that
        // doesn't exist has no real account ID to point to.
        public int? ActorId { get; private set; }

        // What this action was performed on — generic so one table
        // covers logins, registrations, approvals, everything.
        [MaxLength(50)]
        public string? EntityType { get; private set; }

        public int? EntityId { get; private set; }

        [MaxLength(500)]
        public string? Details { get; private set; }

        [MaxLength(45)] // fits IPv6
        public string? IpAddress { get; private set; }

        [Required]
        public DateTime OccurredAtUtc { get; private set; } = DateTime.UtcNow;

        // EF Core needs a parameterless constructor for materialization.
        private AuditLog() { }

        public AuditLog(
            AuditAction action,
            string actorUsername,
            string actorRole,
            int? actorId,
            string? entityType,
            int? entityId,
            string? details,
            string? ipAddress)
        {
            Action = action;
            ActorUsername = actorUsername;
            ActorRole = actorRole;
            ActorId = actorId;
            EntityType = entityType;
            EntityId = entityId;
            Details = details;
            IpAddress = ipAddress;
            OccurredAtUtc = DateTime.UtcNow;
        }
    }
}