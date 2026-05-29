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
        public async Task<ActionResult<LessonDto>> CreateLesson([FromBody] LessonDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return BadRequest("Lesson title is required.");
            }

            var createdLesson = await lessonService.CreateLessonAsync(request);

            return CreatedAtAction(nameof(GetLesson), new { id = createdLesson.LessonId }, createdLesson);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<LessonDto>> GetLesson(int id)
        {
            var lesson = await lessonService.GetLessonAsync(id);

            if (lesson == null)
            {
                return NotFound($"Lesson with ID {id} not found.");
            }

            return Ok(lesson);
        }
    }

}
