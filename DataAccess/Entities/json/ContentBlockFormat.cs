using System;
using System.Collections.Generic;
using System.Text;

namespace DataAccess.Entities.json
{
    using System.Text.Json.Serialization;

    // The "Base" data payload
    [JsonDerivedType(typeof(TextBlockData), typeDiscriminator: "text")]
    [JsonDerivedType(typeof(ImageBlockData), typeDiscriminator: "image")]
    [JsonDerivedType(typeof(VideoBlockData), typeDiscriminator: "video")]
    [JsonDerivedType(typeof(QuizBlockData), typeDiscriminator: "quiz")]
    public abstract class BlockData
    {
        // This can stay empty, it just serves as the contract for your shapes
    }

    public class TextBlockData : BlockData
    {
        public string Content { get; set; } = string.Empty;
    }

    public class ImageBlockData : BlockData
    {
        public string Url { get; set; } = string.Empty;
        public string Caption { get; set; } = string.Empty;
    }

    public class VideoBlockData : BlockData
    {
        public string Provider { get; set; } = string.Empty;
        public string VideoId { get; set; } = string.Empty;
    }

    public class QuizBlockData : BlockData
    {
        public string QuizId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
    }
}
