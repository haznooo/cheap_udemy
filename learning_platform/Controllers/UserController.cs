using System.Security.Claims;
using Business.Common;
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
    public class UserController(AppDbContext context, ILogger<UserController> logger, IMediaService mediaService) : ControllerBase
    {
        // 5 MB limit for avatars; images only.
        private const long MaxAvatarSize = 5 * 1024 * 1024;
        private readonly string[] _allowedImageExtensions = { ".jpg", ".jpeg", ".png" };

        [HttpGet("{userId}")]
        public async Task<ActionResult<UserProfileResponse>> GetUserProfile(int userId, [FromServices] IAuthorizationService authorizationService)
        {
            var authResult = await authorizationService.AuthorizeAsync(User, userId, "UserOwnerOrAdmin");
            if (!authResult.Succeeded)
            {
                logger.LogWarning("Forbidden access: user {CallerId} attempted GetUserProfile on user {TargetId}",
                    User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown", userId);
                return Forbid();
            }

            UserService userService = new UserService(context);
            var result = await userService.GetUserProfile(userId);

            if (!result.IsSuccess)
            {
                return result.FailureType switch
                {
                    ErrorType.NotFound => NotFound(result.Errors),
                    ErrorType.BadRequest => BadRequest(result.Errors),
                    ErrorType.Conflict => Conflict(result.Errors),
                    ErrorType.Unauthorized => Unauthorized(result.Errors),
                    _ => StatusCode(500, "An unexpected error occurred")
                };
            }

            return Ok(result.Value);
        }

        // Upload + attach an avatar to a user's profile. Owner or admin only.
        [HttpPost("{userId}/avatar")]
        public async Task<ActionResult> SetAvatar(int userId, IFormFile file, [FromServices] IAuthorizationService authorizationService)
        {
            var authResult = await authorizationService.AuthorizeAsync(User, userId, "UserOwnerOrAdmin");
            if (!authResult.Succeeded)
            {
                logger.LogWarning("Forbidden access: user {CallerId} attempted SetAvatar on user {TargetId}",
                    User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown", userId);
                return Forbid();
            }

            if (file == null || file.Length == 0)
            {
                return BadRequest("No file was uploaded.");
            }
            if (file.Length > MaxAvatarSize)
            {
                return BadRequest($"File exceeds the maximum limit of {MaxAvatarSize / (1024 * 1024)}MB.");
            }
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(extension) || !_allowedImageExtensions.Contains(extension))
            {
                return BadRequest("Invalid file type. Only JPG and PNG are allowed.");
            }

            var fileName = await mediaService.UploadFileAsync(file);

            UserService userService = new UserService(context);
            var result = await userService.SetAvatar(userId, fileName);

            if (!result.IsSuccess)
            {
                return result.FailureType switch
                {
                    ErrorType.NotFound => NotFound(result.Errors),
                    ErrorType.BadRequest => BadRequest(result.Errors),
                    ErrorType.Conflict => Conflict(result.Errors),
                    ErrorType.Unauthorized => Unauthorized(result.Errors),
                    _ => StatusCode(500, "An unexpected error occurred")
                };
            }

            return Ok(new { avatar = fileName });
        }


        [HttpPost("Delete/{userId}")] // Use route parameters for IDs
        public async Task<ActionResult<bool>> DeleteUser(int userId, [FromBody] DeleteUserRequest request, [FromServices] IAuthorizationService authorizationService)
        {
          
            var authResult = await authorizationService.AuthorizeAsync(User, userId, "UserOwnerOrAdmin");
            if (!authResult.Succeeded)
            {
                logger.LogWarning("Forbidden access: user {CallerId} attempted DeleteUser on user {TargetId}",
                    User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown", userId);
                return Forbid();
            }


            UserService userService = new UserService(context);

            var result = await userService.DeleteUser(userId, request);

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

            // Audit: an admin deleting another user's account is an admin action.
            // (Self-deletion is not an admin action, so it is not recorded here.)
            if (User.IsInRole("admin")
                && int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int adminId)
                && adminId != userId)
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

        [HttpPost("UpdatePassword/{userId}")] // Use route parameters for IDs
        public async Task<ActionResult<bool>> UpdatePassword(int userId, [FromBody] UpdatePasswordRequest request, [FromServices] IAuthorizationService authorizationService)
        {

            var authResult = await authorizationService.AuthorizeAsync(User, userId, "UserOwnerOrAdmin");
            if (!authResult.Succeeded)
            {
                logger.LogWarning("Forbidden access: user {CallerId} attempted UpdatePassword on user {TargetId}",
                    User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown", userId);
                return Forbid();
            }


            UserService userService = new UserService(context);

            var result = await userService.UpdatePassword(userId, request);

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

            return Ok(result.Value);
        }

        //works fine
        [HttpPut("UpdateProfile/{userId}")]
        public async Task<ActionResult<UserProfileResponse>> UpdateUserProfile(int userId, [FromBody] UserProfileRequest ProfileRequest, [FromServices] IAuthorizationService authorizationService)
        {

            var authResult = await authorizationService.AuthorizeAsync(User, userId, "UserOwnerOrAdmin");
            if (!authResult.Succeeded)
            {
                logger.LogWarning("Forbidden access: user {CallerId} attempted UpdateUserProfile on user {TargetId}",
                    User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown", userId);
                return Forbid();
            }

            UserService userService = new UserService(context);
            var result = await userService.UpdateUserProfile(userId, ProfileRequest);

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
            return result.Value;
        }
    }

}
