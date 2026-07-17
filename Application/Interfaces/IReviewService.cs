using Business.Common;
using Business.Dto.Request;
using DataAccess.Dto;
using static DataAccess.Common.clsPageResult;

namespace Business.Interfaces
{
    public interface IReviewService
    {
        Task<MyResult<ReviewDto>> AddReview(int callerId, string callerRole, int courseId, AddReviewRequest request);
        Task<MyResult<PageResult<ReviewDto>>> GetCourseReviews(int courseId, int pageNumber, int pageSize);
        Task<MyResult<ReviewDto>> GetMyReview(int callerId, int courseId);
        Task<MyResult<ReviewDto>> UpdateReview(int callerId, int courseId, UpdateReviewRequest request);
        Task<MyResult<bool>> DeleteReview(int callerId, string callerRole, int courseId, int reviewId);
    }
}
