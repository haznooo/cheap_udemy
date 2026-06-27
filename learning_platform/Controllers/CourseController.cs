using System.Security.Claims;
using Business.Common;
using Business.Dto.Request;
using Business.Services;
using DataAccess.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DataAccess.Dto;
using DataAccess.Entities;

namespace Api.Controllers
{
    [ApiController]
    //  [Route("[controller]")] // Sets the route for this controller to "students", based on the controller name.
    [Route("api/Courses")]
    public class CourseController(AppDbContext context, IMediaService mediaService) : ControllerBase
    {
        // 5 MB limit for thumbnails; images only.
        private const long MaxThumbnailSize = 5 * 1024 * 1024;
        private readonly string[] _allowedImageExtensions = { ".jpg", ".jpeg", ".png" };

        [AllowAnonymous]
        [HttpPost("get")]
        public async Task<ActionResult<clsPageResult.PageResult<CourseDto>>> GetCourses(GetCoursesRequest request)
        {

            var courseService = new CourseService(context);

            var Result = await courseService.GetAllCourses(request);

            if (!Result.IsSuccess)
            {
                // Handle all failure types in one switch expression
                return Result.FailureType switch
                {
                    ErrorType.NotFound => NotFound(Result.Errors),
                    ErrorType.BadRequest => BadRequest(Result.Errors),
                    ErrorType.Conflict => Conflict(Result.Errors),
                    ErrorType.Unauthorized => Unauthorized(Result.Errors),
                    _ => StatusCode(500, "An unexpected error occurred")
                };
            }

            return Ok(Result.Value);
        }

        [Authorize]
        [HttpPost("add")]
        public async Task<ActionResult<clsPageResult.PageResult<CourseDto>>> AddCourse(AddCourseRequest request)
        {
            // Instructor id is taken from the authenticated caller, never from the body.
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int instructorId))
            {
                return Unauthorized("Invalid or missing user identity.");
            }

            var courseServic = new CourseService(context);
            var Result = await courseServic.AddNewCourse(request, instructorId);

            if (!Result.IsSuccess)
            {
                // Handle all failure types in one switch expression
                return Result.FailureType switch
                {
                    ErrorType.NotFound => NotFound(Result.Errors),
                    ErrorType.BadRequest => BadRequest(Result.Errors),
                    ErrorType.Conflict => Conflict(Result.Errors),
                    ErrorType.Unauthorized => Unauthorized(Result.Errors),
                    _ => StatusCode(500, "An unexpected error occurred")
                };
            }

