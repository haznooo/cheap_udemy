using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
namespace DataAccess.Entities
{
    public class LoginLogEntitiy
    {
        public int id { get; set; } // Primary
                                    // 
        public int user_id { get; set; } // Foreign Key
        public UserEntity user { get; set; }
        public IPAddress? ip_dddress {  get; set; }  // Can use IPAddress type or string
        public string? user_agent { get; set; }
        public string status { get; set; }
        public DateTime attempted_at { get; set; } = DateTime.UtcNow;
    }
}
