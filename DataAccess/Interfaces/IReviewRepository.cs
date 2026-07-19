using DataAccess.Dto;
using DataAccess.Entities;
using static DataAccess.Common.clsPageResult;

namespace DataAccess.Interfaces
{
    public interface IReviewRepository
    {
        Task<bool> IsEnrolledAsync(int userId, int courseId);
        Task<bool> HasAlreadyReviewedAsync(int userId, int courseId);
        Task<ReviewDto?> AddReviewAsync(ReviewEntitiy review);
        Task<PageResult<ReviewDto>> GetReviewsByCourseIdAsync(int courseId, int pageNumber, int pageSize);
        Task<ReviewDto?> GetReviewByUserAndCourseAsync(int userId, int courseId);
        Task<ReviewDto?> UpdateReviewAsync(int userId, int courseId, short rating, string? comment);
        Task<ReviewEntitiy?> GetReviewByIdAsync(int reviewId);
        Task<bool> DeleteReviewAsync(int reviewId);
    }
}
