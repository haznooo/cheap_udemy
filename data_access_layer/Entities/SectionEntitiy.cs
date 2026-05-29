using System;
using System.Collections.Generic;
using System.Text;

namespace DataAccess.Entities
{
    public class SectionEntitiy
    {
        public int section_id { get; set; } // Primary Key
        public int course_id { get; set; } // Foreign Key
        public CourseEntitiy course { get; set; }
        public string title { get; set; } = "Main";
        public int sort_order { get; set; } = 0;
    }
}
