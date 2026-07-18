using Microsoft.AspNetCore.Http;
using Supabase;

namespace Business.Services
{
    // The Supabase storage buckets this app uploads to. Names must match the buckets
    // created in the Supabase dashboard. Callers never pass a bucket name — each
    // IMediaService method is bound to exactly one bucket, so an avatar can't
    // accidentally land in the private course bucket (or vice versa).
    public static class MediaBuckets
    {
        // Private bucket: paid lesson content (images/videos in content blocks).
        // Read back via short-lived signed URLs minted in LessonService.
        public const string CourseMedia = "course-media";

        // Public bucket: course thumbnails (image/jpeg + image/png, 5 MB cap enforced
        // by the bucket itself). Thumbnails appear on the anonymous catalog, so they
        // must be publicly readable — a private bucket would need signing on every
        // course list row. Read back via the permanent public object URL.
        public const string CourseThumbnails = "course-thumbnail";

        // Public bucket: user avatars (image/jpeg + image/png only, 3 MB cap enforced
        // by the bucket itself). Read back via the permanent public object URL.
        public const string Avatars = "avatar";
    }

    // latter when i start having proper DI i will move this to somewhere else
    public interface IMediaService
    {
        Task<string> UploadAvatarAsync(IFormFile file);
        Task<string> UploadCourseThumbnailAsync(IFormFile file);
        Task<string> UploadCourseMediaAsync(IFormFile file);

        // Best-effort deletes: they never throw. A leaked file is harmless, so a
        // storage hiccup during cleanup must never fail the caller's request.
        Task<bool> DeleteAvatarAsync(string fileName);
        Task<bool> DeleteCourseThumbnailAsync(string fileName);
        Task<bool> DeleteCourseMediaAsync(string fileName);
    }

    public class SupabaseMediaService : IMediaService
    {
        private readonly Client _supabaseClient;

        public SupabaseMediaService(Client supabaseClient)
        {
            _supabaseClient = supabaseClient;
        }

        public Task<string> UploadAvatarAsync(IFormFile file) =>
            UploadToBucketAsync(file, MediaBuckets.Avatars);

        public Task<string> UploadCourseThumbnailAsync(IFormFile file) =>
            UploadToBucketAsync(file, MediaBuckets.CourseThumbnails);

        public Task<string> UploadCourseMediaAsync(IFormFile file) =>
            UploadToBucketAsync(file, MediaBuckets.CourseMedia);

        public Task<bool> DeleteAvatarAsync(string fileName) =>
            DeleteFromBucketAsync(fileName, MediaBuckets.Avatars);

        public Task<bool> DeleteCourseThumbnailAsync(string fileName) =>
            DeleteFromBucketAsync(fileName, MediaBuckets.CourseThumbnails);

        public Task<bool> DeleteCourseMediaAsync(string fileName) =>
            DeleteFromBucketAsync(fileName, MediaBuckets.CourseMedia);

        private async Task<string> UploadToBucketAsync(IFormFile file, string bucketName)
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

        private async Task<bool> DeleteFromBucketAsync(string fileName, string bucketName)
        {
            // Stored names are server-generated GUIDs; anything with a path separator
            // or scheme is an external URL that slipped into a content block — not ours
            // to delete (and Remove would just error on it anyway).
            if (string.IsNullOrWhiteSpace(fileName) || fileName.Contains('/') || fileName.Contains('\\'))
                return false;

            try
            {
                await _supabaseClient.Storage
                    .From(bucketName)
                    .Remove(new List<string> { fileName });
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to delete '{fileName}' from bucket '{bucketName}': {ex.Message}");
                return false;
            }
        }
    }
}
