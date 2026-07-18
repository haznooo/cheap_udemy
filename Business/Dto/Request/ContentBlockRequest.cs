using DataAccess.Entities.json;

namespace Business.Dto.Request
{
    public class ContentBlockRequest
    {
        public string Type { get; set; } = string.Empty;
        public BlockData Data { get; set; } = null!;
    }
}
