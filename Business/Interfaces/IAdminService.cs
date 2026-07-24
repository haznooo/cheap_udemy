using Business.Common;
using DataAccess.Dto;
using static DataAccess.Common.clsPageResult;

namespace Business.Interfaces
{
    public interface IAdminService
    {
        Task<MyResult<PageResult<UserListItemDto>>> GetUsers(int pageNumber, int pageSize, string? status = null);
        Task<MyResult<UserAndProfileDto>> GetUser(int userId);
        Task<MyResult<string?>> DeleteUser(int adminId, int targetUserId);
        Task<MyResult<bool>> SetUserStatus(int adminId, int targetUserId, string newStatus);
        Task<MyResult<bool>> SetCourseSuspension(int adminId, int courseId, bool suspend);
        Task<MyResult<bool>> TakedownCourse(int adminId, int courseId, string? removalReason);
    }
}
