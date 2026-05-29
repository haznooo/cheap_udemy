using System;
using System.Collections.Generic;
using System.Text;

namespace DataAccess.Entities.json
{
    public class ContentBlock
    {

        public string BlockId { get; set; }
        public string Type { get; set; } //"text", "image", "video", "quiz"

        // Using JsonElement lets EF/System.Text.Json handle the 
        // different shapes of "data" without crashing.
        public System.Text.Json.JsonElement Data { get; set; }
    }
}
