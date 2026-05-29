using System;
using System.Collections.Generic;
using System.Text;

namespace Business.Dto.Request
{
    public class GetCoursesRequest
    {
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
    }
}
