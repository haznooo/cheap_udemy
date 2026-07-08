
using System;
using System.Collections.Generic;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;

namespace DataAccess.Entities
{
    public class UserEntity
    {



     required  public int user_id { get; set; } // Primary Key
      required  public string username { get; set; }
      required  public string email { get; set; }
      // Nullable because the anonymize-on-delete trigger sets hashed_password = NULL
      // (column is VARCHAR(255) NULL). Modeling it as non-nullable made EF emit a
      // non-null-safe reader that threw InvalidCastException when reading a deleted
      // user's row. `required` still forces it to be set when creating a live user.
      required  public string? hashed_password { get; set; }
        required public DateTime create_date { get; set; }
        required public string status { get; set; } = "active";
        required public string role { get; set; } = "student";
        public UserProfileEntity UserProfile { get; set; } = null;
        public virtual ICollection<RefreshTokenEntity> RefreshTokens { get; set; } = new List<RefreshTokenEntity>();

     
    }
}
