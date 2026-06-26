using System;
using System.Collections.Generic;
using System.Text;

namespace Business.Dto.Request
{
    public class AddCourseRequest
    {
        // InstructorId is intentionally NOT here: it is derived from the caller's JWT,
        // never trusted from the request body.
        public string Title { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public string Description { get; set; }
        public string Code { get; set; }
        public decimal Price { get; set; } = 0.00m;
        public string Status { get; set; } = "draft";
        public string level { get; set; } = "beginner"; 

    }
}
