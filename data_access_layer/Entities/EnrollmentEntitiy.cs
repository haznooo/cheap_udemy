using System;
using System.Collections.Generic;
using System.Text;

namespace DataAccess.Entities
{
    public class EnrollmentEntitiy
    {
        public int enrollment_id { get; set; } // Primary Key
        public int sser_id { get; set; } // Foreign Key
        public UserEntity user { get; set; }
        public int course_id { get; set; } // Foreign Key
        public CourseEntitiy course { get; set; }
        public DateTime enrollment_date { get; set; } = DateTime.UtcNow;
        public DateTime? completion_date { get; set; }
        public string status { get; set; } = "active";
        public decimal progress_percentage { get; set; } = 0.00m;
    }
}
