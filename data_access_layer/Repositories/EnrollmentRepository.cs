using DataAccess.Data;
using DataAccess.Dto;
using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using static DataAccess.Common.clsPageResult;

namespace DataAccess.Repositories
{
    public class EnrollmentRepository(AppDbContext context)
    {
        public async Task<bool> IsAlreadyEnrolledAsync(int userId, int courseId)
        {
            return await context.Enrollments
                .AnyAsync(e => e.user_id == userId && e.course_id == courseId);
        }

        // Owning instructor of a course (null if the course doesn't exist). Used to authorize
        // who may read a course's enrollment roster.
        public async Task<int?> GetCourseInstructorIdAsync(int courseId)
        {
            return await context.Courses
                .Where(c => c.course_id == courseId)
                .Select(c => (int?)c.instructor_id)
                .FirstOrDefaultAsync();
        }

        // Course fields needed to decide enrollment eligibility (null if the course doesn't exist).
        public async Task<CourseEnrollmentInfoDto?> GetCourseEnrollmentInfoAsync(int courseId)
        {
            return await context.Courses
                .Where(c => c.course_id == courseId)
                .Select(c => new CourseEnrollmentInfoDto
                {
                    InstructorId = c.instructor_id,
                    Status = c.status,
                    IsDeleted = c.deleted_at != null,
                    Price = c.price
                })
                .FirstOrDefaultAsync();
        }

        // True if the caller may view a course's curriculum/lesson content:
        // an admin, the owning instructor (even for a draft), or an active/completed
        // enrollment in a published, non-deleted course. A missing course is false.
        public async Task<bool> CanViewCourseContentAsync(int courseId, int callerId, bool isAdmin)
        {
            var info = await GetCourseEnrollmentInfoAsync(courseId);
            if (info == null) return false;

            if (isAdmin || info.InstructorId == callerId) return true;

            if (info.IsDeleted || info.Status != "published") return false;

            string? status = await GetEnrollmentStatusAsync(callerId, courseId);
            return status is "active" or "completed";
        }

        public async Task<int?> GetCourseIdByLessonAsync(int lessonId)
        {
            return await context.Lessons
                .Where(l => l.lesson_id == lessonId)
                .Select(l => (int?)l.section.course_id)
                .FirstOrDefaultAsync();
        }

        public async Task<string?> GetEnrollmentStatusAsync(int userId, int courseId)
        {
            return await context.Enrollments
                .Where(e => e.user_id == userId && e.course_id == courseId)
                .Select(e => e.status)
                .FirstOrDefaultAsync();
        }

        public async Task<bool> IsLessonAlreadyCompletedAsync(int userId, int lessonId)
        {
            return await context.UserLessonProgress
                .AnyAsync(p => p.user_id == userId && p.lesson_id == lessonId && p.is_completed);
        }

