using Business.Common;
using Business.Dto.Request;
using DataAccess.Data;
using DataAccess.Dto;
using DataAccess.Entities;
using DataAccess.Repositories;
using static Business.Common.clsPageResult;

namespace Business.Services
{
    public class EnrollmentService(AppDbContext context)
    {
        // callerId is the authenticated user (from the JWT) — a user can only enroll themselves.
        public async Task<MyResult<EnrollmentDto>> EnrollStudent(int callerId, EnrollRequest request)
        {
            if (callerId <= 0)
                return MyResult<EnrollmentDto>.Failure(ErrorType.BadRequest, "Invalid user ID.");

            if (request.CourseId <= 0)
                return MyResult<EnrollmentDto>.Failure(ErrorType.BadRequest, "Invalid course ID.");

            var repo = new EnrollmentRepository(context);

            string? existingStatus = await repo.GetEnrollmentStatusAsync(callerId, request.CourseId);

            if (existingStatus != null && existingStatus != "dropped")
                return MyResult<EnrollmentDto>.Failure(ErrorType.Conflict, "User is already enrolled in this course.");

            // The enrollments table has UNIQUE(user_id, course_id), so a dropped row must
            // be reactivated rather than a new row inserted.
            if (existingStatus == "dropped")
            {
                var reactivated = await repo.ReactivateDroppedEnrollmentAsync(callerId, request.CourseId);
                if (reactivated == null)
                    return MyResult<EnrollmentDto>.Failure(ErrorType.Failure, "Failed to re-enroll student.");
                return MyResult<EnrollmentDto>.Success(reactivated);
            }

            var enrollment = new EnrollmentEntitiy
            {
                user_id = callerId,
                course_id = request.CourseId,
                enrollment_date = DateTime.UtcNow,
                status = "active",
                progress_percentage = 0
            };

            var result = await repo.EnrollStudentAsync(enrollment);
            if (result == null)
                return MyResult<EnrollmentDto>.Failure(ErrorType.Failure, "Failed to enroll student.");

            return MyResult<EnrollmentDto>.Success(result);
        }

        // A user may only read their own enrollments; admins may read anyone's.
        public async Task<MyResult<PageResult<EnrollmentDto>>> GetUserEnrollments(int callerId, string callerRole, int userId, int pageNumber, int pageSize)
        {
            if (userId <= 0)
                return MyResult<PageResult<EnrollmentDto>>.Failure(ErrorType.BadRequest, "Invalid user ID.");

            if (callerRole != "admin" && callerId != userId)
                return MyResult<PageResult<EnrollmentDto>>.Failure(ErrorType.Unauthorized, "Access denied.");

            if (pageNumber <= 0 || pageSize <= 0)
                return MyResult<PageResult<EnrollmentDto>>.Failure(ErrorType.BadRequest, "Invalid page number or page size.");

            var repo = new EnrollmentRepository(context);
            var r = await repo.GetEnrollmentsByUserIdAsync(userId, pageNumber, pageSize);

            if (r == null)
                return MyResult<PageResult<EnrollmentDto>>.Failure(ErrorType.Failure, "Failed to retrieve enrollments.");

            return MyResult<PageResult<EnrollmentDto>>.Success(new PageResult<EnrollmentDto>
            {
                Items = r.Items,
                TotalCount = r.TotalCount,
                PageNumber = r.PageNumber,
                PageSize = r.PageSize
            });
        }

        // The course roster is readable only by the owning instructor or an admin.
        public async Task<MyResult<PageResult<EnrollmentDto>>> GetCourseEnrollments(int callerId, string callerRole, int courseId, int pageNumber, int pageSize)
        {
            if (courseId <= 0)
                return MyResult<PageResult<EnrollmentDto>>.Failure(ErrorType.BadRequest, "Invalid course ID.");

            if (pageNumber <= 0 || pageSize <= 0)
                return MyResult<PageResult<EnrollmentDto>>.Failure(ErrorType.BadRequest, "Invalid page number or page size.");

            var repo = new EnrollmentRepository(context);

            int? instructorId = await repo.GetCourseInstructorIdAsync(courseId);
            if (instructorId == null)
                return MyResult<PageResult<EnrollmentDto>>.Failure(ErrorType.NotFound, "Course not found.");

            if (callerRole != "admin" && callerId != instructorId)
                return MyResult<PageResult<EnrollmentDto>>.Failure(ErrorType.Unauthorized, "Access denied.");

            var r = await repo.GetEnrollmentsByCourseIdAsync(courseId, pageNumber, pageSize);

            if (r == null)
                return MyResult<PageResult<EnrollmentDto>>.Failure(ErrorType.Failure, "Failed to retrieve enrollments.");

            return MyResult<PageResult<EnrollmentDto>>.Success(new PageResult<EnrollmentDto>
            {
                Items = r.Items,
                TotalCount = r.TotalCount,
                PageNumber = r.PageNumber,
                PageSize = r.PageSize
            });
        }

