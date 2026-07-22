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

    // A single row of a course's student roster (instructor/admin view of
    // GET api/Enrollments/course/{courseId}). Unlike EnrollmentDto (a user's own
    // enrollments, where the student is the caller), this carries the *student's*
    // identity so the instructor can actually display who is enrolled — but not the
    // course title, which the instructor already knows.
    public class CourseEnrollmentDto
    {
        public int EnrollmentId { get; set; }
        public int UserId { get; set; }
        public string Username { get; set; }
        public string? DisplayName { get; set; }
        public string? ImageUrl { get; set; }
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
