using DataAccess.Dto;
using DataAccess.Entities;
using static DataAccess.Common.clsPageResult;

namespace DataAccess.Interfaces
{
    public interface IUserAndProfileRepository
    {
        //user
        Task<UserAndProfileDto> AddUserAsync(UserEntity User);
        Task<bool> DeleteUserAsync_Anonymize(UserEntity User);
        Task<bool> DeleteUserAsync_Anonymize(int userId);
        Task<bool> UpdateUserPasswordAsync(int userId, string newHashedPassword);
        Task<PageResult<UserListItemDto>> GetUsersAsync(int pageNumber, int pageSize);
        Task<UserAndProfileDto> GetUserByCredentialsAsync(string email, string hashed_password);
        Task<UserAndProfileDto> GetUserByEmailAsync(string email);
        Task<LoginLookupDto?> GetUserForLoginAsync(string email);
        Task<UserAndProfileDto> GetUserByIdAsync(int userId);
        Task<UserAndProfileDto?> GetUserWithProfileForAdminAsync(int userId);

        Task<PublicUserDto?> GetPublicUserInfoAsync(int userId);

        // profile
        Task<UserProfileDto?> GetUserProfileByIdAsync(int userId);
        Task<string?> UpdateUserAvatarAsync(int userId, string fileName);
        Task<string?> RemoveUserAvatarAsync(int userId);
        Task<UserProfileEntity> AddUserProfileAsync(int UserId, UserProfileEntity NewUserProfileData);
        Task<UserProfileEntity> UpdateUserProfileByUserIdAsync(int UserId, UserProfileEntity NewUserProfileData);

        //user utitlity
        Task<bool> IsEmailUsedAsync(string email);
        Task<bool> IsUsernameUsedAsync(string username);
        Task<bool> IsUserActiveAsync(int userId);
        Task<bool> DoesUserExistByIdAsync(int userId);
        Task<bool> DoesUserProfileExistAsync(int userId);

        //custom elemnts
        Task<string?> GetHashedPasswordByEmailAsync(string email);
        Task<bool> PromoteUserToInstructorAsync(int userId);
        Task<string?> GetUserRoleAsync(int userId);
        Task<UserStatusRoleDto?> GetUserStatusAndRoleAsync(int userId);
        Task<bool> UpdateUserStatusAsync(int userId, string status);
        Task<string?> GetHashedPasswordByIdAsync(int userId);
        Task<int?> GetUserIdByEmail(string email);
    }
}
