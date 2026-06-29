using Business.Common;
using Business.Dto.Request;
using Business.Services;
using DataAccess.Data;
using DataAccess.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/Courses/{courseId}/reviews")]
    public class ReviewController(AppDbContext context) : ControllerBase
    {
        private int? CallerId =>
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int id) ? id : null;

        private string CallerRole =>
            User.FindFirstValue(ClaimTypes.Role) ?? "";

        [HttpPost]
        public async Task<ActionResult<ReviewDto>> AddReview(int courseId, [FromBody] AddReviewRequest request)
        {
            if (CallerId is not int callerId) return Unauthorized();

            var service = new ReviewService(context);
            var result = await service.AddReview(callerId, CallerRole, courseId, request);

            if (!result.IsSuccess)
                return result.FailureType switch
                {
                    ErrorType.BadRequest => BadRequest(result.Errors),
                    ErrorType.NotFound => NotFound(result.Errors),
                    ErrorType.Conflict => Conflict(result.Errors),
                    ErrorType.Unauthorized => Forbid(),
                    _ => StatusCode(500, "An unexpected error occurred")
                };

            return Ok(result.Value);
        }

        [HttpGet]
        public async Task<ActionResult<List<ReviewDto>>> GetReviews(int courseId)
        {
            var service = new ReviewService(context);
            var result = await service.GetCourseReviews(courseId);

            if (!result.IsSuccess)
                return result.FailureType switch
                {
                    ErrorType.BadRequest => BadRequest(result.Errors),
                    ErrorType.NotFound => NotFound(result.Errors),
                    _ => StatusCode(500, "An unexpected error occurred")
                };

            return Ok(result.Value);
        }

        [HttpPut]
        public async Task<ActionResult<ReviewDto>> UpdateReview(int courseId, [FromBody] UpdateReviewRequest request)
        {
            if (CallerId is not int callerId) return Unauthorized();

            var service = new ReviewService(context);
            var result = await service.UpdateReview(callerId, courseId, request);

            if (!result.IsSuccess)
                return result.FailureType switch
                {
                    ErrorType.BadRequest => BadRequest(result.Errors),
                    ErrorType.NotFound => NotFound(result.Errors),
                    _ => StatusCode(500, "An unexpected error occurred")
                };

            return Ok(result.Value);
        }

        [HttpDelete("{reviewId}")]
        public async Task<ActionResult<bool>> DeleteReview(int courseId, int reviewId)
        {
            if (CallerId is not int callerId) return Unauthorized();

            var service = new ReviewService(context);
            var result = await service.DeleteReview(callerId, CallerRole, courseId, reviewId);

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
    }
}