        // callerId (from the JWT) is the only user whose progress can be marked.
        public async Task<MyResult<EnrollmentDto>> MarkLessonProgress(int callerId, MarkLessonProgressRequest request)
        {
            if (callerId <= 0)
                return MyResult<EnrollmentDto>.Failure(ErrorType.BadRequest, "Invalid user ID.");

            if (request.LessonId <= 0)
                return MyResult<EnrollmentDto>.Failure(ErrorType.BadRequest, "Invalid lesson ID.");

            var repo = new EnrollmentRepository(context);

            int? courseId = await repo.GetCourseIdByLessonAsync(request.LessonId);
            if (courseId == null)
                return MyResult<EnrollmentDto>.Failure(ErrorType.NotFound, "Lesson not found.");

            string? enrollmentStatus = await repo.GetEnrollmentStatusAsync(callerId, courseId.Value);
            if (enrollmentStatus == null)
                return MyResult<EnrollmentDto>.Failure(ErrorType.BadRequest, "User is not enrolled in this course.");

            if (enrollmentStatus is "dropped" or "suspended")
                return MyResult<EnrollmentDto>.Failure(ErrorType.BadRequest, "Enrollment is not active.");

            if (enrollmentStatus == "completed")
                return MyResult<EnrollmentDto>.Failure(ErrorType.Conflict, "Course is already completed.");

            bool lessonDone = await repo.IsLessonAlreadyCompletedAsync(callerId, request.LessonId);
            if (lessonDone)
                return MyResult<EnrollmentDto>.Failure(ErrorType.Conflict, "Lesson is already completed.");

            var result = await repo.MarkLessonProgressAsync(callerId, request.LessonId, courseId.Value);
            if (result == null)
                return MyResult<EnrollmentDto>.Failure(ErrorType.Failure, "Failed to mark lesson progress.");

            return MyResult<EnrollmentDto>.Success(result);
        }

        public async Task<MyResult<List<LessonProgressDto>>> GetCourseProgress(int callerId, int courseId)
        {
            if (courseId <= 0)
                return MyResult<List<LessonProgressDto>>.Failure(ErrorType.BadRequest, "Invalid course ID.");

            var repo = new EnrollmentRepository(context);

            string? enrollmentStatus = await repo.GetEnrollmentStatusAsync(callerId, courseId);
            if (enrollmentStatus == null)
                return MyResult<List<LessonProgressDto>>.Failure(ErrorType.NotFound, "You are not enrolled in this course.");

            if (enrollmentStatus is "dropped" or "suspended")
                return MyResult<List<LessonProgressDto>>.Failure(ErrorType.Unauthorized, "Enrollment is not active.");

            var progress = await repo.GetUserCourseProgressAsync(callerId, courseId);
            if (progress == null)
                return MyResult<List<LessonProgressDto>>.Failure(ErrorType.Failure, "Failed to retrieve progress.");

            return MyResult<List<LessonProgressDto>>.Success(progress);
        }

        // callerId (from the JWT) can only drop their own enrollment.
        public async Task<MyResult<bool>> DropEnrollment(int callerId, DropEnrollmentRequest request)
        {
            if (callerId <= 0)
                return MyResult<bool>.Failure(ErrorType.BadRequest, "Invalid user ID.");

            if (request.CourseId <= 0)
                return MyResult<bool>.Failure(ErrorType.BadRequest, "Invalid course ID.");

            var repo = new EnrollmentRepository(context);

            bool alreadyEnrolled = await repo.IsAlreadyEnrolledAsync(callerId, request.CourseId);
            if (!alreadyEnrolled)
                return MyResult<bool>.Failure(ErrorType.NotFound, "Enrollment not found.");

            var result = await repo.DropEnrollmentAsync(callerId, request.CourseId);
            if (!result)
                return MyResult<bool>.Failure(ErrorType.Failure, "Failed to drop enrollment.");

            return MyResult<bool>.Success(true);
        }
    }
}
