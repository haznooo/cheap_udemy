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
    [Route("api/Courses/{courseId}/reviews")]
    public class ReviewController(IReviewService reviewService) : ApiControllerBase
    {

        [HttpPost("add")]
        public async Task<ActionResult<ReviewDto>> AddReview(int courseId, [FromBody] AddReviewRequest request)
        {
            if (CallerId is not int callerId) return MissingIdentity();

            var result = await reviewService.AddReview(callerId, CallerRole, courseId, request);

            if (!result.IsSuccess) return MapFailure(result);

            return Ok(result.Value);
        }

        [HttpGet("get")]
        public async Task<ActionResult<PageResult<ReviewDto>>> GetReviews(
            int courseId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            var result = await reviewService.GetCourseReviews(courseId, pageNumber, pageSize);

            if (!result.IsSuccess) return MapFailure(result);

            return Ok(result.Value);
        }

        // No id in the route on purpose: a user has at most one review per course
        // (uq_user_course_review), so the review is resolved by caller + courseId.
        [HttpPut("update")]
        public async Task<ActionResult<ReviewDto>> UpdateReview(int courseId, [FromBody] UpdateReviewRequest request)
        {
            if (CallerId is not int callerId) return MissingIdentity();

            var result = await reviewService.UpdateReview(callerId, courseId, request);

            if (!result.IsSuccess) return MapFailure(result);

            return Ok(result.Value);
        }

        [HttpDelete("delete/{reviewId}")]
        public async Task<ActionResult<bool>> DeleteReview(int courseId, int reviewId)
        {
            if (CallerId is not int callerId) return MissingIdentity();

            var result = await reviewService.DeleteReview(callerId, CallerRole, courseId, reviewId);

            if (!result.IsSuccess) return MapFailure(result);

            return Ok(result.Value);
        }
    }
}
