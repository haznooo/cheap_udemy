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
    public class CourseController(AppDbContext context) : ControllerBase
    {

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
        [HttpPost("section/add")]
        public async Task<ActionResult<SectionEntitiy>> AddSection(AddSectionRequest request)
        {

            var courseServic = new CourseService(context);
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
