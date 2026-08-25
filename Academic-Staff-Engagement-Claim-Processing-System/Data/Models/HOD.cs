using System;

namespace SystemModels
{
    public class Hod
    {
        public int Id { get; private set; }
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; private set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public int FailedLoginAttempts { get; set; } = 0;
        public DateTime? LockoutEndUtc { get; set; }
        public DateTime? LastLoginUtc { get; set; }

        public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
        public DateTime? UpdatedAtUtc { get; set; }

        public byte[]? RowVersion { get; set; }

        public Hod(int id, string userName, string email)
        {
            Id = id;
            UserName = userName;
            Email = email;
        }

        // Password only changes through here — never a public setter —
        // so every hash update goes through one auditable path.
        public void SetPasswordHash(string newHash)
        {
            if (string.IsNullOrWhiteSpace(newHash))
                throw new ArgumentException("Password hash cannot be empty.", nameof(newHash));

            PasswordHash = newHash;
            UpdatedAtUtc = DateTime.UtcNow;
        }
    }
}