
using DataAccess.Entities.json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using System.Text.Json;

namespace DataAccess.Entities
{
    public class CourseEntitiy
    {
        public int course_id { get; set; } // Primary
                                           // 
        public int category_id { get; set; } // Foreign Key = null
        public CategoryEntitiy category { get; set; }
        public int instructor_id { get; set; } // Foreign Key (Users)
        public UserEntity instructor { get; set; }
        public string title { get; set; }
        public string code { get; set; }
        public string? description { get; set; }
        public string? thumbnail_url { get; set; }
        public decimal price { get; set; } = 0.00m;
        public string status { get; set; } = "draft";
        public string level { get; set; } = "beginner";
        public string? removal_reason { get; set; }
        public DateTime? deleted_at { get; set; }
        public int estimated_duration_minutes { get; set; } = 0;
        public decimal avg_rating { get; set; } = 0.00m;
        public int reviews_count { get; set; } = 0;
        [Column(TypeName = "jsonb")]
        public course_metadata course_metadata { get; set; } // Maps to JSONB
        public DateTime created_date { get; set; } = DateTime.UtcNow;
        public DateTime? published_date { get; set; }
        public DateTime updated_at { get; set; } = DateTime.UtcNow;

        public virtual ICollection<EnrollmentEntitiy> enrollments { get; set; } = new List<EnrollmentEntitiy>();
    }
}
