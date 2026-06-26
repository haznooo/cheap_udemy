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
        public async Task<MyResult<EnrollmentDto>> EnrollStudent(EnrollRequest request)
        {
            if (request.UserId <= 0)
                return MyResult<EnrollmentDto>.Failure(ErrorType.BadRequest, "Invalid user ID.");

            if (request.CourseId <= 0)
                return MyResult<EnrollmentDto>.Failure(ErrorType.BadRequest, "Invalid course ID.");

            var repo = new EnrollmentRepository(context);

            bool alreadyEnrolled = await repo.IsAlreadyEnrolledAsync(request.UserId, request.CourseId);
            if (alreadyEnrolled)
                return MyResult<EnrollmentDto>.Failure(ErrorType.Conflict, "User is already enrolled in this course.");

            var enrollment = new EnrollmentEntitiy
            {
                user_id = request.UserId,
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

        public async Task<MyResult<PageResult<EnrollmentDto>>> GetUserEnrollments(int userId, int pageNumber, int pageSize)
        {
            if (userId <= 0)
                return MyResult<PageResult<EnrollmentDto>>.Failure(ErrorType.BadRequest, "Invalid user ID.");

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

        public async Task<MyResult<PageResult<EnrollmentDto>>> GetCourseEnrollments(int courseId, int pageNumber, int pageSize)
        {
            if (courseId <= 0)
                return MyResult<PageResult<EnrollmentDto>>.Failure(ErrorType.BadRequest, "Invalid course ID.");

            if (pageNumber <= 0 || pageSize <= 0)
                return MyResult<PageResult<EnrollmentDto>>.Failure(ErrorType.BadRequest, "Invalid page number or page size.");

            var repo = new EnrollmentRepository(context);
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

        public async Task<MyResult<bool>> DropEnrollment(DropEnrollmentRequest request)
        {
            if (request.UserId <= 0)
                return MyResult<bool>.Failure(ErrorType.BadRequest, "Invalid user ID.");

            if (request.CourseId <= 0)
                return MyResult<bool>.Failure(ErrorType.BadRequest, "Invalid course ID.");

            var repo = new EnrollmentRepository(context);

            bool alreadyEnrolled = await repo.IsAlreadyEnrolledAsync(request.UserId, request.CourseId);
            if (!alreadyEnrolled)
                return MyResult<bool>.Failure(ErrorType.NotFound, "Enrollment not found.");

            var result = await repo.DropEnrollmentAsync(request.UserId, request.CourseId);
            if (!result)
                return MyResult<bool>.Failure(ErrorType.Failure, "Failed to drop enrollment.");

            return MyResult<bool>.Success(true);
        }
    }
}
