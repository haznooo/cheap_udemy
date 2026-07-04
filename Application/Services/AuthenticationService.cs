
using Business.Common;
using Business.Dto.Request;
using Business.Dto.Rsponse;
using DataAccess.Data;
using DataAccess.Entities;
using DataAccess.Repositories;
using System.Text.RegularExpressions;

namespace Business.Services
{
	public class AuthenticationService(AppDbContext context)
	{
		// A real BCrypt hash (default work factor) used only to spend the same verify time on the
		// unknown-email path as on a real login, so response timing doesn't reveal whether an email
		// exists. Computed once at class load.
		private static readonly string DummyPasswordHash = BCrypt.Net.BCrypt.HashPassword("timing-equalizer");

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
            RefreshTokenService refreshTokenService = new RefreshTokenService(context);
			var NewToken = await refreshTokenService.AddNewRefreshTokenFirstTime(userE.UserId, deviceInfo, ipAddress);

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
                // Spend the same BCrypt time as a real login and return the SAME failure (401, same
                // message) as a wrong password, so neither status code nor timing reveals that the
                // email doesn't exist.
                BCrypt.Net.BCrypt.Verify(request.Password ?? "", DummyPasswordHash);
                return MyResult<LoginResponse>.Failure(ErrorType.Unauthorized, "invalid credentials");
            }

            // user exists — get their id so we can log success or failure
            int? userId = await repo.GetUserIdByEmail(request.Email);

            bool isValidPassword = BCrypt.Net.BCrypt.Verify(request.Password ?? "", Convert.ToString(HashedPassword));

            if (!isValidPassword)
            {
                await new LoginLogService(context).LogAsync(userId, "failed", ipAddress, deviceInfo, request.Email);
                return MyResult<LoginResponse>.Failure(ErrorType.Unauthorized, "invalid credentials");
            }

            var userE = await repo.GetUserByEmailAsync(request.Email);


            if (userE == null) return MyResult<LoginResponse>.Failure(ErrorType.Unauthorized, "invalid credentials");


            RefreshTokenService refreshTokenService = new RefreshTokenService(context);
            var NewRefreshToken = await refreshTokenService.AddNewRefreshTokenFirstTime(userE.UserId, deviceInfo, ipAddress);

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
	}
}
