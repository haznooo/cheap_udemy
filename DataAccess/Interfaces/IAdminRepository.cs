using DataAccess.Dto;
using DataAccess.Entities.json;
using static DataAccess.Common.clsPageResult;

namespace DataAccess.Interfaces
{
    // Admin-exclusive data access. A method lives here iff ONLY the admin flow
    // (AdminService) uses it — anything shared with a self/instructor flow stays in
    // its per-table repo (see UserAndProfileRepository/CoursesRepository), and
    // AdminService injects both. The admin is the one actor whose behaviours are a
    // disjoint set (list-all / ban / suspend / takedown), so it's the only one that
    // earns its own repo cleanly; students and instructors share too much to split.
    public interface IAdminRepository
    {
        // users
        Task<PageResult<UserListItemDto>> GetUsersAsync(int pageNumber, int pageSize, string? status = null, string? search = null);
        Task<UserAndProfileDto?> GetUserWithProfileForAdminAsync(int userId);
        Task<bool> DoesUserExistByIdAsync(int userId);
        Task<UserStatusRoleDto?> GetUserStatusAndRoleAsync(int userId);
        Task<bool> UpdateUserStatusAsync(int userId, string status);

        // courses
        Task<PageResult<AdminCourseDto>> GetAllCoursesForAdminAsync(int pageNumber, int pageSize, string? status = null, string? search = null);
        Task<List<List<ContentBlock>>> GetCourseLessonContentBlocksAsync(int courseId);
        Task<bool> PurgeCourseContentAsync(int courseId, string? removalReason);

        // reviews
        Task<bool> DeleteAllReviewsForCourseAsync(int courseId);
    }
}
