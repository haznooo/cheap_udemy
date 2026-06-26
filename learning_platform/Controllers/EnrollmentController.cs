using Business.Common;
using Business.Dto.Request;
using Business.Services;
using DataAccess.Data;
using DataAccess.Dto;
using Microsoft.AspNetCore.Mvc;
using static Business.Common.clsPageResult;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/Enrollments")]
    public class EnrollmentController(AppDbContext context) : ControllerBase
    {
        [HttpPost("enroll")]
        public async Task<ActionResult<EnrollmentDto>> Enroll(EnrollRequest request)
        {
            var service = new EnrollmentService(context);
            var result = await service.EnrollStudent(request);

            if (!result.IsSuccess)
            {
                return result.FailureType switch
                {
                    ErrorType.BadRequest => BadRequest(result.Errors),
                    ErrorType.Conflict => Conflict(result.Errors),
                    ErrorType.NotFound => NotFound(result.Errors),
                    _ => StatusCode(500, "An unexpected error occurred")
                };
            }

            return Ok(result.Value);
        }

        [HttpGet("user/{userId}")]
        public async Task<ActionResult<PageResult<EnrollmentDto>>> GetUserEnrollments(
            int userId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            var service = new EnrollmentService(context);
            var result = await service.GetUserEnrollments(userId, pageNumber, pageSize);

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

        [HttpGet("course/{courseId}")]
        public async Task<ActionResult<PageResult<EnrollmentDto>>> GetCourseEnrollments(
            int courseId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            var service = new EnrollmentService(context);
            var result = await service.GetCourseEnrollments(courseId, pageNumber, pageSize);

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

        [HttpPost("progress/mark")]
        public async Task<ActionResult<EnrollmentDto>> MarkLessonProgress(MarkLessonProgressRequest request)
        {
            var service = new EnrollmentService(context);
            var result = await service.MarkLessonProgress(request);

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

        [HttpPost("drop")]
        public async Task<ActionResult<bool>> DropEnrollment(DropEnrollmentRequest request)
        {
            var service = new EnrollmentService(context);
            var result = await service.DropEnrollment(request);

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
