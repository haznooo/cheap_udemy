using System.Collections.Generic;
using System.Text.Json;
using DataAccess.Entities.json;

namespace Business.Common
{
    // Pulls the supabase-hosted media file names out of a lesson's content blocks,
    // so callers can best-effort delete those bucket objects when the blocks go away.
    // Shared by LessonService (per-lesson update/delete cleanup) and AdminService
    // (whole-course takedown) — kept here so the extraction logic lives in one place.
    public static class ContentBlockMedia
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            AllowOutOfOrderMetadataProperties = true
        };

        // File names of supabase-hosted media referenced by a lesson's stored blocks.
        public static HashSet<string> ExtractFileNames(List<ContentBlock>? blocks)
        {
            var names = new HashSet<string>();
            if (blocks == null) return names;

            foreach (var b in blocks)
            {
                try
                {
                    if (b.Data.ValueKind == JsonValueKind.Undefined || b.Data.ValueKind == JsonValueKind.Null) continue;
                    AddFileName(names, b.Data.Deserialize<BlockData>(JsonOptions));
                }
                catch (JsonException)
                {
                    // Unreadable block — nothing to clean up for it.
                }
            }

            return names;
        }

        // Only supabase-hosted media maps to a bucket object; external providers
        // (e.g. a youtube video id) are not ours to delete.
        public static void AddFileName(HashSet<string> names, BlockData? data)
        {
            if (data is ImageBlockData img && !string.IsNullOrWhiteSpace(img.Url))
                names.Add(img.Url);
            else if (data is VideoBlockData vid && vid.Provider == "supabase" && !string.IsNullOrWhiteSpace(vid.VideoId))
                names.Add(vid.VideoId);
        }
    }
}
