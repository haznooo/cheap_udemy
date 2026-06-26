
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
    public class AuthenticationController(AppDbContext context, IConfiguration configuration, ILogger<AuthenticationController> logger) : ControllerBase
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
                        ErrorType.Unauthorized => Unauthorized(result.Errors),
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
                // Security event only — never log the submitted password or credentials.
                logger.LogWarning("Failed login attempt from IP {Ip}", ipAddress);

                // Handle all failure types in one switch expression
                return result.FailureType switch
                {
                    ErrorType.NotFound => NotFound(result.Errors),
                    ErrorType.BadRequest => BadRequest(result.Errors),
                    ErrorType.Conflict => Conflict(result.Errors),
                    ErrorType.Unauthorized => Unauthorized(result.Errors),
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

        // Exchanges a valid refresh token for a new access token (and a rotated refresh token).
        // Body carries UserId + RefreshToken; device/IP are taken from the request, not the client.
        [HttpPost("refresh")]
        public async Task<ActionResult<LoginResponse>> Refresh([FromBody] RefreshTokenRequest request)
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            string userAgent = Request.Headers.UserAgent.ToString();

            if (string.IsNullOrWhiteSpace(userAgent))
                userAgent = "unknown";
            if (string.IsNullOrWhiteSpace(ipAddress))
                ipAddress = "unknown";

            TokenService tokenService = new TokenService(context);

            var result = await tokenService.RefreshAccessToken(new RefreshTokenRequest
            (
                RefreshToken: request.RefreshToken,
                deviceInfo: userAgent,
                IpAddress: ipAddress,
                UserId: request.UserId
            ));

            if (!result.IsSuccess)
            {
                logger.LogWarning("Failed refresh-token attempt from IP {Ip}", ipAddress);

                return result.FailureType switch
                {
                    ErrorType.NotFound => NotFound(result.Errors),
                    ErrorType.BadRequest => BadRequest(result.Errors),
                    ErrorType.Conflict => Conflict(result.Errors),
                    ErrorType.Unauthorized => Unauthorized(result.Errors),
                    _ => StatusCode(500, "An unexpected error occurred")
                };
            }

            result.Value.Token = GenerateAccessToken(result.Value);

            return Ok(result.Value);
        }

        // Revokes the presented refresh token. Always returns 200 so it never reveals
        // whether the token (or user) actually existed.
        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request)
        {
            TokenService tokenService = new TokenService(context);

            await tokenService.RevokeRefreshToken(new RefreshTokenRequest
            (
                RefreshToken: request.RefreshToken,
                deviceInfo: "unknown",
                IpAddress: "unknown",
                UserId: request.UserId
            ));

            return Ok("Logged out successfully");
        }

        // Mints a JWT access token from the user's identity claims (same settings as login/signup).
        private string GenerateAccessToken(LoginResponse user)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role)
            };

            var SecretKey = configuration["JWT_SECRET_KEY"];

            if (string.IsNullOrWhiteSpace(SecretKey))
            {
                throw new Exception("JWT secret key is not configured in environment variables");
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SecretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: "CheapUdemyApi",
                audience: "CheapUdemyApiUsers",
                claims: claims,
                expires: DateTime.Now.AddMinutes(20),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
