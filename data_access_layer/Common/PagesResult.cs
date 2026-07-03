using System;
using System.Collections.Generic;
using System.Text;

namespace DataAccess.Common
{

    public class clsPageResult
    {

        public class PageResult<T>
        {

            public IEnumerable<T> Items { get; set; } = Enumerable.Empty<T>();

            // how many pages the user requested
            public int PageNumber { get; set; }
            // how many records per page the user requested
            public int PageSize { get; set; }
            // total number of records in the database that match the query (useful for calculating next and previous pages)
            public int TotalCount { get; set; }

            // Helper logic stays in the model
            public int TotalPages => TotalCount > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 0;
            public bool HasPrevious => PageNumber > 1 && PageNumber <= TotalPages;
            public bool HasNext => PageNumber < TotalPages && TotalPages > 0;

            public PageResult() { }

            public PageResult(IEnumerable<T> items, int count, int pageNumber, int pageSize)
            {
                Items = items;
                TotalCount = count;
                PageNumber = pageNumber;
                PageSize = pageSize;
            }

        }

   
    }

}
