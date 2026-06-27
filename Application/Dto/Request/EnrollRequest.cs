namespace Business.Dto.Request
{
    public class EnrollRequest
    {
        // UserId is intentionally NOT here — the enrolling user is taken from the JWT, never the body.
        public int CourseId { get; set; }
    }
}
