using System;
using System.Collections.Generic;
using System.Text;

namespace DataAccess.Entities
{
    public class UserLessonProgressEntitiy
    {
        public int user_id { get; set; } // Composite Primary Key Part 1
        public UserEntity user { get; set; }
        public int lesson_id { get; set; } // Composite Primary Key Part 2
        public LessonEntity lesson {  get; set; }
        public bool is_completed { get; set; } = false;
        public DateTime completed_at { get; set; } = DateTime.UtcNow;
    }
}
