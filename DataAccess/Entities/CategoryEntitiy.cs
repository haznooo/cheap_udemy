using System;
using System.Collections.Generic;
using System.Text;

namespace DataAccess.Entities
{
    public class CategoryEntitiy
    {
        public int category_id { get; set; } // Primary Key
        public string name { get; set; }
        public string slug { get; set; }
        public int? parent_id { get; set; } // Foreign Key (Self)
        public CategoryEntitiy parent { get; set; }
    }
}
