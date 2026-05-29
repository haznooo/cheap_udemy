using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace DataAccess.Entities
{
    public class AdminActionEntitiy
    {
        public int id { get; set; } // Primary Key
        public int admin_id { get; set; } // Foreign Key (Users)
        public UserEntity admin {  get; set; }
        public string action_type { get; set; }
        public string trget_table { get; set; }
        public int target_id { get; set; }
        // Maps directly to JSONB
        public JsonDocument? old_value { get; set; }
        public JsonDocument? new_value { get; set; }
        public DateTime performed_at { get; set; } = DateTime.UtcNow;
    }
}
