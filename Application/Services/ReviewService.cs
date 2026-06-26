using Business.Common;
using Business.Dto.Request;
using DataAccess.Data;
using DataAccess.Dto;
using DataAccess.Entities;
using DataAccess.Repositories;

namespace Business.Services
{
    public class ReviewService(AppDbContext context)
    {
        // Only enrolled students can add a review (not the course instructor).
        // Admins bypass the enrollment check.
        public async Task<MyResult<ReviewDto>> AddReview(int callerId, string callerRole, int courseId, AddReviewRequest request)
        {
            if (courseId <= 0)
                return MyResult<ReviewDto>.Failure(ErrorType.BadRequest, "Invalid course ID.");

            if (request.Rating < 1 || request.Rating > 5)
                return MyResult<ReviewDto>.Failure(ErrorType.BadRequest, "Rating must be between 1 and 5.");

            var repo = new ReviewRepository(context);

            int? instructorId = await repo.GetCourseInstructorIdAsync(courseId);
            if (instructorId == null)
                return MyResult<ReviewDto>.Failure(ErrorType.NotFound, "Course not found.");

            if (callerId == instructorId)
                return MyResult<ReviewDto>.Failure(ErrorType.BadRequest, "Instructors cannot review their own course.");

            bool isAdmin = callerRole == "admin";

            if (!isAdmin)
            {
                bool enrolled = await repo.IsEnrolledAsync(callerId, courseId);
                if (!enrolled)
                    return MyResult<ReviewDto>.Failure(ErrorType.Unauthorized, "You must be enrolled in this course to leave a review.");
            }

            bool alreadyReviewed = await repo.HasAlreadyReviewedAsync(callerId, courseId);
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

            var result = await repo.AddReviewAsync(entity);
            if (result == null)
                return MyResult<ReviewDto>.Failure(ErrorType.Failure, "Failed to save review.");

            return MyResult<ReviewDto>.Success(result);
        }

        // Accessible to: enrolled students, the course instructor, admins.
        public async Task<MyResult<List<ReviewDto>>> GetCourseReviews(int callerId, string callerRole, int courseId)
        {
            if (courseId <= 0)
                return MyResult<List<ReviewDto>>.Failure(ErrorType.BadRequest, "Invalid course ID.");

            var repo = new ReviewRepository(context);

            int? instructorId = await repo.GetCourseInstructorIdAsync(courseId);
            if (instructorId == null)
                return MyResult<List<ReviewDto>>.Failure(ErrorType.NotFound, "Course not found.");

            bool isAdmin = callerRole == "admin";

            if (!isAdmin)
            {
                bool isInstructor = callerId == instructorId;
                bool enrolled = !isInstructor && await repo.IsEnrolledAsync(callerId, courseId);

                if (!isInstructor && !enrolled)
                    return MyResult<List<ReviewDto>>.Failure(ErrorType.Unauthorized, "Access denied.");
            }

            var reviews = await repo.GetReviewsByCourseIdAsync(courseId);
            if (reviews == null)
                return MyResult<List<ReviewDto>>.Failure(ErrorType.Failure, "Failed to retrieve reviews.");

            return MyResult<List<ReviewDto>>.Success(reviews);
        }

        // Only the review owner can update their own review.
        public async Task<MyResult<ReviewDto>> UpdateReview(int callerId, int courseId, UpdateReviewRequest request)
        {
            if (courseId <= 0)
                return MyResult<ReviewDto>.Failure(ErrorType.BadRequest, "Invalid course ID.");

            if (request.Rating < 1 || request.Rating > 5)
                return MyResult<ReviewDto>.Failure(ErrorType.BadRequest, "Rating must be between 1 and 5.");

            var repo = new ReviewRepository(context);

            bool hasReview = await repo.HasAlreadyReviewedAsync(callerId, courseId);
            if (!hasReview)
                return MyResult<ReviewDto>.Failure(ErrorType.NotFound, "You have no review on this course.");

            var result = await repo.UpdateReviewAsync(callerId, courseId, request.Rating, request.Comment);
            if (result == null)
                return MyResult<ReviewDto>.Failure(ErrorType.Failure, "Failed to update review.");

            return MyResult<ReviewDto>.Success(result);
        }

        // Review owner, the course instructor, or an admin can delete a review.
        public async Task<MyResult<bool>> DeleteReview(int callerId, string callerRole, int courseId, int reviewId)
        {
            if (reviewId <= 0)
                return MyResult<bool>.Failure(ErrorType.BadRequest, "Invalid review ID.");

            var repo = new ReviewRepository(context);

            var review = await repo.GetReviewByIdAsync(reviewId);
            if (review == null || review.course_id != courseId)
                return MyResult<bool>.Failure(ErrorType.NotFound, "Review not found.");

            bool isAdmin = callerRole == "admin";

            if (!isAdmin)
            {
                int? instructorId = await repo.GetCourseInstructorIdAsync(courseId);
                bool isInstructor = callerId == instructorId;
                bool isOwner = review.user_id == callerId;

                if (!isInstructor && !isOwner)
                    return MyResult<bool>.Failure(ErrorType.Unauthorized, "Access denied.");
            }

            bool deleted = await repo.DeleteReviewAsync(reviewId);
            if (!deleted)
                return MyResult<bool>.Failure(ErrorType.Failure, "Failed to delete review.");

            return MyResult<bool>.Success(true);
        }
    }
}
