using DataAccess.Data;
using DataAccess.Dto;
using DataAccess.Entities;
using DataAccess.Interfaces;
using Microsoft.EntityFrameworkCore;
using static DataAccess.Common.clsPageResult;

namespace DataAccess.Repositories
{
    public class ReviewRepository(AppDbContext context) : IReviewRepository
    {
        public async Task<bool> IsEnrolledAsync(int userId, int courseId)
            => await context.Enrollments.AnyAsync(e => e.user_id == userId && e.course_id == courseId);

        public async Task<bool> HasAlreadyReviewedAsync(int userId, int courseId)
            => await context.Reviews.AnyAsync(r => r.user_id == userId && r.course_id == courseId);

        public async Task<ReviewDto?> AddReviewAsync(ReviewEntitiy review)
        {
            try
            {
                await context.Reviews.AddAsync(review);
                await context.SaveChangesAsync();

                string reviewerName = await context.Users
                    .Where(u => u.user_id == review.user_id)
                    .Select(u => u.username)
                    .FirstOrDefaultAsync() ?? "Unknown";

                return MapToDto(review, reviewerName);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return null;
            }
        }


        public async Task<PageResult<ReviewDto>> GetReviewsByCourseIdAsync(int courseId, int pageNumber, int pageSize)
        {
            try
            {
                var query = context.Reviews
                    .Where(r => r.course_id == courseId)
                    .AsNoTracking();

                var totalCount = await query.CountAsync();

                var items = await query
                    .OrderByDescending(r => r.created_at)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(r => new ReviewDto
                    {
                        ReviewId = r.review_id,
                        CourseId = r.course_id,
                        UserId = r.user_id,
                        ReviewerName = r.user.username,
                        Rating = r.rating,
                        Comment = r.comment,
                        CreatedAt = r.created_at,
                        UpdatedAt = r.updated_at
                    })
                    .ToListAsync();

                return new PageResult<ReviewDto>
                {
                    Items = items,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return null;
            }
        }

        // The caller's own review of a course (null if they never reviewed it). One review
        // per user per course (uq_user_course_review), so this is a unique lookup.
        public async Task<ReviewDto?> GetReviewByUserAndCourseAsync(int userId, int courseId)
        {
            try
            {
                return await context.Reviews
                    .Where(r => r.user_id == userId && r.course_id == courseId)
                    .AsNoTracking()
                    .Select(r => new ReviewDto
                    {
                        ReviewId = r.review_id,
                        CourseId = r.course_id,
                        UserId = r.user_id,
                        ReviewerName = r.user.username,
                        Rating = r.rating,
                        Comment = r.comment,
                        CreatedAt = r.created_at,
                        UpdatedAt = r.updated_at
                    })
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return null;
            }
        }

        public async Task<ReviewDto?> UpdateReviewAsync(int userId, int courseId, short rating, string? comment)
        {
            try
            {
                var review = await context.Reviews
                    .FirstOrDefaultAsync(r => r.user_id == userId && r.course_id == courseId);

                if (review == null) return null;

                review.rating = rating;
                review.comment = comment;
                review.updated_at = DateTime.UtcNow;
                await context.SaveChangesAsync();

                string reviewerName = await context.Users
                    .Where(u => u.user_id == userId)
                    .Select(u => u.username)
                    .FirstOrDefaultAsync() ?? "Unknown";

                return MapToDto(review, reviewerName);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return null;
            }
        }

        public async Task<ReviewEntitiy?> GetReviewByIdAsync(int reviewId)
            => await context.Reviews.AsNoTracking().FirstOrDefaultAsync(r => r.review_id == reviewId);

        public async Task<bool> DeleteReviewAsync(int reviewId)
        {
            try
            {
                var review = await context.Reviews.FirstOrDefaultAsync(r => r.review_id == reviewId);
                if (review == null) return false;

                context.Reviews.Remove(review);
                await context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return false;
            }
        }

        private static ReviewDto MapToDto(ReviewEntitiy r, string reviewerName) => new ReviewDto
        {
            ReviewId = r.review_id,
            CourseId = r.course_id,
            UserId = r.user_id,
            ReviewerName = reviewerName,
            Rating = r.rating,
            Comment = r.comment,
            CreatedAt = r.created_at,
            UpdatedAt = r.updated_at
        };
    }
}
