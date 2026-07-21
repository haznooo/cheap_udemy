namespace Business.Dto.Request
{
    public class CheckoutRequest
    {
        // UserId is intentionally NOT here — the buying user is taken from the JWT, never the body.
        public int CourseId { get; set; }
    }
}
