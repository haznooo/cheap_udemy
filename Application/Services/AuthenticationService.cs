
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

			// Username: required, non-blank, and within the DB's VARCHAR(20). Without these the
			// request reaches the DB and blows up as a 500 (constraint violation) or, for a blank
			// username, trips the valid_username_format CHECK — return a clean 400 instead.
			if (string.IsNullOrWhiteSpace(request.Username) || request.Username.Length > 20)
			{
				return MyResult<LoginResponse>.Failure(ErrorType.BadRequest, "Bad username format. Username must be 1-20 characters and not blank.");
			}
			// Password: at least 5, and capped at 20. BCrypt silently truncates at 72 bytes, so an
			// arbitrarily long password is misleading — the tail is ignored. Cap it low and explicit.
			if(request.Password == null || request.Password.Length < 5 || request.Password.Length > 20)
			{
				return MyResult<LoginResponse>.Failure(ErrorType.BadRequest, "Password must be between 5 and 20 characters long.");
			}
            // Email: valid format and at most 50 chars (app policy — 254 is absurd for this app).
            if (request.Email == null || request.Email.Length > 50 || !Regex.IsMatch(request.Email, @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$"))
            {
                return MyResult<LoginResponse>.Failure(ErrorType.BadRequest, "Invalid email format. Email must be at most 50 characters.");
            }
            UserAndProfileRepository UserRepository = new UserAndProfileRepository(context);
            if (await UserRepository.IsUsernameUsedAsync(request.Username))
            {

                return MyResult<LoginResponse>.Failure(ErrorType.Conflict, "Username is already in use.");
            }
            if (await UserRepository.IsEmailUsedAsync(request.Email))
            {

                return MyResult<LoginResponse>.Failure(ErrorType.Conflict, "Email is already in use.");
            }


            //prepare the user entity to be added to database
            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);
            // Signup creates the account only. The profile is created afterwards via
            // POST api/user/me/profile/add, and the avatar via POST api/user/me/avatar —
            // so no client-supplied profile data (especially no image file name) is accepted here.
            var UserEntity = new UserEntity
            {
                user_id = 0,
                username = request.Username,
                email = request.Email,
                hashed_password = hashedPassword,
                role = "student",
                status = "active",
                create_date = DateTime.UtcNow

            };

            var userE = await UserRepository.AddUserAsync(UserEntity);

            //check if user was created successfully
            if (userE == null) return MyResult<LoginResponse>.Failure(ErrorType.Failure, "An error occurred while creating the user.");


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
				Profile = null, // no profile exists yet at signup

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
                (userE.Profile?.DisplayName, userE.Profile?.Bio, userE.Profile?.ImageUrl)


            });

        }
	}
}
