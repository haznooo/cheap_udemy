using Business.Dto.Request;
using DataAccess.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DataAccess.Data;
using Business.Services;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/Lessons")]
    [Authorize]
    public class LessonsController(LessonService lessonService) : ApiControllerBase
    {
        [HttpPost("add")]
        public async Task<ActionResult<LessonDto>> CreateLesson([FromBody] LessonRequest request)
        {
            // Caller identity comes from the JWT; lesson ownership is resolved
            // through the section's course in the service layer.
            if (CallerId is not int callerId) return MissingIdentity();

            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return Problem(statusCode: StatusCodes.Status400BadRequest, detail: "Lesson title is required.");
            }

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
