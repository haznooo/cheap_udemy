namespace Business.Dto.Request
{
    public class DropEnrollmentRequest
    {
        // UserId is intentionally NOT here — the user is taken from the JWT, never the body.
        public int CourseId { get; set; }
    }
}
