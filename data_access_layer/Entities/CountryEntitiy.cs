using System;
using System.Collections.Generic;
using System.Text;

namespace DataAccess.Entities
{
    public class CountryEntitiy
    {
        public int country_id { get; set; } // Primary Key
        public string name { get; set; }
        public string iso_code { get; set; }
    }
}
