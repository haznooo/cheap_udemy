using System;
using System.Collections.Generic;
using System.Text;

namespace Business.Dto.Rsponse
{
    public class LoginResponse
    {

        public int Id { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }   // "Student", "Instructor", etc.
        public string Status { get; set; }   // "Active"
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public DateTime? RefreshTokenExpiresAt { get; set; }
        public UserProfileResponse? Profile { get; set; } = null;

    }
}
