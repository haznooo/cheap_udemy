using Business.Common;
using Business.Dto.Request;
using DataAccess.Dto;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DataAccess.Data;
using Business.Services;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/Lessons")]
    public class LessonsController(LessonService lessonService) : ControllerBase
    {
        [HttpPost("add")]
        public async Task<ActionResult<LessonDto>> CreateLesson([FromBody] LessonRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return BadRequest("Lesson title is required.");
            }

            var result = await lessonService.CreateLessonAsync(request);

            if (!result.IsSuccess)
            {
                return result.FailureType switch
                {
                    ErrorType.BadRequest => BadRequest(result.Errors),
                    _ => StatusCode(500, "An unexpected error occurred")
                };
            }

            return CreatedAtAction(nameof(GetLesson), new { id = result.Value.LessonId }, result.Value);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<LessonDto>> GetLesson(int id)
        {
            var result = await lessonService.GetLessonAsync(id);

            if (!result.IsSuccess)
            {
                return result.FailureType switch
                {
                    ErrorType.NotFound => NotFound(result.Errors),
                    _ => StatusCode(500, "An unexpected error occurred")
                };
            }

            return Ok(result.Value);
        }
    }

}
