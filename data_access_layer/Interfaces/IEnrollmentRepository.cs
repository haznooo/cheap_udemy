using DataAccess.Dto;
using DataAccess.Entities;
using static DataAccess.Common.clsPageResult;

namespace DataAccess.Interfaces
{
    public interface IEnrollmentRepository
    {
        Task<bool> IsAlreadyEnrolledAsync(int userId, int courseId);
        Task<int?> GetCourseInstructorIdAsync(int courseId);
        Task<CourseEnrollmentInfoDto?> GetCourseEnrollmentInfoAsync(int courseId);
        Task<bool> CanViewCourseContentAsync(int courseId, int callerId, bool isAdmin);
        Task<int?> GetCourseIdByLessonAsync(int lessonId);
        Task<string?> GetEnrollmentStatusAsync(int userId, int courseId);
        Task<bool> IsLessonAlreadyCompletedAsync(int userId, int lessonId);
        Task<EnrollmentDto?> EnrollStudentAsync(EnrollmentEntitiy enrollment);
        Task<PageResult<EnrollmentDto>> GetEnrollmentsByUserIdAsync(int userId, int pageNumber, int pageSize);
        Task<PageResult<EnrollmentDto>> GetEnrollmentsByCourseIdAsync(int courseId, int pageNumber, int pageSize);
        Task<EnrollmentDto?> MarkLessonProgressAsync(int userId, int lessonId, int courseId);
        Task<bool> DropEnrollmentAsync(int userId, int courseId);
        Task<PageResult<LessonProgressDto>> GetUserCourseProgressAsync(int userId, int courseId, int pageNumber, int pageSize);
        Task<EnrollmentDto?> ReactivateDroppedEnrollmentAsync(int userId, int courseId);
    }
}
