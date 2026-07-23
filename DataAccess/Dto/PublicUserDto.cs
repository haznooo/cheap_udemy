using System;
using System.Collections.Generic;
using System.Text;

namespace DataAccess.Dto
{
    // Public-facing user info (e.g. the instructor who published a course):
    // username + the display name/avatar from their profile. No email/role/status.
    public class PublicUserDto
    {
        public int UserId { get; set; }
        public string Username { get; set; }
        public string? DisplayName { get; set; }
        public string? ImageUrl { get; set; }
    }
}
