namespace Business.Dto.Request
{
    public class LessonRequest
    {
        public int SectionId { get; set; }
        public string Title { get; set; } = string.Empty;
        public List<ContentBlockRequest> ContentBlocks { get; set; } = new();
    }
}
