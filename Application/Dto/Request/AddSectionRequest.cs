using System;
using System.Collections.Generic;
using System.Text;

namespace Business.Dto.Request
{
    public class AddSectionRequest
    {
       public int CourseId { get; set; }
        public string Title { get; set; } = string.Empty;
        public int SortOrder { get; set; }
    }
}
