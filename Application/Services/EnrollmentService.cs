using Business.Common;
using Business.Dto.Request;
using Business.Interfaces;
using DataAccess.Dto;
using DataAccess.Entities;
using DataAccess.Interfaces;
using static DataAccess.Common.clsPageResult;

namespace Business.Services
{
    public class EnrollmentService(IEnrollmentRepository enrollmentRepository) : IEnrollmentService
    {
        // callerId is the authenticated user (from the JWT) — a user can only enroll themselves.
        public async Task<MyResult<EnrollmentDto>> EnrollStudent(int callerId, EnrollRequest request)
        {
            if (callerId <= 0)
                return MyResult<EnrollmentDto>.Failure(ErrorType.BadRequest, "Invalid user ID.");

            if (request.CourseId <= 0)
                return MyResult<EnrollmentDto>.Failure(ErrorType.BadRequest, "Invalid course ID.");

            var course = await enrollmentRepository.GetCourseEnrollmentInfoAsync(request.CourseId);

            // Treat draft/retired/deleted courses as non-existent (don't reveal unpublished courses).
            if (course == null || course.IsDeleted || course.Status != "published")
                return MyResult<EnrollmentDto>.Failure(ErrorType.NotFound, "Course not found.");

            if (course.InstructorId == callerId)
                return MyResult<EnrollmentDto>.Failure(ErrorType.BadRequest, "Instructors cannot enroll in their own course.");

            // No payment flow exists, so only free courses can be enrolled.
            if (course.Price > 0)
                return MyResult<EnrollmentDto>.Failure(ErrorType.BadRequest, "This course requires payment, which is not supported yet.");

            string? existingStatus = await enrollmentRepository.GetEnrollmentStatusAsync(callerId, request.CourseId);

            if (existingStatus != null && existingStatus != "dropped")
                return MyResult<EnrollmentDto>.Failure(ErrorType.Conflict, "User is already enrolled in this course.");

            // The enrollments table has UNIQUE(user_id, course_id), so a dropped row must
            // be reactivated rather than a new row inserted.
            if (existingStatus == "dropped")
            {
                var reactivated = await enrollmentRepository.ReactivateDroppedEnrollmentAsync(callerId, request.CourseId);
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

            var result = await enrollmentRepository.EnrollStudentAsync(enrollment);
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

            var r = await enrollmentRepository.GetEnrollmentsByUserIdAsync(userId, pageNumber, pageSize);

            if (r == null)
                return MyResult<PageResult<EnrollmentDto>>.Failure(ErrorType.Failure, "Failed to retrieve enrollments.");

            return MyResult<PageResult<EnrollmentDto>>.Success(r);
        }

        // The course roster is readable only by the owning instructor or an admin.
        public async Task<MyResult<PageResult<EnrollmentDto>>> GetCourseEnrollments(int callerId, string callerRole, int courseId, int pageNumber, int pageSize)
        {
            if (courseId <= 0)
                return MyResult<PageResult<EnrollmentDto>>.Failure(ErrorType.BadRequest, "Invalid course ID.");

            if (pageNumber <= 0 || pageSize <= 0)
                return MyResult<PageResult<EnrollmentDto>>.Failure(ErrorType.BadRequest, "Invalid page number or page size.");

            int? instructorId = await enrollmentRepository.GetCourseInstructorIdAsync(courseId);
            if (instructorId == null)
                return MyResult<PageResult<EnrollmentDto>>.Failure(ErrorType.NotFound, "Course not found.");

            if (callerRole != "admin" && callerId != instructorId)
                return MyResult<PageResult<EnrollmentDto>>.Failure(ErrorType.Unauthorized, "Access denied.");

            var r = await enrollmentRepository.GetEnrollmentsByCourseIdAsync(courseId, pageNumber, pageSize);

            if (r == null)
                return MyResult<PageResult<EnrollmentDto>>.Failure(ErrorType.Failure, "Failed to retrieve enrollments.");

            return MyResult<PageResult<EnrollmentDto>>.Success(r);
        }

        public async Task<MyResult<EnrollmentDto>> MarkLessonProgress(int callerId, MarkLessonProgressRequest request)
        {
            if (callerId <= 0)
                return MyResult<EnrollmentDto>.Failure(ErrorType.BadRequest, "Invalid user ID.");

            if (request.LessonId <= 0)
                return MyResult<EnrollmentDto>.Failure(ErrorType.BadRequest, "Invalid lesson ID.");

            int? courseId = await enrollmentRepository.GetCourseIdByLessonAsync(request.LessonId);
            if (courseId == null)
                return MyResult<EnrollmentDto>.Failure(ErrorType.NotFound, "Lesson not found.");

            // Same codes as GetCourseProgress: not enrolled -> 404 (hidden), enrollment
            // exists but isn't active -> 403.
            string? enrollmentStatus = await enrollmentRepository.GetEnrollmentStatusAsync(callerId, courseId.Value);
            if (enrollmentStatus == null)
                return MyResult<EnrollmentDto>.Failure(ErrorType.NotFound, "You are not enrolled in this course.");

            if (enrollmentStatus is "dropped" or "suspended")
                return MyResult<EnrollmentDto>.Failure(ErrorType.Unauthorized, "Enrollment is not active.");

            if (enrollmentStatus == "completed")
                return MyResult<EnrollmentDto>.Failure(ErrorType.Conflict, "Course is already completed.");

            bool lessonDone = await enrollmentRepository.IsLessonAlreadyCompletedAsync(callerId, request.LessonId);
            if (lessonDone)
                return MyResult<EnrollmentDto>.Failure(ErrorType.Conflict, "Lesson is already completed.");

            var result = await enrollmentRepository.MarkLessonProgressAsync(callerId, request.LessonId, courseId.Value);
            if (result == null)
                return MyResult<EnrollmentDto>.Failure(ErrorType.Failure, "Failed to mark lesson progress.");

            return MyResult<EnrollmentDto>.Success(result);
        }

        public async Task<MyResult<PageResult<LessonProgressDto>>> GetCourseProgress(int callerId, int courseId, int pageNumber, int pageSize)
        {
            if (courseId <= 0)
                return MyResult<PageResult<LessonProgressDto>>.Failure(ErrorType.BadRequest, "Invalid course ID.");

            if (pageNumber <= 0 || pageSize <= 0)
                return MyResult<PageResult<LessonProgressDto>>.Failure(ErrorType.BadRequest, "Invalid page number or page size.");

            // A deleted course must 404 outright — without this, an active enrollee
            // would pass the status check below and get a silently empty page (the
            // repo query filters deleted courses out of the rows).
            var course = await enrollmentRepository.GetCourseEnrollmentInfoAsync(courseId);
            if (course == null || course.IsDeleted)
                return MyResult<PageResult<LessonProgressDto>>.Failure(ErrorType.NotFound, "Course not found.");

            string? enrollmentStatus = await enrollmentRepository.GetEnrollmentStatusAsync(callerId, courseId);
            if (enrollmentStatus == null)
                return MyResult<PageResult<LessonProgressDto>>.Failure(ErrorType.NotFound, "You are not enrolled in this course.");

            if (enrollmentStatus is "dropped" or "suspended")
                return MyResult<PageResult<LessonProgressDto>>.Failure(ErrorType.Unauthorized, "Enrollment is not active.");

            var progress = await enrollmentRepository.GetUserCourseProgressAsync(callerId, courseId, pageNumber, pageSize);
            if (progress == null)
                return MyResult<PageResult<LessonProgressDto>>.Failure(ErrorType.Failure, "Failed to retrieve progress.");

            return MyResult<PageResult<LessonProgressDto>>.Success(progress);
        }
    }
}
