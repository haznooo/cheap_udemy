using Business.Common;
using Business.Dto.Request;
using Business.Dto.Rsponse;

namespace Business.Interfaces
{
    public interface IUserService
    {
        Task<MyResult<string?>> DeleteUser(int userid, DeleteUserRequest request);
        Task<MyResult<string?>> AdminDeleteUser(int userId);
        Task<bool> IsUserActive(int userId);
        Task<MyResult<bool>> UpdatePassword(int userId, UpdatePasswordRequest request);
        Task<MyResult<UserProfileResponse>> GetUserProfile(int userId);
        Task<MyResult<string?>> SetAvatar(int userId, string fileName);
        Task<MyResult<string?>> RemoveAvatar(int userId);
        Task<MyResult<UserProfileResponse>> AddUserProfile(int userid, UserProfileRequest request);
        Task<MyResult<UserProfileResponse>> UpdateUserProfile(int userid, UserProfileRequest request);
    }
}
