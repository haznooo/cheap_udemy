
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

			// Username: an Instagram/TikTok-style handle — ASCII letters, digits, '.' and '_' only,
			// 1-20 chars (bounded by users.username VARCHAR(20)). The regex anchors force the first and
			// last char to be non-period (no leading/trailing dot); the Contains("..") check blocks
			// consecutive dots (and, with the anchors, the all-dots case). Restricting the charset keeps
			// the handle URL-safe and removes it as a homoglyph-spoofing / stored-XSS surface — real
			// names live in users_profile.display_name, which stays free-form.
			if (string.IsNullOrWhiteSpace(request.Username)
				|| request.Username.Length > 20
				|| !Regex.IsMatch(request.Username, @"^[a-zA-Z0-9_](?:[a-zA-Z0-9._]*[a-zA-Z0-9_])?$")
				|| request.Username.Contains(".."))
			{
				return MyResult<LoginResponse>.Failure(ErrorType.BadRequest, "Invalid username. Use 1-20 letters, digits, '.' or '_' — no spaces or other symbols, and no leading/trailing or repeated dots.");
			}
			// Handles are case-insensitive (IG behavior): normalize to lowercase so 'John' and 'john'
			// can't coexist. Stored lowercased, so the existing UNIQUE constraint enforces this for free.
			string normalizedUsername = request.Username.ToLowerInvariant();
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
            if (await UserRepository.IsUsernameUsedAsync(normalizedUsername))
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
                username = normalizedUsername,
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


            // Length capped at the users.email / login_logs.attempted_identifier column width (254) so an
            // oversized identifier can't overflow the failed-login audit insert. No signup-style 50 cap here:
            // login must keep accepting any account that was created under an older, looser policy.
            if (request.Email == null || request.Email.Length > 254 || !Regex.IsMatch(request.Email, @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$"))
            {
                return MyResult<LoginResponse>.Failure(ErrorType.BadRequest, "Invalid email format.");
            }

            UserAndProfileRepository repo = new UserAndProfileRepository(context);

            // One query fetches everything the login needs — id, account fields, hash, profile.
            var user = await repo.GetUserForLoginAsync(request.Email);

            // Unknown email OR an anonymized/deleted user (NULL hash): still auditable (record the
            // attempted identifier, never the password). Spend the same BCrypt time as a real login
            // and return the SAME failure (401, same message) as a wrong password, so neither status
            // code nor timing reveals whether the email exists.
            if (user == null || user.HashedPassword == null)
            {
                await new LoginLogService(context).LogAsync(user?.UserId, "failed", ipAddress, deviceInfo, request.Email);
                BCrypt.Net.BCrypt.Verify(request.Password ?? "", DummyPasswordHash);
                return MyResult<LoginResponse>.Failure(ErrorType.Unauthorized, "invalid credentials");
            }

            bool isValidPassword = BCrypt.Net.BCrypt.Verify(request.Password ?? "", user.HashedPassword);

            if (!isValidPassword)
            {
                await new LoginLogService(context).LogAsync(user.UserId, "failed", ipAddress, deviceInfo, request.Email);
                return MyResult<LoginResponse>.Failure(ErrorType.Unauthorized, "invalid credentials");
            }

            // Correct password but the account isn't active (suspended/banned): same 401, so account
            // state isn't leaked. Mirrors the old flow where GetUserByEmailAsync's status=="active"
            // filter returned null at this point.
            if (user.Status != "active")
                return MyResult<LoginResponse>.Failure(ErrorType.Unauthorized, "invalid credentials");


            RefreshTokenService refreshTokenService = new RefreshTokenService(context);
            var NewRefreshToken = await refreshTokenService.AddNewRefreshTokenFirstTime(user.UserId, deviceInfo, ipAddress);

            await new LoginLogService(context).LogAsync(user.UserId, "success", ipAddress, deviceInfo);

            return MyResult<LoginResponse>.Success(new LoginResponse
            {
                Id = user.UserId,
                Username = user.Username,
                Email = user.Email,
                Role = user.Role,
                Status = user.Status,
				IsRefreshTokenRevoked = false,
                RefreshToken = NewRefreshToken.Value?.RefreshToken,
                RefreshTokenExpiresAt = NewRefreshToken.Value?.ExpiresAt,
                Profile = new UserProfileResponse
                (user.Profile?.DisplayName, user.Profile?.Bio, user.Profile?.ImageUrl)


            });

        }
	}
}
