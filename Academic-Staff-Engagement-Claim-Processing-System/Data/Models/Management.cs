using System;

namespace SystemModels
{
    public class Management
    {
        public int Id { get; private set; }
        public string Username { get; set; } = string.Empty;

        // Security: Restrict Role modifications to prevent privilege escalation
        public string Role { get; private set; } = string.Empty;

        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string SignatureUrlOrHash { get; set; } = string.Empty;

        public Management(int id, string username, string role, string email)
        {
            Id = id;
            Username = username;
            Role = role;
            Email = email;
        }

        public void AssignRole(string newRole)
        {
            // Add authorization checks here before updating roles
            Role = newRole;
        }
    }
}