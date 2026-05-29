using System;
using System.Collections.Generic;
using System.Text;

namespace DataAccess.Dto
{
    public class LessonDto
    {
        public int LessonId { get; set; }
        public int SectionId { get; set; } = 0;
        public string Title { get; set; } = string.Empty;   
        public int SortOrder { get; set; } = 0;
        public List<ContentBlockDto> ContentBlocks { get; set; } = new();


    }
}
