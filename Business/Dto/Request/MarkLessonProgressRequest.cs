namespace Business.Dto.Request
{
    public class MarkLessonProgressRequest
    {
        // UserId is intentionally NOT here — the user is taken from the JWT, never the body.
        public int LessonId { get; set; }
    }
}