        public async Task<EnrollmentDto?> EnrollStudentAsync(EnrollmentEntitiy enrollment)
        {
            context.Enrollments.Add(enrollment);

            try
            {
                var rows = await context.SaveChangesAsync();
                if (rows <= 0) return null;

                string courseTitle = await context.Courses
                    .Where(c => c.course_id == enrollment.course_id)
                    .Select(c => c.title)
                    .FirstOrDefaultAsync() ?? "Unknown Course";

                return new EnrollmentDto
                {
                    EnrollmentId = enrollment.enrollment_id,
                    UserId = enrollment.user_id,
                    CourseId = enrollment.course_id,
                    CourseTitle = courseTitle,
                    EnrollmentDate = enrollment.enrollment_date,
                    CompletionDate = enrollment.completion_date,
                    Status = enrollment.status,
                    ProgressPercentage = enrollment.progress_percentage
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return null;
            }
        }

        public async Task<PageResult<EnrollmentDto>> GetEnrollmentsByUserIdAsync(int userId, int pageNumber, int pageSize)
        {
            try
            {
                var query = context.Enrollments
                    .Where(e => e.user_id == userId)
                    .AsNoTracking();

                var totalCount = await query.CountAsync();

                var items = await query
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(e => new EnrollmentDto
                    {
                        EnrollmentId = e.enrollment_id,
                        UserId = e.user_id,
                        CourseId = e.course_id,
                        CourseTitle = e.course.title,
                        EnrollmentDate = e.enrollment_date,
                        CompletionDate = e.completion_date,
                        Status = e.status,
                        ProgressPercentage = e.progress_percentage
                    })
                    .ToListAsync();

                return new PageResult<EnrollmentDto>
                {
                    Items = items,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return null;
            }
        }

        public async Task<PageResult<EnrollmentDto>> GetEnrollmentsByCourseIdAsync(int courseId, int pageNumber, int pageSize)
        {
            try
            {
                var query = context.Enrollments
                    .Where(e => e.course_id == courseId)
                    .AsNoTracking();

                var totalCount = await query.CountAsync();

                var items = await query
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(e => new EnrollmentDto
                    {
                        EnrollmentId = e.enrollment_id,
                        UserId = e.user_id,
                        CourseId = e.course_id,
                        CourseTitle = e.course.title,
                        EnrollmentDate = e.enrollment_date,
                        CompletionDate = e.completion_date,
                        Status = e.status,
                        ProgressPercentage = e.progress_percentage
                    })
                    .ToListAsync();

                return new PageResult<EnrollmentDto>
                {
                    Items = items,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return null;
            }
        }

        public async Task<EnrollmentDto?> MarkLessonProgressAsync(int userId, int lessonId, int courseId)
        {
            try
            {
                var progress = await context.UserLessonProgress
                    .FirstOrDefaultAsync(p => p.user_id == userId && p.lesson_id == lessonId);

                if (progress == null)
                {
                    context.UserLessonProgress.Add(new UserLessonProgressEntitiy
                    {
                        user_id = userId,
                        lesson_id = lessonId,
                        is_completed = true,
                        completed_at = DateTime.UtcNow
                    });
                }
                else
                {
                    progress.is_completed = true;
                    progress.completed_at = DateTime.UtcNow;
                }

                // DB trigger trg_sync_progress fires here and recalculates progress_percentage
                await context.SaveChangesAsync();

                // Fresh query to pick up the trigger-updated progress_percentage
                var enrollment = await context.Enrollments
                    .FirstOrDefaultAsync(e => e.user_id == userId && e.course_id == courseId);

                if (enrollment == null) return null;

                if (enrollment.progress_percentage >= 100)
                {
                    enrollment.status = "completed";
                    enrollment.completion_date = DateTime.UtcNow;
                    await context.SaveChangesAsync();
                }

                string courseTitle = await context.Courses
                    .Where(c => c.course_id == courseId)
                    .Select(c => c.title)
                    .FirstOrDefaultAsync() ?? "Unknown Course";

                return new EnrollmentDto
                {
                    EnrollmentId = enrollment.enrollment_id,
                    UserId = enrollment.user_id,
                    CourseId = enrollment.course_id,
                    CourseTitle = courseTitle,
                    EnrollmentDate = enrollment.enrollment_date,
                    CompletionDate = enrollment.completion_date,
                    Status = enrollment.status,
                    ProgressPercentage = enrollment.progress_percentage
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return null;
            }
        }

        public async Task<bool> DropEnrollmentAsync(int userId, int courseId)
        {
            try
            {
                var enrollment = await context.Enrollments
                    .FirstOrDefaultAsync(e => e.user_id == userId && e.course_id == courseId);

                if (enrollment == null) return false;

                enrollment.status = "dropped";
                await context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return false;
            }
        }

        public async Task<PageResult<LessonProgressDto>> GetUserCourseProgressAsync(int userId, int courseId, int pageNumber, int pageSize)
        {
            try
            {
                var query = context.Lessons
                    .Where(l => l.section.course_id == courseId && l.section.course.deleted_at == null)
                    .AsNoTracking();

                var totalCount = await query.CountAsync();

                var items = await query
                    .OrderBy(l => l.section.sort_order)
                    .ThenBy(l => l.sort_order)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(l => new LessonProgressDto
                    {
                        LessonId = l.lesson_id,
                        LessonTitle = l.title,
                        IsCompleted = context.UserLessonProgress
                            .Any(p => p.user_id == userId && p.lesson_id == l.lesson_id && p.is_completed),
                        CompletedAt = context.UserLessonProgress
                            .Where(p => p.user_id == userId && p.lesson_id == l.lesson_id && p.is_completed)
                            .Select(p => (DateTime?)p.completed_at)
                            .FirstOrDefault()
                    })
                    .ToListAsync();

                return new PageResult<LessonProgressDto>
                {
                    Items = items,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return null;
            }
        }

        // Reactivates a previously-dropped enrollment instead of inserting a new row,
        // which would violate the UNIQUE(user_id, course_id) constraint.
        public async Task<EnrollmentDto?> ReactivateDroppedEnrollmentAsync(int userId, int courseId)
        {
            try
            {
                var enrollment = await context.Enrollments
                    .FirstOrDefaultAsync(e => e.user_id == userId && e.course_id == courseId && e.status == "dropped");

                if (enrollment == null) return null;

                enrollment.status = "active";
                enrollment.enrollment_date = DateTime.UtcNow;
                enrollment.progress_percentage = 0;
                enrollment.completion_date = null;
                await context.SaveChangesAsync();

                string courseTitle = await context.Courses
                    .Where(c => c.course_id == courseId)
                    .Select(c => c.title)
                    .FirstOrDefaultAsync() ?? "Unknown Course";

                return new EnrollmentDto
                {
                    EnrollmentId = enrollment.enrollment_id,
                    UserId = enrollment.user_id,
                    CourseId = enrollment.course_id,
                    CourseTitle = courseTitle,
                    EnrollmentDate = enrollment.enrollment_date,
                    CompletionDate = enrollment.completion_date,
                    Status = enrollment.status,
                    ProgressPercentage = enrollment.progress_percentage
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return null;
            }
        }
    }
}
