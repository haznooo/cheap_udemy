namespace Business.Dto.Request
{
    public class UpdateCourseRequest
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Code { get; set; }
        public decimal? Price { get; set; }
        public string? Level { get; set; }
        public int? CategoryId { get; set; }
    }
}
