using DataAccess.Entities.json;
using System;
using System.Collections.Generic;
using System.Text;

namespace DataAccess.Dto
{
    
        public class CourseDto
        {
            public int CourseId { get; set; }
        public int InstructorId { get; set; }

        public string Title { get; set; }
            public int CategoryId { get; set; }
            public string CategoryName { get; set; }
        public string code { get; set; }
        public string? description { get; set; }
        public decimal price { get; set; } = 0.00m;
        public string status { get; set; } = "draft";
        public string level { get; set; } = "beginner";
        public int estimated_duration_minutes { get; set; } = 0;
        public course_metadata course_metadata { get; set; } // Maps to JSONB
        public DateTime? published_date { get; set; }
    }
    
}
