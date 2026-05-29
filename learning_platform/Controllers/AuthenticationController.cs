
using Business.Common;
using Business.Dto.Request;
using Business.Dto.Rsponse;
using Business.Services;
using DataAccess.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;



namespace CheapUdemy.Controllers
{
    [ApiController]
    [Route("api/User")]
    public class AuthenticationController(AppDbContext context,IConfiguration configuration) : ControllerBase
    {

   
        //works fine
        [HttpPost("signUp")]
        public async Task<ActionResult<LoginResponse>> SignUp([FromBody] SignUpRequest request)
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

            string userAgent = Request.Headers.UserAgent.ToString();

            if (string.IsNullOrWhiteSpace(userAgent))
            {
                userAgent = "unknown";
            }
            if (string.IsNullOrWhiteSpace(ipAddress))
            {
                ipAddress = "unknown";
            }

            UserService userService = new UserService(context);

            //refresh token is generated in the service layer and stored securely (hashed + expiry + not revoked)
            var result = await userService.UserSignUp(request, userAgent, ipAddress);

      
            // 2. Check the Success flag of the Result pattern

            if (!result.IsSuccess)
                {
                    // Handle all failure types in one switch expression
                    return result.FailureType switch
                    {
                        ErrorType.NotFound => NotFound(result.Errors),
                        ErrorType.BadRequest => BadRequest(result.Errors),
                        ErrorType.Conflict => Conflict(result.Errors),
                        ErrorType.Unauthorized => Conflict(result.Errors),
                        _ => StatusCode(500, "An unexpected error occurred")
                    };
                }

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, result.Value.Id.ToString()),
                new Claim(ClaimTypes.Email, result.Value.Email),
                new Claim(ClaimTypes.Role, result.Value.Role)
            };

            var SecretKey
                = configuration["JWT_SECRET_KEY"];

            if (string.IsNullOrWhiteSpace(SecretKey))
            {
                throw new Exception("JWT secret key is not configured in environment variables");
            }


            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(SecretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                issuer: "CheapUdemyApi",
                audience: "CheapUdemyApiUsers",
                claims: claims,
                expires: DateTime.Now.AddMinutes(20),
                signingCredentials: creds
            );

            result.Value.Token = new JwtSecurityTokenHandler().WriteToken(token);


            var response = result.Value;
            return response;
            // Use CreatedAtAction or CreatedAtRoute for a 201 response
            // Assuming you have a GetUserById method defined elsewhere
          //  return CreatedAtAction("GetUserById", new { id = response.user_id }, response);
        }
       
        [HttpPost("login")] // Use route parameters for IDs
        public async Task<ActionResult<LoginResponse>> login([FromBody] LoginRequest request)
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

            string userAgent = Request.Headers.UserAgent.ToString();

            if (string.IsNullOrWhiteSpace(userAgent))
            {
                userAgent = "unknown";
            }
            if (string.IsNullOrWhiteSpace(ipAddress))
            {
                ipAddress = "unknown";
            }


            UserService userService = new UserService(context);

            var result = await userService.LoginUser(request,userAgent,ipAddress);

            if (!result.IsSuccess)
            {
                // Handle all failure types in one switch expression
                return result.FailureType switch
                {
                    ErrorType.NotFound => NotFound(result.Errors),
                    ErrorType.BadRequest => BadRequest(result.Errors),
                    ErrorType.Conflict => Conflict(result.Errors),
                    ErrorType.Unauthorized => Conflict(result.Errors),
                    _ => StatusCode(500, "An unexpected error occurred")
                };
            }


            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, result.Value.Id.ToString()),
                new Claim(ClaimTypes.Email, result.Value.Email),
                new Claim(ClaimTypes.Role, result.Value.Role)
            };

            var SecretKey = configuration["JWT_SECRET_KEY"];

            if (string.IsNullOrWhiteSpace(SecretKey))
            {
                throw new Exception("JWT secret key is not configured in environment variables");
            }


            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(SecretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: "CheapUdemyApi",
                audience: "CheapUdemyApiUsers",
                claims: claims,
                expires: DateTime.Now.AddMinutes(20),
                signingCredentials: creds
            );
            result.Value.Token = new JwtSecurityTokenHandler().WriteToken(token);
            // Store refresh token securely (hash + expiry + not revoked)

            return Ok(result);
        }

     

        /*    


            [HttpPost("refresh")]
            public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
            {
                var results = await sender.Send(new GetUserByEmailQuery(request.Email));


                if (results == null)
                    return Unauthorized("Invalid refresh request");

                if (results.Value.RefreshTokenRevokedAt != null)
                    return Unauthorized("Refresh token is revoked");

                if (results.Value.RefreshTokenExpiresAt == null || results.Value.RefreshTokenExpiresAt <= DateTime.UtcNow)
                    return Unauthorized("Refresh token expired");

                bool refreshValid = BCrypt.Net.BCrypt.Verify(request.RefreshToken, results.Value.RefreshToken);
                if (!refreshValid)
                    return Unauthorized("Invalid refresh token");

                // Issue NEW access token (same claims & signing settings as login)
                var claims = new[]
                {
            new Claim(ClaimTypes.NameIdentifier, results.Value.Id.ToString()),
            new Claim(ClaimTypes.Email, results.Value.Email),
            new Claim(ClaimTypes.Role, results.Value.Role)
        };

                var key = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(put the key here));

                var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

                var jwt = new JwtSecurityToken(
                    issuer: "StudentApi",
                    audience: "StudentApiUsers",
                    claims: claims,
                    expires: DateTime.UtcNow.AddMinutes(30),
                    signingCredentials: creds
                );

                var newAccessToken = new JwtSecurityTokenHandler().WriteToken(jwt);

                // Rotation: replace refresh token
                var newRefreshToken = GenerateRefreshToken();
                results.Value.RefreshToken = BCrypt.Net.BCrypt.HashPassword(newRefreshToken);
                results.Value.RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(7);
                results.Value.RefreshTokenRevokedAt = null;

                return Ok(new TokenResponse(newAccessToken, newRefreshToken));
            }

            */

        //works fine




        /*
        //not working
        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
        {
         
            var results = await sender.Send(new GetUserByEmailQuery(request.Email));

            if (results == null)
                return Ok(); // Do not reveal if user exists

            bool refreshValid = BCrypt.Net.BCrypt.Verify(request.RefreshToken, results.Value.RefreshToken);
            if (!refreshValid)
                return Ok();

            results.Value.RefreshTokenRevokedAt = DateTime.UtcNow;
            return Ok("Logged out successfully");
        }

        */
    }
}
