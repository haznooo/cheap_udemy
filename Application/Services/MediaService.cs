using Microsoft.AspNetCore.Http;
using Supabase;

namespace Business.Services
{
    // The Supabase storage buckets this app uploads to. Names must match the buckets
    // created in the Supabase dashboard.
    public static class MediaBuckets
    {
        // Private bucket: paid course content (thumbnails, lesson images/videos).
        // Read back via short-lived signed URLs minted in LessonService.
        public const string CourseMedia = "course-media";

        // Public bucket: user avatars (image/jpeg + image/png only, 3 MB cap enforced
        // by the bucket itself). Read back via the permanent public object URL.
        public const string Avatars = "avatar";
    }

    // latter when i start having proper DI i will move this to somewhere else
    public interface IMediaService
    {
        Task<string> UploadFileAsync(IFormFile file, string bucketName);
    }

    public class SupabaseMediaService : IMediaService
    {
        private readonly Client _supabaseClient;

        public SupabaseMediaService(Client supabaseClient)
        {
            _supabaseClient = supabaseClient;
        }
        public async Task<string> UploadFileAsync(IFormFile file, string bucketName)
        {
            // 1. Read the file into a byte array
            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream);
            var fileBytes = memoryStream.ToArray();

            // 2. Generate a unique file name so we don't overwrite existing files
            var extension = Path.GetExtension(file.FileName);
            var uniqueFileName = $"{Guid.NewGuid()}{extension}";

            // 3. Upload. The real content type must be sent explicitly: the library
            // defaults to "text/plain", which buckets with MIME restrictions (avatar)
            // would reject, and which breaks image rendering from public URLs.
            await _supabaseClient.Storage
                .From(bucketName)
                .Upload(fileBytes, uniqueFileName, new Supabase.Storage.FileOptions
                {
                    Upsert = false,
                    ContentType = file.ContentType
                });

            // 4. Instead of returning a public URL, return ONLY the unique file name!
            // This is what gets saved to the DB (profile image_url, thumbnail_url,
            // lesson content blocks).
            return uniqueFileName;
        }
    }
}
