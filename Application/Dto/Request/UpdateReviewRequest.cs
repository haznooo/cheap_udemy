namespace Business.Dto.Request
{
    public class UpdateReviewRequest
    {
        public short Rating { get; set; }
        public string? Comment { get; set; }
    }
}
