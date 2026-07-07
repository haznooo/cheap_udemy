
using AngleSharp.Io;
using Business.Common;
using Business.Dto.Request;
using Business.Dto.Rsponse;
using DataAccess.Data;
using DataAccess.Entities;
using DataAccess.Repositories;
using Supabase.Gotrue;
using System.Security.Cryptography;
using System.Text;


namespace Business.Services
{
	public class UserService(AppDbContext context)
	{
        // On success the value is the deleted user's avatar file name (null if none)
        // so the controller can remove the file from storage — the anonymize trigger
        // deletes the profile row, after which the name is unrecoverable.
        public async Task<MyResult<string?>> DeleteUser(int userid,DeleteUserRequest request)
		{

			if (userid <= 0) { return MyResult<string?>.Failure(ErrorType.BadRequest, "user id can not be zero or negative"); }

			UserAndProfileRepository UserRepository = new UserAndProfileRepository(context);

			string? HashedPassword = await UserRepository.GetHashedPasswordByIdAsync(userid);

			if (HashedPassword == null) return MyResult<string?>.Failure(ErrorType.NotFound, "user not found");

			bool isValidPassword = BCrypt.Net.BCrypt.Verify(request.Password, Convert.ToString(HashedPassword));

			if (!isValidPassword) return MyResult<string?>.Failure(ErrorType.Unauthorized, "invalid credintials");

			// Capture the avatar name BEFORE the delete wipes the profile row.
			string? avatarFileName = (await UserRepository.GetUserProfileByIdAsync(userid))?.ImageUrl;

			var result = await UserRepository.DeleteUserAsync_Anonymize(userid);

			if (!result) return MyResult<string?>.Failure(ErrorType.Failure, "failed to delete user");

			return MyResult<string?>.Success(avatarFileName);
		}

		public async Task<bool> IsUserActive(int userId)
		{
            UserAndProfileRepository repo = new UserAndProfileRepository(context);
            return await repo.IsUserActiveAsync(userId);

        }

	   public async Task<MyResult<bool>> UpdatePassword(int userId,UpdatePasswordRequest request)
	   {

            if (request.NewPassword == null || request.NewPassword.Length < 5)
            {
                return MyResult<bool>.Failure(ErrorType.BadRequest, "Password must be at least 5 characters long.");
            }

            if (userId <= 0) { return MyResult<bool>.Failure(ErrorType.BadRequest, "user id can not be zero or negative"); }

            UserAndProfileRepository UserRepository = new UserAndProfileRepository(context);

            string? HashedPassword = await UserRepository.GetHashedPasswordByIdAsync(userId);

            if (HashedPassword == null) return MyResult<bool>.Failure(ErrorType.NotFound, "user not found");

            bool isValidPassword = BCrypt.Net.BCrypt.Verify(request.OldPassword, Convert.ToString(HashedPassword));

            if (!isValidPassword) return MyResult<bool>.Failure(ErrorType.Unauthorized, "invalid credintials");


            string NewhashedPassword = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);

            var results = await UserRepository.UpdateUserPasswordAsync(userId, NewhashedPassword);

            if (results)
            {
                // A password change must kill existing sessions, otherwise a stolen refresh token
                // survives the very change meant to lock the attacker out.
                await new RefreshTokenService(context).RevokeAllForUser(userId);
            }

            return MyResult<bool>.Success(results);

        }

        public async Task<MyResult<UserProfileResponse>> GetUserProfile(int userId)
        {
            if (userId <= 0) return MyResult<UserProfileResponse>.Failure(ErrorType.BadRequest, "user id can not be zero or negative");

            UserAndProfileRepository repo = new UserAndProfileRepository(context);

            var p = await repo.GetUserProfileByIdAsync(userId);

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

            UserAndProfileRepository repo = new UserAndProfileRepository(context);

            var oldFileName = await repo.UpdateUserAvatarAsync(userId, fileName);

            return MyResult<string?>.Success(oldFileName);
        }

        public async Task<MyResult<UserProfileResponse>> AddUserProfile(int userid, UserProfileRequest request)
        {
            UserAndProfileRepository repo = new UserAndProfileRepository(context);

            bool userExists = await repo.DoesUserExistByIdAsync(userid);

            if (!userExists) return MyResult<UserProfileResponse>.Failure(ErrorType.NotFound, "user not found");

            bool profileExists = await repo.DoesUserProfileExistAsync(userid);

            if (profileExists) return MyResult<UserProfileResponse>.Failure(ErrorType.Conflict, "user profile already exists");

            var profileE = new UserProfileEntity()
            {
                user_id = userid,
                bio = request?.Bio,
                display_name = request?.DisplayName,
            };

            var r = await repo.AddUserProfileAsync(userid, profileE);

            return MyResult<UserProfileResponse>.Success(new UserProfileResponse(r?.display_name, r?.bio, r?.image_url));
        }

        public async Task<MyResult<UserProfileResponse>> UpdateUserProfile(int userid, UserProfileRequest request)
        {
            UserAndProfileRepository repo = new UserAndProfileRepository(context);

            bool profileExists = await repo.DoesUserProfileExistAsync(userid);

            if (!profileExists) return MyResult<UserProfileResponse>.Failure(ErrorType.NotFound, "user profile not found");

            var profileE = new UserProfileEntity()
            {
                user_id = userid,
                bio = request?.Bio,
                display_name = request?.DisplayName,
            };

            var r = await repo.UpdateUserProfileByUserIdAsync(userid, profileE);

            return MyResult<UserProfileResponse>.Success(new UserProfileResponse(r?.display_name, r?.bio, r?.image_url));
        }
	
	}
}
