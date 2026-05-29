
using System;
using System.Collections.Generic;
using System.Text;

namespace DataAccess.Dto
{
   
        public class UserAndProfileDto
    {

          required  public int UserId { get; set; }
           required public string Username { get; set; }
           required public string Email { get; set; }
        required public string Role { get; set; } = "student"; // "Student", "Instructor", etc.
            required public string Status { get; set; } = "active"; // "Active"
             public UserProfileDto? Profile { get; set; } = null;

        
    }
}
