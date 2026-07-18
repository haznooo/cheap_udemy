using Business.Common;
using DataAccess.Dto;

namespace Business.Interfaces
{
    public interface IAdminService
    {
        Task<MyResult<UserAndProfileDto>> GetUser(int userId);
        Task<MyResult<string?>> DeleteUser(int adminId, int targetUserId);
        Task<MyResult<bool>> SetUserStatus(int adminId, int targetUserId, string newStatus);
    }
}
