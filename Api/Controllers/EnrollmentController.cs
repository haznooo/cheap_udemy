using Business.Common;
using Business.Dto.Request;
using Business.Interfaces;
using DataAccess.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static DataAccess.Common.clsPageResult;

namespace Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/Enrollments")]
    public class EnrollmentController(IEnrollmentService enrollmentService) : ApiControllerBase
    {
        [HttpPost("enroll")]
        public async Task<ActionResult<EnrollmentDto>> Enroll(EnrollRequest request)
        {
            if (CallerId is not int callerId) return MissingIdentity();

            var result = await enrollmentService.EnrollStudent(callerId, request);

            if (!result.IsSuccess) return MapFailure(result);

            return Ok(result.Value);
        }

        // The caller's own enrollments (identity from the access token).
        [HttpGet("me/enrollments")]
        public async Task<ActionResult<PageResult<EnrollmentDto>>> GetMyEnrollments(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            if (CallerId is not int callerId) return MissingIdentity();

            // Library view: hide enrollments whose course has been taken down / deleted.
            var result = await enrollmentService.GetUserEnrollments(callerId, CallerRole, callerId, pageNumber, pageSize, excludeDeletedCourses: true);

            if (!result.IsSuccess) return MapFailure(result);

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
            if (CallerId is not int callerId) return MissingIdentity();

            // Admin audit view: full history, including enrollments in deleted courses.
            var result = await enrollmentService.GetUserEnrollments(callerId, CallerRole, userId, pageNumber, pageSize, excludeDeletedCourses: false);

            if (!result.IsSuccess) return MapFailure(result);

            return Ok(result.Value);
        }

        // The course's student roster — instructor (owner) or admin only. Returns each
        // student's identity (username/display name/avatar) alongside their progress,
        // so the instructor can see *who* is enrolled, not just user ids.
        [HttpGet("course/{courseId}")]
        public async Task<ActionResult<PageResult<CourseEnrollmentDto>>> GetCourseEnrollments(
            int courseId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            if (CallerId is not int callerId) return MissingIdentity();

            var result = await enrollmentService.GetCourseEnrollments(callerId, CallerRole, courseId, pageNumber, pageSize);

            if (!result.IsSuccess) return MapFailure(result);

            return Ok(result.Value);
        }

        [HttpPost("progress/mark")]
        public async Task<ActionResult<EnrollmentDto>> MarkLessonProgress(MarkLessonProgressRequest request)
        {
            if (CallerId is not int callerId) return MissingIdentity();

            var result = await enrollmentService.MarkLessonProgress(callerId, request);

            if (!result.IsSuccess) return MapFailure(result);

            return Ok(result.Value);
        }

        [HttpGet("progress/{courseId}")]
        public async Task<ActionResult<PageResult<LessonProgressDto>>> GetCourseProgress(
            int courseId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            if (CallerId is not int callerId) return MissingIdentity();

            var result = await enrollmentService.GetCourseProgress(callerId, courseId, pageNumber, pageSize);

            if (!result.IsSuccess) return MapFailure(result);

            return Ok(result.Value);
        }

        // There is deliberately NO drop/unenroll endpoint — owner's decision: once
        // enrolled, you stay enrolled (legacy 'dropped' rows can still re-enroll).
    }
}
