using System;
using System.Collections.Generic;
using System.Text;

namespace Business.Dto.Request
{
    public class GetCoursesRequest
    {
        public int PageNumber { get; set; }
        public int PageSize { get; set; }

        // All optional. Search uses the gin_trgm_ops index on courses.title.
        public string? SearchTerm { get; set; }
        public int? CategoryId { get; set; }
        public string? Level { get; set; }          // beginner | intermediate | advanced
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public string? SortBy { get; set; }          // newest | price_asc | price_desc | rating (defaults to relevance when searching, else newest)
    }
}
