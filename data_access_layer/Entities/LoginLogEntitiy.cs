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
        public int? user_id { get; set; } // Foreign Key (nullable: unknown-email attempts are still logged)
        public UserEntity? user { get; set; }
        public string? attempted_identifier { get; set; } // what the caller typed (never the password)
        public IPAddress? ip_address { get; set; }
        public string? user_agent { get; set; }
        public string status { get; set; }
        public DateTime attempted_at { get; set; } = DateTime.UtcNow;
    }
}
