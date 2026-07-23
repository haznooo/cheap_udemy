using Business.Common;
using Business.Dto.Request;
using DataAccess.Dto;
using static DataAccess.Common.clsPageResult;

namespace Business.Interfaces
{
    public interface IEnrollmentService
    {
        Task<MyResult<EnrollmentDto>> EnrollStudent(int callerId, EnrollRequest request);
        Task<MyResult<PageResult<EnrollmentDto>>> GetUserEnrollments(int callerId, string callerRole, int userId, int pageNumber, int pageSize, bool excludeDeletedCourses);
        Task<MyResult<PageResult<CourseEnrollmentDto>>> GetCourseEnrollments(int callerId, string callerRole, int courseId, int pageNumber, int pageSize);
        Task<MyResult<EnrollmentDto>> MarkLessonProgress(int callerId, MarkLessonProgressRequest request);
        Task<MyResult<PageResult<LessonProgressDto>>> GetCourseProgress(int callerId, int courseId, int pageNumber, int pageSize);
    }
}
