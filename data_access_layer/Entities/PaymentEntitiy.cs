using System;
using System.Collections.Generic;
using System.Text;

namespace DataAccess.Entities
{
    public class PaymentEntitiy
    {
        public int payment_id { get; set; } // Primary
                                            // 
        public decimal amount { get; set; }
        public DateTime payment_date { get; set; } = DateTime.UtcNow;
        public int user_id { get; set; } // Foreign Key
        public UserEntity user { get; set; }
    }
}
