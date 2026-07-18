using System;
using System.Collections.Generic;
using System.Text;

namespace DataAccess.Entities
{
    public class PaymentEntitiy
    {
        public int payment_id { get; set; } // Primary
                                            //
        public int user_id { get; set; } // Foreign Key (Users)
        public UserEntity user { get; set; }
        public int course_id { get; set; } // Foreign Key (Courses) — what was bought
        public CourseEntitiy course { get; set; }
        public int? enrollment_id { get; set; } // Foreign Key (Enrollments) — the enrollment it produced
        public EnrollmentEntitiy? enrollment { get; set; }
        public decimal amount { get; set; }
        public string currency { get; set; } = "USD";
        public string status { get; set; } = "pending";
        public string provider { get; set; } = "simulated";
        public string? provider_reference { get; set; } // fake txn id
        public DateTime payment_date { get; set; } = DateTime.UtcNow;
    }
}
