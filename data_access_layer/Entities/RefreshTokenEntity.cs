namespace DataAccess.Entities
{
    public class RefreshTokenEntity
    {

       public int token_id { get; set; }
        public int user_id { get;  set; }
        public UserEntity user { get;  set; } = null;
        public string token_hash{ get; set; }
        public string device_info{ get; set; }
        public DateTime expires_at{ get; set; }
        public DateTime created_at{ get; set; }
        public DateTime? revoked_at{ get; set; }
        public  bool is_used{ get; set; }
        public int? replaced_by_id{ get; set; }
        public bool chain_breached{ get; set; }
        public DateTime? last_used_at { get; set; } = null;
        public string ip_address;

 

    }
    }

