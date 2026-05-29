using System;
using System.Collections.Generic;
using System.Text;

namespace DataAccess.Dto
{
    public class UserProfileDto
    {
     public   string? DisplayName { get; set; }
     public   string? Bio { get; set; }
        public string? ImageUrl { get; set; }
        public int? CountryId { get; set; }
        public string? CountryName { get; set; }
        public string? CountryIsoCode { get; set; }
        
    }
}
