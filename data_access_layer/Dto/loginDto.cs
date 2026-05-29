using System;
using System.Collections.Generic;
using System.Text;

namespace DataAccess.Dto
{
    public class loginDto
    {

       public int Id { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }   // "Student", "Instructor", etc.
        public string Status { get; set; }   // "Active"
        public string Token { get; set; }
        public string RefreshToken { get; set; }
        public DateTime? RefreshTokenExpiresAt { get; set; }
          public bool IsRevoked { get; set; }
        public UserProfileDto? Profile { get; set; } = null;

    }
}
