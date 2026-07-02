using Business.Dto.Request;
using Business.Services;
using DataAccess.Data;
using DataAccess.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static Business.Common.clsPageResult;

namespace Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/Courses/{courseId}/reviews")]
    public class ReviewController(AppDbContext context) : ApiControllerBase
    {

        [HttpPost("add")]
        public async Task<ActionResult<ReviewDto>> AddReview(int courseId, [FromBody] AddReviewRequest request)
        {
            if (CallerId is not int callerId) return MissingIdentity();

            var service = new ReviewService(context);
            var result = await service.AddReview(callerId, CallerRole, courseId, request);

            if (!result.IsSuccess) return MapFailure(result);

            return Ok(result.Value);
        }

        [HttpGet("get")]
        public async Task<ActionResult<PageResult<ReviewDto>>> GetReviews(
            int courseId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            var service = new ReviewService(context);
            var result = await service.GetCourseReviews(courseId, pageNumber, pageSize);

            if (!result.IsSuccess) return MapFailure(result);

            return Ok(result.Value);
        }

        [HttpPut("update/{id}")]
        public async Task<ActionResult<ReviewDto>> UpdateReview(int courseId, [FromBody] UpdateReviewRequest request)
        {
            if (CallerId is not int callerId) return MissingIdentity();

            var service = new ReviewService(context);
            var result = await service.UpdateReview(callerId, courseId, request);

            if (!result.IsSuccess) return MapFailure(result);

            return Ok(result.Value);
        }

        [HttpDelete("delete/{reviewId}")]
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
