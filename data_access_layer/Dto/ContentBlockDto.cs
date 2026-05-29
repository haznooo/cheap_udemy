using DataAccess.Entities.json;
using System;
using System.Collections.Generic;
using System.Text;

namespace DataAccess.Dto
{
    public class ContentBlockDto
    {
        public string BlockId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;

        // C# will automatically turn this into TextBlockData, ImageBlockData, etc.
        public BlockData Data { get; set; }
    }
}
