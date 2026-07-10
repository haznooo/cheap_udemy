namespace DataAccess.Dto
{
    public class SectionDto
    {
        public int SectionId { get; set; }
        public int CourseId { get; set; }
        public string Title { get; set; } = string.Empty;
        public int SortOrder { get; set; } = 0;
    }
}
