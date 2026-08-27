using Microsoft.AspNetCore.DataProtection;

namespace Academic_Staff_Engagement_Claim_Processing_System.Services
{
    // Wraps ASP.NET Core's Data Protection API with a fixed purpose string
    // so this encryption key is never reused for anything else (e.g.
    // password reset tokens, anti-forgery tokens). Registered as a
    // singleton in Program.cs and used by ApplicationDbContext's value
    // converter to encrypt/decrypt Lecturer.GovernmentIdEncrypted
    // transparently on every save/read.
    public class GovernmentIdProtector
    {
        private readonly IDataProtector _protector;

        public GovernmentIdProtector(IDataProtectionProvider provider)
        {
            _protector = provider.CreateProtector("Lecturer.GovernmentId.v1");
        }

        public string Encrypt(string plainText) => _protector.Protect(plainText);

        public string Decrypt(string cipherText) => _protector.Unprotect(cipherText);
    }
}