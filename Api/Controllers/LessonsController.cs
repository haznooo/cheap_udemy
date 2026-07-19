using Business.Dto.Request;
using Business.Interfaces;
using DataAccess.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/Lessons")]
    [Authorize]
    public class LessonsController(ILessonService lessonService) : ApiControllerBase
    {
        [HttpPost("add")]
        public async Task<ActionResult<LessonDto>> CreateLesson([FromBody] LessonRequest request)
        {
            // Caller identity comes from the JWT; lesson ownership is resolved
            // through the section's course in the service layer.
            if (CallerId is not int callerId) return MissingIdentity();

            bool isAdmin = User.IsInRole("admin");
            var result = await lessonService.CreateLessonAsync(request, callerId, isAdmin);

            if (!result.IsSuccess) return MapFailure(result);

            return CreatedAtAction(nameof(GetLesson), new { id = result.Value.LessonId }, result.Value);
        }

        [HttpPut("update/{id}")]
        public async Task<ActionResult<LessonDto>> UpdateLesson(int id, [FromBody] UpdateLessonRequest request)
        {
            if (CallerId is not int callerId) return MissingIdentity();

            bool isAdmin = User.IsInRole("admin");
            var result = await lessonService.UpdateLessonAsync(id, request, callerId, isAdmin);

            if (!result.IsSuccess) return MapFailure(result);

            return Ok(result.Value);
        }

        // Hard delete. Caller identity comes from the JWT; ownership is resolved
        // lesson → section → course → instructor (or admin) in the service layer.
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteLesson(int id)
        {
            if (CallerId is not int callerId) return MissingIdentity();

            bool isAdmin = User.IsInRole("admin");
            var result = await lessonService.DeleteLessonAsync(id, callerId, isAdmin);

            if (!result.IsSuccess) return MapFailure(result);

            // 204, matching DeleteSection/DeleteCourse.
            return NoContent();
        }

        // Publish a lesson (draft or hidden -> published), making it visible to
        // enrolled students. Owner/admin only, resolved lesson -> section -> course.
        [HttpPost("{id}/publish")]
        public async Task<ActionResult<LessonDto>> PublishLesson(int id)
        {
            if (CallerId is not int callerId) return MissingIdentity();

            bool isAdmin = User.IsInRole("admin");
            var result = await lessonService.PublishLessonAsync(id, callerId, isAdmin);

            if (!result.IsSuccess) return MapFailure(result);

            return Ok(result.Value);
        }

        // Unpublish a lesson (published -> hidden): invisible to everyone but the
        // owner/admin, including already-enrolled students.
        [HttpPost("{id}/unpublish")]
        public async Task<ActionResult<LessonDto>> UnpublishLesson(int id)
        {
            if (CallerId is not int callerId) return MissingIdentity();

            bool isAdmin = User.IsInRole("admin");
            var result = await lessonService.UnpublishLessonAsync(id, callerId, isAdmin);

            if (!result.IsSuccess) return MapFailure(result);

            return Ok(result.Value);
        }

        [HttpGet("get/{id}")]
        public async Task<ActionResult<LessonDto>> GetLesson(int id)
        {
            if (CallerId is not int callerId) return MissingIdentity();

            bool isAdmin = User.IsInRole("admin");
            var result = await lessonService.GetLessonAsync(id, callerId, isAdmin);

            if (!result.IsSuccess) return MapFailure(result);

            return Ok(result.Value);
        }
    }

}
