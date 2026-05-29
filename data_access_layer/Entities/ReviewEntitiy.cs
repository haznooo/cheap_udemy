using System;
using System.Collections.Generic;
using System.Text;

namespace DataAccess.Entities
{
    public class ReviewEntitiy
    {
        public int review_id { get; set; } // Primary Key
        public int course_id { get; set; } // Foreign Key
        public CourseEntitiy course { get; set; }
        public int user_id { get; set; } // Foreign Key
        public UserEntity user { get; set; }
        public short rating { get; set; } // 1 to 5
        public string? comment { get; set; }
        public DateTime created_at { get; set; } = DateTime.UtcNow;
    }
}
