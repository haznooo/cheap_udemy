using Business.Dto.Request;
using Business.Services;
using DataAccess.Data;
using DataAccess.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/Courses/{courseId}/reviews")]
    public class ReviewController(AppDbContext context) : ApiControllerBase
    {
        [HttpPost]
        public async Task<ActionResult<ReviewDto>> AddReview(int courseId, [FromBody] AddReviewRequest request)
        {
            if (CallerId is not int callerId) return MissingIdentity();

            var service = new ReviewService(context);
            var result = await service.AddReview(callerId, CallerRole, courseId, request);

            if (!result.IsSuccess) return MapFailure(result);

            return Ok(result.Value);
        }

        [HttpGet]
        public async Task<ActionResult<List<ReviewDto>>> GetReviews(int courseId)
        {
            var service = new ReviewService(context);
            var result = await service.GetCourseReviews(courseId);

            if (!result.IsSuccess) return MapFailure(result);

            return Ok(result.Value);
        }

        [HttpPut]
        public async Task<ActionResult<ReviewDto>> UpdateReview(int courseId, [FromBody] UpdateReviewRequest request)
        {
            if (CallerId is not int callerId) return MissingIdentity();

            var service = new ReviewService(context);
            var result = await service.UpdateReview(callerId, courseId, request);

            if (!result.IsSuccess) return MapFailure(result);

            return Ok(result.Value);
        }

        [HttpDelete("{reviewId}")]
        public async Task<ActionResult<bool>> DeleteReview(int courseId, int reviewId)
        {
            if (CallerId is not int callerId) return MissingIdentity();

            var service = new ReviewService(context);
            var result = await service.DeleteReview(callerId, CallerRole, courseId, reviewId);

            if (!result.IsSuccess) return MapFailure(result);

            return Ok(result.Value);
        }
    }
}
