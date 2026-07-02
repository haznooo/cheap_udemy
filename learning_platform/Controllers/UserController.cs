using System.Security.Claims;
using Business.Dto.Request;
using Business.Dto.Rsponse;
using Business.Services;
using DataAccess.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/user")]
    [Authorize]
    public class UserController(AppDbContext context, ILogger<UserController> logger, IMediaService mediaService) : ApiControllerBase
    {
        // 5 MB limit for avatars; images only.
        private const long MaxAvatarSize = 5 * 1024 * 1024;
        private readonly string[] _allowedImageExtensions = { ".jpg", ".jpeg", ".png" };

        // CallerId + MapFailure (MyResult → ProblemDetails) come from ApiControllerBase.

        // ---- Self endpoints (identity from the access token) ----

        [HttpGet("me/profile")]
        public async Task<ActionResult<UserProfileResponse>> GetMyProfile()
        {
            if (CallerId is not int callerId) return MissingIdentity();

            var result = await new UserService(context).GetUserProfile(callerId);
            return result.IsSuccess ? Ok(result.Value) : MapFailure(result);
        }

        // Upload + attach an avatar to the caller's own profile.
        [HttpPost("me/avatar")]
        public async Task<ActionResult> SetMyAvatar(IFormFile file)
        {
            if (CallerId is not int callerId) return MissingIdentity();

            if (file == null || file.Length == 0)
            {
                return Problem(statusCode: StatusCodes.Status400BadRequest, detail: "No file was uploaded.");
            }
            if (file.Length > MaxAvatarSize)
            {
                return Problem(statusCode: StatusCodes.Status400BadRequest, detail: $"File exceeds the maximum limit of {MaxAvatarSize / (1024 * 1024)}MB.");
            }
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(extension) || !_allowedImageExtensions.Contains(extension))
            {
                return Problem(statusCode: StatusCodes.Status400BadRequest, detail: "Invalid file type. Only JPG and PNG are allowed.");
            }

            var fileName = await mediaService.UploadFileAsync(file);

            var result = await new UserService(context).SetAvatar(callerId, fileName);
            return result.IsSuccess ? Ok(new { avatar = fileName }) : MapFailure(result);
        }

        [HttpPost("me/password")]
        public async Task<ActionResult<bool>> UpdateMyPassword([FromBody] UpdatePasswordRequest request)
        {
            if (CallerId is not int callerId) return MissingIdentity();

            var result = await new UserService(context).UpdatePassword(callerId, request);
            return result.IsSuccess ? Ok(result.Value) : MapFailure(result);
        }

        [HttpPost("me/profile")]
        public async Task<ActionResult<UserProfileResponse>> AddMyProfile([FromBody] UserProfileRequest ProfileRequest)
        {
            if (CallerId is not int callerId) return MissingIdentity();

            var result = await new UserService(context).AddUserProfile(callerId, ProfileRequest);
            return result.IsSuccess ? Ok(result.Value) : MapFailure(result);
        }

        [HttpPut("me/profile")]
        public async Task<ActionResult<UserProfileResponse>> UpdateMyProfile([FromBody] UserProfileRequest ProfileRequest)
        {
            if (CallerId is not int callerId) return MissingIdentity();

            var result = await new UserService(context).UpdateUserProfile(callerId, ProfileRequest);
            return result.IsSuccess ? Ok(result.Value) : MapFailure(result);
        }

        // Self-deletion (anonymization). Not an admin action, so it is never audited.
        [HttpPost("me/delete")]
        public async Task<ActionResult<bool>> DeleteMyAccount([FromBody] DeleteUserRequest request)
        {
            if (CallerId is not int callerId) return MissingIdentity();

            var result = await new UserService(context).DeleteUser(callerId, request);
            return result.IsSuccess ? Ok(result.Value) : MapFailure(result);
        }

        // ---- Admin-only cross-user endpoints (id from the URL) ----

        [HttpGet("{userId:int}")]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult<UserProfileResponse>> GetUserProfile(int userId)
        {
            var result = await new UserService(context).GetUserProfile(userId);
            return result.IsSuccess ? Ok(result.Value) : MapFailure(result);
        }

        // An admin deleting another user's account — always audited.
        [HttpPost("{userId:int}/delete")]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult<bool>> DeleteUser(int userId, [FromBody] DeleteUserRequest request)
        {
            var result = await new UserService(context).DeleteUser(userId, request);
            if (!result.IsSuccess) return MapFailure(result);

            if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int adminId))
            {
                await new AdminActionService(context).LogAsync(
                    adminId,
                    actionType: "delete",
                    targetTable: "users",
                    targetId: userId,
                    oldValue: new { user_id = userId });

                logger.LogInformation("Admin {AdminId} deleted user {TargetId}", adminId, userId);
            }

            return Ok(result.Value);
        }
    }

}
