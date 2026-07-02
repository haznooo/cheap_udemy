
using Api.Controllers;
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
    [AllowAnonymous] // signUp/login/refresh/logout must stay public under the authenticated-by-default fallback policy
    public class AuthenticationController(AppDbContext context, IConfiguration configuration, ILogger<AuthenticationController> logger) : ApiControllerBase
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

            AuthenticationService authenticationService = new AuthenticationService(context);

            //refresh token is generated in the service layer and stored securely (hashed + expiry + not revoked)
            var result = await authenticationService.UserSignUp(request, userAgent, ipAddress);

      
            // 2. Check the Success flag of the Result pattern

            // Credential failures here really are 401s, unlike the ownership 403s elsewhere.
            if (!result.IsSuccess)
                return MapFailure(result, StatusCodes.Status401Unauthorized);

            result.Value.AccessToken = GenerateAccessToken(result.Value);

            return result.Value;
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


            AuthenticationService authenticationService = new AuthenticationService(context);

            var result = await authenticationService.LoginUser(request,userAgent,ipAddress);

            if (!result.IsSuccess)
            {
                // Security event only — never log the submitted password or credentials.
                logger.LogWarning("Failed login attempt from IP {Ip}", ipAddress);

                return MapFailure(result, StatusCodes.Status401Unauthorized);
            }


            result.Value.AccessToken = GenerateAccessToken(result.Value);
            // Store refresh token securely (hash + expiry + not revoked)

            return result.Value;
        }

        // Exchanges a valid refresh token for a new access token (and a rotated refresh token).
        // Body carries RefreshToken + (possibly expired) AccessToken; the user id is recovered from
        // the access token's signed claims, never trusted from the client. Device/IP come from the request.
        [HttpPost("refresh")]
        public async Task<ActionResult<LoginResponse>> Refresh([FromBody] RefreshTokenRequest request)
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            string userAgent = Request.Headers.UserAgent.ToString();

            if (string.IsNullOrWhiteSpace(userAgent))
                userAgent = "unknown";
            if (string.IsNullOrWhiteSpace(ipAddress))
                ipAddress = "unknown";

            if (string.IsNullOrWhiteSpace(request.AccessToken) || string.IsNullOrWhiteSpace(request.RefreshToken))
                return Problem(statusCode: StatusCodes.Status400BadRequest, detail: "access token and refresh token are required");

            // Recover the user id from the (possibly expired) access token. Signature is verified;
            // only the lifetime check is skipped. A tampered/garbage token yields null → 401.
            var userId = GetUserIdFromExpiredToken(request.AccessToken);
            if (userId == null)
            {
                logger.LogWarning("Failed refresh-token attempt (invalid access token) from IP {Ip}", ipAddress);
                return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "invalid access token");
            }

            RefreshTokenService refreshTokenService = new RefreshTokenService(context);

            var result = await refreshTokenService.RefreshAccessToken(request.RefreshToken, userId.Value, userAgent, ipAddress);

            if (!result.IsSuccess)
            {
                logger.LogWarning("Failed refresh-token attempt from IP {Ip}", ipAddress);

                return MapFailure(result, StatusCodes.Status401Unauthorized);
            }

            result.Value.AccessToken = GenerateAccessToken(result.Value);

            return Ok(result.Value);
        }

        // Revokes the presented refresh token. Always returns 200 so it never reveals
        // whether the token (or user) actually existed. User id is recovered from the access token.
        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request)
        {
            var userId = string.IsNullOrWhiteSpace(request.AccessToken)
                ? null
                : GetUserIdFromExpiredToken(request.AccessToken);

            if (userId != null)
            {
                RefreshTokenService refreshTokenService = new RefreshTokenService(context);
                await refreshTokenService.RevokeRefreshToken(request.RefreshToken, userId.Value);
            }

            return Ok("Logged out successfully");
        }

        // Validates an access token's signature/issuer/audience but IGNORES expiry, then pulls the
        // user id (NameIdentifier) out of it. Returns null if the token is invalid in any other way.
        // This is the trustworthy source of the user id for /refresh and /logout — the client never
        // sends a raw user id anymore.
        private int? GetUserIdFromExpiredToken(string accessToken)
        {
            var SecretKey = configuration["JWT_SECRET_KEY"];

            if (string.IsNullOrWhiteSpace(SecretKey))
                throw new Exception("JWT secret key is not configured in environment variables");

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = "CheapUdemyApi",
                ValidateAudience = true,
                ValidAudience = "CheapUdemyApiUsers",
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SecretKey)),
                ValidateLifetime = false // the whole point: the access token is allowed to be expired
            };

            try
            {
                var principal = new JwtSecurityTokenHandler()
                    .ValidateToken(accessToken, validationParameters, out var validatedToken);

                // Guard against tokens signed with the wrong algorithm.
                if (validatedToken is not JwtSecurityToken jwt ||
                    !jwt.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
                    return null;

                var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                return int.TryParse(userIdClaim, out var userId) ? userId : null;
            }
            catch
            {
                return null;
            }
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
                expires: DateTime.UtcNow.AddMinutes(20),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
