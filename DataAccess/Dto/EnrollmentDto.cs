namespace DataAccess.Dto
{
    public class EnrollmentDto
    {
        public int EnrollmentId { get; set; }
        public int UserId { get; set; }
        public int CourseId { get; set; }
        public string CourseTitle { get; set; }
        public DateTime EnrollmentDate { get; set; }
        public DateTime? CompletionDate { get; set; }
        public string Status { get; set; }
        public decimal ProgressPercentage { get; set; }
    }

    // Minimal course fields needed to decide whether a student may enroll.
    public class CourseEnrollmentInfoDto
    {
        public int InstructorId { get; set; }
        public string Status { get; set; }
        public bool IsDeleted { get; set; }
        public decimal Price { get; set; }
    }
}
