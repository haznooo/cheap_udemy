namespace Business.Dto.Request
{
    public class AddReviewRequest
    {
        public short Rating { get; set; }
        public string? Comment { get; set; }
    }
}
