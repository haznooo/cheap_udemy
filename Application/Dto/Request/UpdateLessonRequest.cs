namespace Business.Dto.Request
{
    public class UpdateLessonRequest
    {
        public string? Title { get; set; }
        public int? EstimatedDurationMinutes { get; set; }
        public List<ContentBlockRequest>? ContentBlocks { get; set; }
    }
}
