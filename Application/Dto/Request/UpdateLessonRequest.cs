namespace Business.Dto.Request
{
    public class UpdateLessonRequest
    {
        public string? Title { get; set; }
        public int? EstimatedDurationMinutes { get; set; }
        public List<ContentBlockRequest>? ContentBlocks { get; set; }
        // Reorder within the section (unique per section — a collision is a 409,
        // same contract as section reorder).
        public int? SortOrder { get; set; }
    }
}
