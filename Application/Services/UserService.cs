
using Business.Common;
using Business.Dto.Request;
using Business.Dto.Rsponse;
using Business.Interfaces;
using DataAccess.Entities;
using DataAccess.Interfaces;


namespace Business.Services
{
	public class UserService(IUserAndProfileRepository userRepository, IRefreshTokenService refreshTokenService) : IUserService
	{
        // On success the value is the deleted user's avatar file name (null if none)
        // so the controller can remove the file from storage — the anonymize trigger
        // deletes the profile row, after which the name is unrecoverable.
        // Self-delete: the caller confirms with their OWN password (userid == caller).
        public async Task<MyResult<string?>> DeleteUser(int userid,DeleteUserRequest request)
		{

			if (userid <= 0) { return MyResult<string?>.Failure(ErrorType.BadRequest, "user id can not be zero or negative"); }


			if (string.IsNullOrEmpty(request.Password)) { return MyResult<string?>.Failure(ErrorType.BadRequest, "Password is required."); }

			string? HashedPassword = await userRepository.GetHashedPasswordByIdAsync(userid);

			if (HashedPassword == null) return MyResult<string?>.Failure(ErrorType.NotFound, "user not found");

			bool isValidPassword = BCrypt.Net.BCrypt.Verify(request.Password, Convert.ToString(HashedPassword));

			if (!isValidPassword) return MyResult<string?>.Failure(ErrorType.Unauthorized, "invalid credintials");

			// Capture the avatar name BEFORE the delete wipes the profile row.
			string? avatarFileName = (await userRepository.GetUserProfileByIdAsync(userid))?.ImageUrl;

			var result = await userRepository.DeleteUserAsync_Anonymize(userid);

			if (!result) return MyResult<string?>.Failure(ErrorType.Failure, "failed to delete user");

			return MyResult<string?>.Success(avatarFileName);
		}

		// Admin-initiated cross-user actions (delete, ban/suspend/unban, admin read)
		// live in AdminService — this service is self-service ("me/*") only.

		public async Task<bool> IsUserActive(int userId)
		{
            return await userRepository.IsUserActiveAsync(userId);

        }

	   public async Task<MyResult<bool>> UpdatePassword(int userId,UpdatePasswordRequest request)
	   {

            if (request.NewPassword == null || request.NewPassword.Length < 5)
            {
                return MyResult<bool>.Failure(ErrorType.BadRequest, "Password must be at least 5 characters long.");
            }

            if (string.IsNullOrEmpty(request.OldPassword))
            {
                return MyResult<bool>.Failure(ErrorType.BadRequest, "Current password is required.");
            }

            if (userId <= 0) { return MyResult<bool>.Failure(ErrorType.BadRequest, "user id can not be zero or negative"); }

            // A still-valid access token can outlive the account by up to its 20-min
            // lifetime (deletion/ban does NOT revoke JWTs). Block a non-active caller
            // from mutating a dead/suspended account.
            if (!await userRepository.IsUserActiveAsync(userId))
                return MyResult<bool>.Failure(ErrorType.NotFound, "user not found");

            string? HashedPassword = await userRepository.GetHashedPasswordByIdAsync(userId);

            if (HashedPassword == null) return MyResult<bool>.Failure(ErrorType.NotFound, "user not found");

            bool isValidPassword = BCrypt.Net.BCrypt.Verify(request.OldPassword, Convert.ToString(HashedPassword));

            if (!isValidPassword) return MyResult<bool>.Failure(ErrorType.Unauthorized, "invalid credintials");


            string NewhashedPassword = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);

            var results = await userRepository.UpdateUserPasswordAsync(userId, NewhashedPassword);

            if (results)
            {
                // A password change must kill existing sessions, otherwise a stolen refresh token
                // survives the very change meant to lock the attacker out.
                await refreshTokenService.RevokeAllForUser(userId);
            }

            return MyResult<bool>.Success(results);

        }

        public async Task<MyResult<UserProfileResponse>> GetUserProfile(int userId)
        {
            if (userId <= 0) return MyResult<UserProfileResponse>.Failure(ErrorType.BadRequest, "user id can not be zero or negative");

            var p = await userRepository.GetUserProfileByIdAsync(userId);

            if (p == null) return MyResult<UserProfileResponse>.Failure(ErrorType.NotFound, "user not found");

            return MyResult<UserProfileResponse>.Success(
                new UserProfileResponse(p.DisplayName, p.Bio, p.ImageUrl));
        }

        // Persists an already-uploaded avatar file name onto the user's profile.
        // On success the value is the REPLACED file name (null if there was no
        // avatar yet) so the controller can remove the stale file from storage.
        public async Task<MyResult<string?>> SetAvatar(int userId, string fileName)
        {
            if (userId <= 0) return MyResult<string?>.Failure(ErrorType.BadRequest, "user id can not be zero or negative");

            // A still-valid access token can outlive the account (deletion/ban does
            // NOT revoke JWTs). Without this guard the avatar upsert below would
            // resurrect a profile row for a deleted account.
            if (!await userRepository.IsUserActiveAsync(userId))
                return MyResult<string?>.Failure(ErrorType.NotFound, "user not found");

            var oldFileName = await userRepository.UpdateUserAvatarAsync(userId, fileName);

            return MyResult<string?>.Success(oldFileName);
        }

        // Clears the user's avatar. On success the value is the REMOVED file name
        // (null if there was none) so the controller can delete the stale file.
        public async Task<MyResult<string?>> RemoveAvatar(int userId)
        {
            if (userId <= 0) return MyResult<string?>.Failure(ErrorType.BadRequest, "user id can not be zero or negative");

            // Same 20-min stale-token window as SetAvatar: don't let a deleted/banned
            // caller mutate the (already-gone) profile row.
            if (!await userRepository.IsUserActiveAsync(userId))
                return MyResult<string?>.Failure(ErrorType.NotFound, "user not found");

            var oldFileName = await userRepository.RemoveUserAvatarAsync(userId);

            return MyResult<string?>.Success(oldFileName);
        }

        public async Task<MyResult<UserProfileResponse>> AddUserProfile(int userid, UserProfileRequest request)
        {
            bool userExists = await userRepository.DoesUserExistByIdAsync(userid);

            if (!userExists) return MyResult<UserProfileResponse>.Failure(ErrorType.NotFound, "user not found");

            bool profileExists = await userRepository.DoesUserProfileExistAsync(userid);

            if (profileExists) return MyResult<UserProfileResponse>.Failure(ErrorType.Conflict, "user profile already exists");

            var profileE = new UserProfileEntity()
            {
                user_id = userid,
                bio = request?.Bio,
                display_name = request?.DisplayName,
            };

            var r = await userRepository.AddUserProfileAsync(userid, profileE);

            return MyResult<UserProfileResponse>.Success(new UserProfileResponse(r?.display_name, r?.bio, r?.image_url));
        }

        public async Task<MyResult<UserProfileResponse>> UpdateUserProfile(int userid, UserProfileRequest request)
        {
            bool profileExists = await userRepository.DoesUserProfileExistAsync(userid);

            if (!profileExists) return MyResult<UserProfileResponse>.Failure(ErrorType.NotFound, "user profile not found");

            var profileE = new UserProfileEntity()
            {
                user_id = userid,
                bio = request?.Bio,
                display_name = request?.DisplayName,
            };

            var r = await userRepository.UpdateUserProfileByUserIdAsync(userid, profileE);

            return MyResult<UserProfileResponse>.Success(new UserProfileResponse(r?.display_name, r?.bio, r?.image_url));
        }

	}
}
