using System;
using System.Collections.Generic;
using System.Text;

namespace DataAccess.Entities
{
    public class UserProfileEntity
    {
       required public int user_id { get;set; } 
        public  UserEntity user { get; set; } // Primary Key & Foreign Key (Users)
        public string? bio { get; set; }
        public string? image_url { get; set; }
        public string? display_name { get; set; }

 
    }
}
