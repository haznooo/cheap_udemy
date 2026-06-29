using Business.Common;
using Business.Dto.Request;
using Business.Services;
using DataAccess.Data;
using DataAccess.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using static Business.Common.clsPageResult;

namespace Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/Enrollments")]
    public class EnrollmentController(AppDbContext context) : ControllerBase
    {
        private int? CallerId =>
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int id) ? id : null;

        private string CallerRole =>
            User.FindFirstValue(ClaimTypes.Role) ?? "";

        [HttpPost("enroll")]
        public async Task<ActionResult<EnrollmentDto>> Enroll(EnrollRequest request)
        {
            if (CallerId is not int callerId) return Unauthorized();

            var service = new EnrollmentService(context);
            var result = await service.EnrollStudent(callerId, request);

            if (!result.IsSuccess)
            {
                return result.FailureType switch
                {
                    ErrorType.BadRequest => BadRequest(result.Errors),
                    ErrorType.Conflict => Conflict(result.Errors),
                    ErrorType.NotFound => NotFound(result.Errors),
                    ErrorType.Unauthorized => Forbid(),
                    _ => StatusCode(500, "An unexpected error occurred")
                };
            }

            return Ok(result.Value);
        }

        // The caller's own enrollments (identity from the access token).
        [HttpGet("me")]
        public async Task<ActionResult<PageResult<EnrollmentDto>>> GetMyEnrollments(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            if (CallerId is not int callerId) return Unauthorized();

            var service = new EnrollmentService(context);
            var result = await service.GetUserEnrollments(callerId, CallerRole, callerId, pageNumber, pageSize);

            if (!result.IsSuccess)
            {
                return result.FailureType switch
                {
                    ErrorType.BadRequest => BadRequest(result.Errors),
                    ErrorType.NotFound => NotFound(result.Errors),
                    ErrorType.Unauthorized => Forbid(),
                    _ => StatusCode(500, "An unexpected error occurred")
                };
            }

            return Ok(result.Value);
        }

        // Read another user's enrollments — admin only.
        [HttpGet("user/{userId:int}")]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult<PageResult<EnrollmentDto>>> GetUserEnrollments(
            int userId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            if (CallerId is not int callerId) return Unauthorized();

            var service = new EnrollmentService(context);
            var result = await service.GetUserEnrollments(callerId, CallerRole, userId, pageNumber, pageSize);

            if (!result.IsSuccess)
            {
                return result.FailureType switch
                {
                    ErrorType.BadRequest => BadRequest(result.Errors),
                    ErrorType.NotFound => NotFound(result.Errors),
                    ErrorType.Unauthorized => Forbid(),
                    _ => StatusCode(500, "An unexpected error occurred")
                };
            }

            return Ok(result.Value);
        }

        [HttpGet("course/{courseId}")]
        public async Task<ActionResult<PageResult<EnrollmentDto>>> GetCourseEnrollments(
            int courseId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            if (CallerId is not int callerId) return Unauthorized();

            var service = new EnrollmentService(context);
            var result = await service.GetCourseEnrollments(callerId, CallerRole, courseId, pageNumber, pageSize);

            if (!result.IsSuccess)
            {
                return result.FailureType switch
                {
                    ErrorType.BadRequest => BadRequest(result.Errors),
                    ErrorType.NotFound => NotFound(result.Errors),
                    ErrorType.Unauthorized => Forbid(),
                    _ => StatusCode(500, "An unexpected error occurred")
                };
            }

            return Ok(result.Value);
        }

        [HttpPost("progress/mark")]
        public async Task<ActionResult<EnrollmentDto>> MarkLessonProgress(MarkLessonProgressRequest request)
        {
            if (CallerId is not int callerId) return Unauthorized();

            var service = new EnrollmentService(context);
            var result = await service.MarkLessonProgress(callerId, request);

            if (!result.IsSuccess)
            {
                return result.FailureType switch
                {
                    ErrorType.BadRequest => BadRequest(result.Errors),
                    ErrorType.NotFound => NotFound(result.Errors),
                    ErrorType.Conflict => Conflict(result.Errors),
                    _ => StatusCode(500, "An unexpected error occurred")
                };
            }

            return Ok(result.Value);
        }

        [HttpGet("progress/{courseId}")]
        public async Task<ActionResult<List<LessonProgressDto>>> GetCourseProgress(int courseId)
        {
            if (CallerId is not int callerId) return Unauthorized();

            var service = new EnrollmentService(context);
            var result = await service.GetCourseProgress(callerId, courseId);

            if (!result.IsSuccess)
                return result.FailureType switch
                {
                    ErrorType.BadRequest => BadRequest(result.Errors),
                    ErrorType.NotFound => NotFound(result.Errors),
                    ErrorType.Unauthorized => Forbid(),
                    _ => StatusCode(500, "An unexpected error occurred")
                };

            return Ok(result.Value);
        }

        [HttpPost("drop")]
        public async Task<ActionResult<bool>> DropEnrollment(DropEnrollmentRequest request)
        {
            if (CallerId is not int callerId) return Unauthorized();

            var service = new EnrollmentService(context);
            var result = await service.DropEnrollment(callerId, request);

            if (!result.IsSuccess)
            {
                return result.FailureType switch
                {
                    ErrorType.BadRequest => BadRequest(result.Errors),
                    ErrorType.NotFound => NotFound(result.Errors),
                    _ => StatusCode(500, "An unexpected error occurred")
                };
            }

            return Ok(result.Value);
        }
    }
}
