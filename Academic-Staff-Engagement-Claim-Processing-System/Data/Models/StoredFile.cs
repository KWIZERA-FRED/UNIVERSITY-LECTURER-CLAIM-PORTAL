using System;
using System.ComponentModel.DataAnnotations;

namespace Academic_Staff_Engagement_Claim_Processing_System.Data.Models
{
    public class StoredFile
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(255)]
        public string OriginalFileName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string ContentType { get; set; } = string.Empty;

        public long SizeBytes { get; set; }

        [Required]
        [MaxLength(64)]
        public string Sha256Hash { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Folder { get; set; } = string.Empty;

        [Required]
        public byte[] Content { get; set; } = Array.Empty<byte>();

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}