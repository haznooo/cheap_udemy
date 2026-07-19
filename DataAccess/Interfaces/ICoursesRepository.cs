using DataAccess.Dto;
using DataAccess.Entities;
using static DataAccess.Common.clsPageResult;

namespace DataAccess.Interfaces
{
    public interface ICoursesRepository
    {
        Task<PageResult<CourseDto>> GetAllCourses(
            int pageNumber, int pageSize,
            string? search = null, int? categoryId = null, string? level = null,
            decimal? minPrice = null, decimal? maxPrice = null, string? sortBy = null);
        Task<CourseDto> GetCourseById(int courseId, int? callerId = null, bool isAdmin = false);
        Task<int?> GetCourseInstructorId(int courseId);
        Task<int?> GetCourseIdBySection(int sectionId);
        Task<(bool Success, string? OldFileName)> UpdateThumbnail(int courseId, string fileName);
        Task<CourseDto> AddNewCourse(CourseEntitiy CourseE);
        Task<PageResult<LessonDto>> GetCourseLessons(int courseId, int pageNumber, int pageSize, bool includeUnpublished);
        Task<PageResult<SectionDto>> GetCourseSections(int courseId, int pageNumber, int pageSize);
        Task<int> GetMaxSortOrderForCourseAsync(int courseId);
        Task<(SectionEntitiy? Result, bool Conflict)> AddNewSection(SectionEntitiy section);
        Task<(SectionDto? Result, bool Conflict)> UpdateSectionAsync(int sectionId, string? title, int? sortOrder);
        Task<bool> DeleteSectionAsync(int sectionId);
        Task<CourseDto?> UpdateCourseAsync(int courseId, string? title, string? description, string? code, decimal? price, string? level, int? categoryId);
        Task<PageResult<CourseDto>> GetCoursesByInstructorIdAsync(int instructorId, int pageNumber, int pageSize);
        Task<CourseEntitiy?> GetRawCourseAsync(int courseId);
        Task<CourseDto?> UpdateCourseStatusAsync(int courseId, string newStatus);
        Task<bool> SoftDeleteCourseAsync(int courseId, string? removalReason);
        Task<bool> DoesCategoryExistAsync(int categoryId);
    }
}
