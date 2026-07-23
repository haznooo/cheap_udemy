using Business.Common;
using Business.Dto.Request;
using Business.Interfaces;
using Business.Services;
using DataAccess.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DataAccess.Dto;
using Business.Dto.Rsponse;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/Courses")]
    public class CourseController(ICourseService courseService, IMediaService mediaService) : ApiControllerBase
    {
        // 5 MB limit for thumbnails; images only.
        private const long MaxThumbnailSize = 5 * 1024 * 1024;
        private readonly string[] _allowedImageExtensions = { ".jpg", ".jpeg", ".png" };

        // Lesson media (images + short video) for a course's content blocks. Video gets a
        // much larger cap than images: a 30 MiB image is almost always an unoptimized upload,
        // while real short videos need the room. Video cap matches the course-media bucket's
        // file_size_limit exactly (30 MiB), so a file that passes won't be rejected by Supabase.
        private const long MaxMediaImageSize = 5 * 1024 * 1024;
        private const long MaxMediaVideoSize = 30 * 1024 * 1024;
        private readonly string[] _allowedMediaImageExtensions = { ".jpg", ".jpeg", ".png" };
        private readonly string[] _allowedMediaVideoExtensions = { ".mp4", ".mov" };

        [AllowAnonymous]
        [HttpPost("get")]
        public async Task<ActionResult<clsPageResult.PageResult<CourseDto>>> GetCourses(GetCoursesRequest request)
        {
            var Result = await courseService.GetAllCourses(request);

            if (!Result.IsSuccess) return MapFailure(Result);

            return Ok(Result.Value);
        }

        [Authorize]
        [HttpPost("add")]
        public async Task<ActionResult<CourseDto>> AddCourse(AddCourseRequest request)
        {
            // Instructor id is taken from the authenticated caller, never from the body.
            if (CallerId is not int instructorId) return MissingIdentity();

            var Result = await courseService.AddNewCourse(request, instructorId);

            if (!Result.IsSuccess) return MapFailure(Result);

            return Ok(Result.Value);
        }

        // Anonymous callers (and non-owners) only see published courses; the owning
        // instructor or an admin may also fetch their own draft/retired course here.
        [AllowAnonymous]
        [HttpGet("{courseId}")]
        public async Task<ActionResult<CourseDto>> GetCourse(int courseId)
        {
            bool isAdmin = User.IsInRole("admin");
            var Result = await courseService.GetCourseById(courseId, CallerId, isAdmin);

            if (!Result.IsSuccess) return MapFailure(Result);

            return Ok(Result.Value);
        }

        // Upload + attach a thumbnail to a course. Owner instructor or admin only.
        [Authorize]
        [HttpPut("{courseId}/thumbnail")]
        public async Task<ActionResult<ThumbnailUploadResponse>> SetThumbnail(int courseId, IFormFile file)
        {
            if (CallerId is not int callerId) return MissingIdentity();

            if (file == null || file.Length == 0)
            {
                return Problem(statusCode: StatusCodes.Status400BadRequest, detail: "No file was uploaded.");
            }
            if (file.Length > MaxThumbnailSize)
            {
                return Problem(statusCode: StatusCodes.Status400BadRequest, detail: $"File exceeds the maximum limit of {MaxThumbnailSize / (1024 * 1024)}MB.");
            }
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(extension) || !_allowedImageExtensions.Contains(extension))
            {
                return Problem(statusCode: StatusCodes.Status400BadRequest, detail: "Invalid file type. Only JPG and PNG are allowed.");
            }

            bool isAdmin = User.IsInRole("admin");

            // Verify ownership BEFORE uploading anything to storage, so a non-owner
            // can't write objects to the shared bucket on a course they don't own.
            var permission = await courseService.CheckCourseEditPermission(courseId, callerId, isAdmin);
            if (!permission.IsSuccess) return MapFailure(permission);

            // Ownership confirmed; upload then persist the returned file name.
            var fileName = await mediaService.UploadCourseThumbnailAsync(file);
            var Result = await courseService.SetThumbnail(courseId, callerId, isAdmin, fileName);

            if (!Result.IsSuccess) return MapFailure(Result);

            // The new name is persisted; the replaced file is now orphaned in the
            // bucket, so remove it (best-effort — a leftover file is harmless).
            if (!string.IsNullOrEmpty(Result.Value))
            {
                await mediaService.DeleteCourseThumbnailAsync(Result.Value);
            }

            return Ok(new ThumbnailUploadResponse(fileName));
        }

        // Upload a media file (image/video) to be referenced from a lesson's content blocks.
        // Only the owning instructor (or an admin) may upload media for a course — verified
        // BEFORE touching storage so non-owners can't write to the shared bucket.
        [Authorize]
        [HttpPost("{courseId}/media")]
        // Kestrel's default request-body cap (~28.6 MB) is below our 30 MiB media limit,
        // so raise it for just this endpoint (with a little headroom for multipart overhead)
        // — otherwise a legitimate ~30 MiB upload is rejected before reaching this action.
        [RequestSizeLimit(32 * 1024 * 1024)]
        public async Task<ActionResult<MediaUploadResponse>> UploadCourseMedia(int courseId, IFormFile file)
        {
            if (CallerId is not int callerId) return MissingIdentity();

            if (file == null || file.Length == 0)
            {
                return Problem(statusCode: StatusCodes.Status400BadRequest, detail: "No file was uploaded.");
            }
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            bool isVideo = _allowedMediaVideoExtensions.Contains(extension);
            bool isImage = _allowedMediaImageExtensions.Contains(extension);
            if (string.IsNullOrEmpty(extension) || (!isVideo && !isImage))
            {
                return Problem(statusCode: StatusCodes.Status400BadRequest, detail: "Invalid file type. Only JPG, PNG, MP4, and MOV are allowed.");
            }

            // Per-type size cap: video up to 30 MiB, images held to 5 MB.
            long maxSize = isVideo ? MaxMediaVideoSize : MaxMediaImageSize;
            if (file.Length > maxSize)
            {
                return Problem(statusCode: StatusCodes.Status400BadRequest, detail: $"File exceeds the maximum limit of {maxSize / (1024 * 1024)}MB for {(isVideo ? "video" : "image")} files.");
            }

            bool isAdmin = User.IsInRole("admin");

            var permission = await courseService.CheckCourseEditPermission(courseId, callerId, isAdmin);
            if (!permission.IsSuccess) return MapFailure(permission);

            var fileName = await mediaService.UploadCourseMediaAsync(file);
            return Ok(new MediaUploadResponse(fileName));
        }

        [Authorize]
        [HttpGet("{courseId}/lessons")]
        public async Task<ActionResult<clsPageResult.PageResult<LessonDto>>> GetCourseLessons(
            int courseId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            if (CallerId is not int callerId) return MissingIdentity();

            bool isAdmin = User.IsInRole("admin");
            var Result = await courseService.GetCourseLessons(courseId, callerId, isAdmin, pageNumber, pageSize);

            if (!Result.IsSuccess) return MapFailure(Result);

            return Ok(Result.Value);
        }

        [Authorize]
        [HttpGet("{courseId}/sections")]
        public async Task<ActionResult<clsPageResult.PageResult<SectionResponse>>> GetCourseSections(
            int courseId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            if (CallerId is not int callerId) return MissingIdentity();

            bool isAdmin = User.IsInRole("admin");
            var Result = await courseService.GetCourseSections(courseId, callerId, isAdmin, pageNumber, pageSize);

            if (!Result.IsSuccess) return MapFailure(Result);

            return Ok(Result.Value);
        }

        // The caller's own courses as an instructor (identity from the access token).
        [Authorize]
        [HttpGet("instructor/me")]
        public async Task<ActionResult<clsPageResult.PageResult<CourseDto>>> GetMyInstructorCourses(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            if (CallerId is not int callerId) return MissingIdentity();

            var result = await courseService.GetInstructorCourses(callerId, callerId, CallerRole, pageNumber, pageSize);

            if (!result.IsSuccess) return MapFailure(result);

            return Ok(result.Value);
        }

        // Read another instructor's courses — anonymous (published only). An owner/admin
        // caller sees drafts too; anonymous/other callers (callerId 0) see published only.
        [AllowAnonymous]
        [HttpGet("instructor/{instructorId:int}")]
        public async Task<ActionResult<clsPageResult.PageResult<CourseDto>>> GetInstructorCourses(
            int instructorId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            var result = await courseService.GetInstructorCourses(instructorId, CallerId ?? 0, CallerRole, pageNumber, pageSize);

            if (!result.IsSuccess) return MapFailure(result);

            return Ok(result.Value);
        }

        // Public info about an instructor (username + display name + avatar) —
        // anonymous, e.g. to show who published a course. No email/role/status.
        [AllowAnonymous]
        [HttpGet("instructor/{instructorId:int}/info")]
        public async Task<ActionResult<PublicInstructorResponse>> GetInstructorInfo(int instructorId)
        {
            var result = await courseService.GetInstructorInfo(instructorId);

            if (!result.IsSuccess) return MapFailure(result);

            return Ok(result.Value);
        }

        [Authorize]
        [HttpPatch("{courseId}")]
        public async Task<ActionResult<CourseDto>> UpdateCourse(int courseId, [FromBody] UpdateCourseRequest request)
        {
            if (CallerId is not int callerId) return MissingIdentity();

            bool isAdmin = User.IsInRole("admin");
            var result = await courseService.UpdateCourse(courseId, request, callerId, isAdmin);

            if (!result.IsSuccess) return MapFailure(result);

            return Ok(result.Value);
        }

        [Authorize]
        [HttpPost("{courseId}/publish")]
        public async Task<ActionResult<CourseDto>> PublishCourse(int courseId)
        {
            if (CallerId is not int callerId) return MissingIdentity();

            bool isAdmin = User.IsInRole("admin");
            var result = await courseService.PublishCourse(courseId, callerId, isAdmin);

            if (!result.IsSuccess) return MapFailure(result);

            return Ok(result.Value);
        }

        [Authorize]
        [HttpPost("{courseId}/unpublish")]
        public async Task<ActionResult<CourseDto>> UnpublishCourse(int courseId)
        {
            if (CallerId is not int callerId) return MissingIdentity();

            bool isAdmin = User.IsInRole("admin");
            var result = await courseService.UnpublishCourse(courseId, callerId, isAdmin);

            if (!result.IsSuccess) return MapFailure(result);

            return Ok(result.Value);
        }

        [Authorize]
        [HttpPost("section/add")]
        public async Task<ActionResult<SectionResponse>> AddSection(AddSectionRequest request)
        {
            if (CallerId is not int callerId) return MissingIdentity();

            bool isAdmin = User.IsInRole("admin");

            // Ownership (owning instructor or admin) is checked inside the service,
            // same as UpdateSection/DeleteSection.
            var Result = await courseService.AddNewSection(request, callerId, isAdmin);

            if (!Result.IsSuccess) return MapFailure(Result);

            return Ok(Result.Value);
        }

        // Rename/reorder a section. Owner/admin only, allowed regardless of enrollment
        // (same as lesson edits — only course-level hard delete is enrollment-gated).
        [Authorize]
        [HttpPut("section/{sectionId}")]
        public async Task<ActionResult<SectionResponse>> UpdateSection(int sectionId, [FromBody] UpdateSectionRequest request)
        {
            if (CallerId is not int callerId) return MissingIdentity();

            bool isAdmin = User.IsInRole("admin");
            var result = await courseService.UpdateSection(sectionId, request, callerId, isAdmin);

            if (!result.IsSuccess) return MapFailure(result);

            return Ok(result.Value);
        }

        // Hard-delete a section (and, via DB cascade, its lessons). Owner/admin only,
        // allowed regardless of enrollment.
        [Authorize]
        [HttpDelete("section/{sectionId}")]
        public async Task<ActionResult> DeleteSection(int sectionId)
        {
            if (CallerId is not int callerId) return MissingIdentity();

            bool isAdmin = User.IsInRole("admin");
            var result = await courseService.DeleteSection(sectionId, callerId, isAdmin);

            if (!result.IsSuccess) return MapFailure(result);

            return NoContent();
        }

        // Hard-delete (soft-delete: deleted_at + removal_reason) a course with no enrollment
        // history. If anyone has ever enrolled (any status, including dropped), this is
        // blocked (409) — unpublish instead.
        [Authorize]
        [HttpDelete("{courseId}")]
        public async Task<ActionResult> DeleteCourse(int courseId, [FromBody] DeleteCourseRequest? request)
        {
            if (CallerId is not int callerId) return MissingIdentity();

            bool isAdmin = User.IsInRole("admin");
            var result = await courseService.DeleteCourse(courseId, callerId, isAdmin, request?.RemovalReason);

            if (!result.IsSuccess) return MapFailure(result);

            return NoContent();
        }
    }
}
