using DataAccess.Entities.json;
using System;
using System.Collections.Generic;
using System.Text;

namespace DataAccess.Entities
{
    public class LessonEntity
    {
        public int lesson_id { get; set; }
        public int section_id { get; set; }
        public SectionEntitiy section { get; set; }
        public string title { get; set; }

        public List<ContentBlock> content_blocks { get; set; } = new List<ContentBlock>();

        public LessonMetadata lesson_metadata { get; set; } = new();

        public int sort_order { get; set; }
        public int estimated_duration_minutes { get; set; } = 0;

        public DateTime created_at { get; set; } = DateTime.UtcNow;
        public DateTime? updated_at { get; set; } = DateTime.UtcNow;
    }
}
