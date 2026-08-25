using System;

namespace SystemModels
{
    public class Lecturer
    {
        public int Id { get; private set; }
        public string Type { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;

        public string GovernmentIdEncrypted { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string SignatureHash { get; set; } = string.Empty;
        public string Rank { get; set; } = string.Empty;

        public Lecturer(int id, string userName, string email)
        {
            Id = id;
            UserName = userName;
            Email = email;
        }
    }
}