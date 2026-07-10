using Business.Common;
using Business.Dto.Request;
using Business.Dto.Rsponse;
using DataAccess.Dto;
using static DataAccess.Common.clsPageResult;

namespace Business.Interfaces
{
    public interface ICourseService
    {
        Task<MyResult<PageResult<CourseDto>>> GetAllCourses(GetCoursesRequest request);
        Task<MyResult<CourseDto>> AddNewCourse(AddCourseRequest request, int instructorId);
        Task<MyResult<CourseDto>> GetCourseById(int courseId);
        Task<MyResult<bool>> CheckCourseEditPermission(int courseId, int callerId, bool isAdmin);
        Task<MyResult<string?>> SetThumbnail(int courseId, int callerId, bool isAdmin, string fileName);
        Task<MyResult<PageResult<LessonDto>>> GetCourseLessons(int courseId, int callerId, bool isAdmin, int pageNumber, int pageSize);
        Task<MyResult<PageResult<CourseDto>>> GetInstructorCourses(int instructorId, int callerId, string callerRole, int pageNumber, int pageSize);
        Task<MyResult<CourseDto>> UpdateCourse(int courseId, UpdateCourseRequest request, int callerId, bool isAdmin);
        Task<MyResult<CourseDto>> PublishCourse(int courseId, int callerId, bool isAdmin);
        Task<MyResult<CourseDto>> UnpublishCourse(int courseId, int callerId, bool isAdmin);
        Task<MyResult<SectionResponse>> AddNewSection(AddSectionRequest request);
    }
}
