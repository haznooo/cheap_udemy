
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
using System.Text.RegularExpressions;


namespace Business.Services
{
	public class UserService(AppDbContext context)
	{
		public async Task<MyResult<LoginResponse>> UserSignUp(SignUpRequest request,string deviceInfo,string ipAddress)
		{

			// Business Rule
	
			if(request.Password == null || request.Password.Length < 5)
			{
				return MyResult<LoginResponse>.Failure(ErrorType.BadRequest, "Password must be at least 5 characters long.");
			}
            if (request.Email == null || !Regex.IsMatch(request.Email, @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$"))
            {
                return MyResult<LoginResponse>.Failure(ErrorType.BadRequest, "Invalid email format.");
            }
            UserAndProfileRepository UserRepository = new UserAndProfileRepository(context);
            if (await UserRepository.IsEmailUsedAsync(request.Email))
            {

                return MyResult<LoginResponse>.Failure(ErrorType.Conflict, "Email is already in use.");
            }


            //prepare the user entity to be added to database
            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);
            var UserEntity = new UserEntity
            {
                user_id = 0,
                username = request.Username,
                email = request.Email,
                hashed_password = hashedPassword,
                role = "student",
                status = "active",
                create_date = DateTime.UtcNow,
                UserProfile = new UserProfileEntity
                {
                    user_id = 0,
                    display_name = request.Profile?.DisplayName,
                    bio = request.Profile?.Bio,
                    image_url = request.Profile?.ImageUrl,
                    country_id = request.Profile?.CountryId

                }

            };

            var userE = await UserRepository.AddUserAsync(UserEntity);

            //check if user was created successfully
            if (userE == null) return MyResult<LoginResponse>.Failure(ErrorType.Failure, "An error occurred while creating the user.");

            UserProfileResponse userProfileResponse = null;

            userProfileResponse = new UserProfileResponse
				(userE.Profile?.DisplayName, userE.Profile?.Bio, userE.Profile?.ImageUrl,
			 userE.Profile?.CountryId, userE.Profile?.CountryName, userE.Profile?.CountryIsoCode);


            //generate refresh token and save it to database
            TokenService refreshTokenService = new TokenService(context);
			var NewToken = await refreshTokenService.AddNewRefreshTokenFirstTime(new RefreshTokenRequest
			(
				RefreshToken: null,
				deviceInfo: deviceInfo,
				IpAddress: ipAddress,
				UserId: userE.UserId
			));


            await new LoginLogService(context).LogAsync(userE.UserId, "success", ipAddress, deviceInfo);

			//make the response
			var response = new LoginResponse
            {
				Id = userE.UserId,
				Username = userE.Username,
				Role = userE.Role,
				Status = userE.Status,
				Profile = userProfileResponse,
				RefreshToken = NewToken.Value?.RefreshToken,
				Email = userE.Email,
				IsRefreshTokenRevoked = false,

				RefreshTokenExpiresAt = NewToken.Value?.ExpiresAt


			};
			return MyResult<LoginResponse>.Success(response);

		}
        public async Task<MyResult<LoginResponse>> LoginUser(LoginRequest request,string deviceInfo,string ipAddress)
        {


            if (request.Email == null || !Regex.IsMatch(request.Email, @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$"))
            {
                return MyResult<LoginResponse>.Failure(ErrorType.BadRequest, "Invalid email format.");
            }

            UserAndProfileRepository repo = new UserAndProfileRepository(context);

            string? HashedPassword = await repo.GetHashedPasswordByEmailAsync(request.Email);

            if (HashedPassword == null)
            {
                // Unknown email: still auditable. Record the attempted identifier (never the password).
                await new LoginLogService(context).LogAsync(null, "failed", ipAddress, deviceInfo, request.Email);
                return MyResult<LoginResponse>.Failure(ErrorType.NotFound, "invalid credentials");
            }

            // user exists — get their id so we can log success or failure
            int? userId = await repo.GetUserIdByEmail(request.Email);

            bool isValidPassword = BCrypt.Net.BCrypt.Verify(request.Password, Convert.ToString(HashedPassword));

            if (!isValidPassword)
            {
                await new LoginLogService(context).LogAsync(userId, "failed", ipAddress, deviceInfo, request.Email);
                return MyResult<LoginResponse>.Failure(ErrorType.Unauthorized, "invalid credentials");
            }

            var userE = await repo.GetUserByEmailAsync(request.Email);


            if (userE == null) return MyResult<LoginResponse>.Failure(ErrorType.Unauthorized, "invalid credentials");


            TokenService refreshTokenService = new TokenService(context);
            var NewRefreshToken = await refreshTokenService.AddNewRefreshTokenFirstTime(new RefreshTokenRequest
            (
                RefreshToken: null,
                deviceInfo: deviceInfo,
                IpAddress: ipAddress,
                UserId: userE.UserId
            ));

            await new LoginLogService(context).LogAsync(userE.UserId, "success", ipAddress, deviceInfo);

            return MyResult<LoginResponse>.Success(new LoginResponse
            {
                Id = userE.UserId,
                Username = userE.Username,
                Email = userE.Email,
                Role = userE.Role,
                Status = userE.Status,
				IsRefreshTokenRevoked = false,
                RefreshToken = NewRefreshToken.Value?.RefreshToken,
                RefreshTokenExpiresAt = NewRefreshToken.Value?.ExpiresAt,
                Profile = new UserProfileResponse
                (userE.Profile?.DisplayName, userE.Profile?.Bio, userE.Profile?.ImageUrl,
                userE.Profile?.CountryId, userE.Profile?.CountryName, userE.Profile?.CountryIsoCode)


            });

        }
        public async Task<MyResult<bool>> DeleteUser(int userid,DeleteUserRequest request)
		{

			if (userid < 0) { return MyResult<bool>.Failure(ErrorType.BadRequest, "user id can not be zero or negative"); }

			UserAndProfileRepository UserRepository = new UserAndProfileRepository(context);

			string? HashedPassword = await UserRepository.GetHashedPasswordByIdAsync(userid);

			if (HashedPassword == null) return MyResult<bool>.Failure(ErrorType.NotFound, "user not found");

			bool isValidPassword = BCrypt.Net.BCrypt.Verify(request.Password, Convert.ToString(HashedPassword));

			if (!isValidPassword) return MyResult<bool>.Failure(ErrorType.Unauthorized, "invalid credintials");


			var result = await UserRepository.DeleteUserAsync_Anonymize(userid);

			// 4. Return Success
			return MyResult<bool>.Success(result);
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

            if (userId < 0) { return MyResult<bool>.Failure(ErrorType.BadRequest, "user id can not be zero or negative"); }

            UserAndProfileRepository UserRepository = new UserAndProfileRepository(context);

            string? HashedPassword = await UserRepository.GetHashedPasswordByIdAsync(userId);

            if (HashedPassword == null) return MyResult<bool>.Failure(ErrorType.NotFound, "user not found");

            bool isValidPassword = BCrypt.Net.BCrypt.Verify(request.OldPassword, Convert.ToString(HashedPassword));

            if (!isValidPassword) return MyResult<bool>.Failure(ErrorType.Unauthorized, "invalid credintials");


            string NewhashedPassword = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);

            var results = await UserRepository.UpdateUserPasswordAsync(userId, NewhashedPassword);

            return MyResult<bool>.Success(results);
         
        }

        public async Task<MyResult<UserProfileResponse>> UpdateUserProfile(int userid, UserProfileRequest request)
        {
        
            UserAndProfileRepository repo = new UserAndProfileRepository(context);

            bool userExists = await repo.DoesUserExistByIdAsync(userid);

            if (!userExists) return MyResult<UserProfileResponse>.Failure(ErrorType.NotFound, "user not found");

            var profileE = new UserProfileEntity()
            {
                user_id = userid,
                bio = request?.Bio,
                country_id = request?.CountryId,
                image_url = request?.ImageUrl,
                display_name = request?.DisplayName,

            };

           var r = await  repo.UpdateUserProfileByUserIdAsync(userid, profileE);

            return  MyResult<UserProfileResponse>.Success( new UserProfileResponse(r?.display_name, r?.bio, r?.image_url, r?.country_id, r?.country?.name, r?.country?.iso_code));
                
               


        }
	
	}
}
