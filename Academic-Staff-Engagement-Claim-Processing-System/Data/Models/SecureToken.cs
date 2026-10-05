using System.Security.Cryptography;

namespace Academic_Staff_Engagement_Claim_Processing_System.Data.Models
{
    // 16 bytes from the OS cryptographic RNG (128 bits), written as
    // 32 lowercase hex characters. Same format as the old
    // Guid.ToString("N") tokens, so the column, the length check and
    // every existing token keep working. No migration needed.
    public static class SecureToken
    {
        public static string Create() =>
            Convert.ToHexString(
                RandomNumberGenerator.GetBytes(16))
            .ToLowerInvariant();
    }
}