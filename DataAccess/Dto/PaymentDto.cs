namespace DataAccess.Dto
{
    public class PaymentDto
    {
        public int PaymentId { get; set; }
        public int UserId { get; set; }
        public int CourseId { get; set; }
        public string CourseTitle { get; set; }
        public int? EnrollmentId { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; }
        public string Status { get; set; }
        public string Provider { get; set; }
        public string? ProviderReference { get; set; }
        public DateTime PaymentDate { get; set; }
    }
}
