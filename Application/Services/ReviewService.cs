using Business.Common;
using Business.Dto.Request;
using Business.Interfaces;
using DataAccess.Dto;
using DataAccess.Entities;
using DataAccess.Interfaces;
using static DataAccess.Common.clsPageResult;

namespace Business.Services
{
    public class ReviewService(IReviewRepository reviewRepository, IEnrollmentRepository enrollmentRepository) : IReviewService
    {
        // Only enrolled students can add a review (not the course instructor).
        // Admins bypass the enrollment check.
        public async Task<MyResult<ReviewDto>> AddReview(int callerId, string callerRole, int courseId, AddReviewRequest request)
        {
            if (courseId <= 0)
                return MyResult<ReviewDto>.Failure(ErrorType.BadRequest, "Invalid course ID.");

            if (request.Rating < 1 || request.Rating > 5)
                return MyResult<ReviewDto>.Failure(ErrorType.BadRequest, "Rating must be between 1 and 5.");

            // Course must exist and be published/non-deleted to be reviewable.
            var course = await enrollmentRepository.GetCourseEnrollmentInfoAsync(courseId);
            if (course == null || course.IsDeleted || course.Status != "published")
                return MyResult<ReviewDto>.Failure(ErrorType.NotFound, "Course not found.");

            if (callerId == course.InstructorId)
                return MyResult<ReviewDto>.Failure(ErrorType.BadRequest, "Instructors cannot review their own course.");

            bool isAdmin = callerRole == "admin";

            if (!isAdmin)
            {
                // Must actually be (or have been) a student of the course — an active or
                // completed enrollment. A dropped/suspended enrollment can't review.
                string? status = await enrollmentRepository.GetEnrollmentStatusAsync(callerId, courseId);
                if (status is not ("active" or "completed"))
                    return MyResult<ReviewDto>.Failure(ErrorType.Unauthorized, "You must be enrolled in this course to leave a review.");
            }

            bool alreadyReviewed = await reviewRepository.HasAlreadyReviewedAsync(callerId, courseId);
            if (alreadyReviewed)
                return MyResult<ReviewDto>.Failure(ErrorType.Conflict, "You have already reviewed this course.");

            var entity = new ReviewEntitiy
            {
                course_id = courseId,
                user_id = callerId,
                rating = request.Rating,
                comment = request.Comment,
                created_at = DateTime.UtcNow
            };

            var result = await reviewRepository.AddReviewAsync(entity);
            if (result == null)
                return MyResult<ReviewDto>.Failure(ErrorType.Failure, "Failed to save review.");

            return MyResult<ReviewDto>.Success(result);
        }

        // Any logged-in user can read a course's reviews (controller requires authentication).
        public async Task<MyResult<PageResult<ReviewDto>>> GetCourseReviews(int courseId, int pageNumber, int pageSize)
        {
            if (courseId <= 0)
                return MyResult<PageResult<ReviewDto>>.Failure(ErrorType.BadRequest, "Invalid course ID.");

            if (pageNumber <= 0 || pageSize <= 0)
                return MyResult<PageResult<ReviewDto>>.Failure(ErrorType.BadRequest, "Invalid page number or page size.");

            int? instructorId = await reviewRepository.GetCourseInstructorIdAsync(courseId);
            if (instructorId == null)
                return MyResult<PageResult<ReviewDto>>.Failure(ErrorType.NotFound, "Course not found.");

            var reviews = await reviewRepository.GetReviewsByCourseIdAsync(courseId, pageNumber, pageSize);
            if (reviews == null)
                return MyResult<PageResult<ReviewDto>>.Failure(ErrorType.Failure, "Failed to retrieve reviews.");

            return MyResult<PageResult<ReviewDto>>.Success(reviews);
        }

        // Only the review author can update their own review.
        public async Task<MyResult<ReviewDto>> UpdateReview(int callerId, int courseId, UpdateReviewRequest request)
        {
            if (courseId <= 0)
                return MyResult<ReviewDto>.Failure(ErrorType.BadRequest, "Invalid course ID.");

            if (request.Rating < 1 || request.Rating > 5)
                return MyResult<ReviewDto>.Failure(ErrorType.BadRequest, "Rating must be between 1 and 5.");

            bool hasReview = await reviewRepository.HasAlreadyReviewedAsync(callerId, courseId);
            if (!hasReview)
                return MyResult<ReviewDto>.Failure(ErrorType.NotFound, "You have no review on this course.");

            var result = await reviewRepository.UpdateReviewAsync(callerId, courseId, request.Rating, request.Comment);
            if (result == null)
                return MyResult<ReviewDto>.Failure(ErrorType.Failure, "Failed to update review.");

            return MyResult<ReviewDto>.Success(result);
        }

        // Only the review's author or an admin can delete a review. Instructors deliberately
        // CANNOT delete reviews on their own course (no censoring of unfavourable reviews).
        public async Task<MyResult<bool>> DeleteReview(int callerId, string callerRole, int courseId, int reviewId)
        {
            if (reviewId <= 0)
                return MyResult<bool>.Failure(ErrorType.BadRequest, "Invalid review ID.");

            var review = await reviewRepository.GetReviewByIdAsync(reviewId);
            if (review == null || review.course_id != courseId)
                return MyResult<bool>.Failure(ErrorType.NotFound, "Review not found.");

            bool isAdmin = callerRole == "admin";
            bool isOwner = review.user_id == callerId;

            if (!isAdmin && !isOwner)
                return MyResult<bool>.Failure(ErrorType.Unauthorized, "Access denied.");

            bool deleted = await reviewRepository.DeleteReviewAsync(reviewId);
            if (!deleted)
                return MyResult<bool>.Failure(ErrorType.Failure, "Failed to delete review.");

            return MyResult<bool>.Success(true);
        }
    }
}