            return Ok(Result.Value);
        }

        [AllowAnonymous]
        [HttpGet("{courseId}")]
        public async Task<ActionResult<CourseDto>> GetCourse(int courseId)
        {
            var courseService = new CourseService(context);
            var Result = await courseService.GetCourseById(courseId);

            if (!Result.IsSuccess)
            {
                return Result.FailureType switch
                {
                    ErrorType.NotFound => NotFound(Result.Errors),
                    ErrorType.BadRequest => BadRequest(Result.Errors),
                    ErrorType.Conflict => Conflict(Result.Errors),
                    ErrorType.Unauthorized => Unauthorized(Result.Errors),
                    _ => StatusCode(500, "An unexpected error occurred")
                };
            }

            return Ok(Result.Value);
        }

        // Upload + attach a thumbnail to a course. Owner instructor or admin only.
        [Authorize]
        [HttpPut("{courseId}/thumbnail")]
        public async Task<ActionResult<CourseDto>> SetThumbnail(int courseId, IFormFile file)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int callerId))
            {
                return Unauthorized("Invalid or missing user identity.");
            }

            if (file == null || file.Length == 0)
            {
                return BadRequest("No file was uploaded.");
            }
            if (file.Length > MaxThumbnailSize)
            {
                return BadRequest($"File exceeds the maximum limit of {MaxThumbnailSize / (1024 * 1024)}MB.");
            }
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(extension) || !_allowedImageExtensions.Contains(extension))
            {
                return BadRequest("Invalid file type. Only JPG and PNG are allowed.");
            }

            var courseService = new CourseService(context);
            bool isAdmin = User.IsInRole("admin");

            // Verify ownership BEFORE uploading anything to storage, so a non-owner
            // can't write objects to the shared bucket on a course they don't own.
            var permission = await courseService.CheckCourseEditPermission(courseId, callerId, isAdmin);
            if (!permission.IsSuccess)
            {
                return permission.FailureType switch
                {
                    ErrorType.NotFound => NotFound(permission.Errors),
                    ErrorType.BadRequest => BadRequest(permission.Errors),
                    ErrorType.Conflict => Conflict(permission.Errors),
                    ErrorType.Unauthorized => Unauthorized(permission.Errors),
                    _ => StatusCode(500, "An unexpected error occurred")
                };
            }

            // Ownership confirmed; upload then persist the returned file name.
            var fileName = await mediaService.UploadFileAsync(file);
            var Result = await courseService.SetThumbnail(courseId, callerId, isAdmin, fileName);

            if (!Result.IsSuccess)
            {
                return Result.FailureType switch
                {
                    ErrorType.NotFound => NotFound(Result.Errors),
                    ErrorType.BadRequest => BadRequest(Result.Errors),
                    ErrorType.Conflict => Conflict(Result.Errors),
                    ErrorType.Unauthorized => Unauthorized(Result.Errors),
                    _ => StatusCode(500, "An unexpected error occurred")
                };
            }

            return Ok(new { thumbnail = fileName });
        }

        [AllowAnonymous]
        [HttpGet("{courseId}/lessons")]
        public async Task<ActionResult<List<LessonDto>>> GetCourseLessons(int courseId)
        {
            var courseService = new CourseService(context);
            var Result = await courseService.GetCourseLessons(courseId);

            if (!Result.IsSuccess)
            {
                return Result.FailureType switch
                {
                    ErrorType.NotFound => NotFound(Result.Errors),
                    ErrorType.BadRequest => BadRequest(Result.Errors),
                    ErrorType.Conflict => Conflict(Result.Errors),
                    ErrorType.Unauthorized => Unauthorized(Result.Errors),
                    _ => StatusCode(500, "An unexpected error occurred")
                };
            }

            return Ok(Result.Value);
        }

        [Authorize]
        [HttpPost("{courseId}/publish")]
        public async Task<ActionResult<CourseDto>> PublishCourse(int courseId)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int callerId))
                return Unauthorized("Invalid or missing user identity.");

            bool isAdmin = User.IsInRole("admin");
            var courseService = new CourseService(context);
            var result = await courseService.PublishCourse(courseId, callerId, isAdmin);

            if (!result.IsSuccess)
                return result.FailureType switch
                {
                    ErrorType.NotFound => NotFound(result.Errors),
                    ErrorType.BadRequest => BadRequest(result.Errors),
                    ErrorType.Conflict => Conflict(result.Errors),
                    ErrorType.Unauthorized => Unauthorized(result.Errors),
                    _ => StatusCode(500, "An unexpected error occurred")
                };

            return Ok(result.Value);
        }

        [Authorize]
        [HttpPost("{courseId}/unpublish")]
        public async Task<ActionResult<CourseDto>> UnpublishCourse(int courseId)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int callerId))
                return Unauthorized("Invalid or missing user identity.");

            bool isAdmin = User.IsInRole("admin");
            var courseService = new CourseService(context);
            var result = await courseService.UnpublishCourse(courseId, callerId, isAdmin);

            if (!result.IsSuccess)
                return result.FailureType switch
                {
                    ErrorType.NotFound => NotFound(result.Errors),
                    ErrorType.BadRequest => BadRequest(result.Errors),
                    ErrorType.Conflict => Conflict(result.Errors),
                    ErrorType.Unauthorized => Unauthorized(result.Errors),
                    _ => StatusCode(500, "An unexpected error occurred")
                };

            return Ok(result.Value);
        }

        [Authorize]
        [HttpPost("section/add")]
        public async Task<ActionResult<SectionEntitiy>> AddSection(AddSectionRequest request)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int callerId))
            {
                return Unauthorized("Invalid or missing user identity.");
            }

            var courseServic = new CourseService(context);
            bool isAdmin = User.IsInRole("admin");

            // Only the owning instructor (or an admin) may add sections to a course.
            var permission = await courseServic.CheckCourseEditPermission(request.CourseId, callerId, isAdmin);
            if (!permission.IsSuccess)
            {
                return permission.FailureType switch
                {
                    ErrorType.NotFound => NotFound(permission.Errors),
                    ErrorType.BadRequest => BadRequest(permission.Errors),
                    ErrorType.Conflict => Conflict(permission.Errors),
                    ErrorType.Unauthorized => Unauthorized(permission.Errors),
                    _ => StatusCode(500, "An unexpected error occurred")
                };
            }

            var Result = await courseServic.AddNewSection(request);

            if (!Result.IsSuccess)
            {
                // Handle all failure types in one switch expression
                return Result.FailureType switch
                {
                    ErrorType.NotFound => NotFound(Result.Errors),
                    ErrorType.BadRequest => BadRequest(Result.Errors),
                    ErrorType.Conflict => Conflict(Result.Errors),
                    ErrorType.Unauthorized => Unauthorized(Result.Errors),
                    _ => StatusCode(500, "An unexpected error occurred")
                };
            }

            return Ok(Result.Value);
        }
    }
}
