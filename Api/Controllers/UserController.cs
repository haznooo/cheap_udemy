using Business.Dto.Request;
using Business.Dto.Rsponse;
using Business.Interfaces;
using Business.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/user")]
    [Authorize]
    public class UserController(IUserService userService, ILogger<UserController> logger, IMediaService mediaService) : ApiControllerBase
    {
        // 3 MB limit for avatars (matches the avatar bucket's own cap); images only.
        private const long MaxAvatarSize = 3 * 1024 * 1024;
        private readonly string[] _allowedImageExtensions = { ".jpg", ".jpeg", ".png" };
        // The avatar bucket itself only accepts these MIME types; reject mismatches
        // here so they fail with a clean 400 instead of a storage error (500).
        private readonly string[] _allowedImageContentTypes = { "image/jpeg", "image/png" };

        // CallerId + MapFailure (MyResult → ProblemDetails) come from ApiControllerBase.

        // ---- Self endpoints (identity from the access token) ----

        [HttpGet("me/profile")]
        public async Task<ActionResult<UserProfileResponse>> GetMyProfile()
        {
         
            if (CallerId is not int callerId) return MissingIdentity();

            var result = await userService.GetUserProfile(callerId);
            return result.IsSuccess ? Ok(result.Value) : MapFailure(result);
        }

        // Upload + attach an avatar to the caller's own profile.
        [HttpPost("me/avatar")]
        public async Task<ActionResult<AvatarUploadResponse>> SetMyAvatar(IFormFile file)
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
            if (!_allowedImageContentTypes.Contains(file.ContentType?.ToLowerInvariant()))
            {
                return Problem(statusCode: StatusCodes.Status400BadRequest, detail: "Invalid file type. Only JPG and PNG are allowed.");
            }

            var fileName = await mediaService.UploadAvatarAsync(file);

            var result = await userService.SetAvatar(callerId, fileName);
            if (!result.IsSuccess)
            {
                // Persisting failed (e.g. the account is no longer active); the file we
                // just uploaded is now orphaned — remove it best-effort.
                await mediaService.DeleteAvatarAsync(fileName);
                return MapFailure(result);
            }

            // The new name is persisted; the replaced file is now orphaned in the
            // bucket, so remove it (best-effort — a leftover file is harmless).
            if (!string.IsNullOrEmpty(result.Value))
            {
                await mediaService.DeleteAvatarAsync(result.Value);
            }

            return Ok(new AvatarUploadResponse(fileName));
        }

        [HttpDelete("me/avatar")]
        public async Task<ActionResult> DeleteMyAvatar()
        {
            if (CallerId is not int callerId) return MissingIdentity();

            var result = await userService.RemoveAvatar(callerId);
            if (!result.IsSuccess) return MapFailure(result);

            // Avatar cleared in the DB; remove the now-orphaned file (best-effort).
            if (!string.IsNullOrEmpty(result.Value))
            {
                await mediaService.DeleteAvatarAsync(result.Value);
            }

            return NoContent();
        }

        [HttpPost("me/password")]
        public async Task<ActionResult<bool>> UpdateMyPassword([FromBody] UpdatePasswordRequest request)
        {
            if (CallerId is not int callerId) return MissingIdentity();

            var result = await userService.UpdatePassword(callerId, request);
            return result.IsSuccess ? Ok(result.Value) : MapFailure(result);
        }

        [HttpPost("me/profile/add")]
        public async Task<ActionResult<UserProfileResponse>> AddMyProfile([FromBody] UserProfileRequest ProfileRequest)
        {
            if (CallerId is not int callerId) return MissingIdentity();

            var result = await userService.AddUserProfile(callerId, ProfileRequest);
            return result.IsSuccess ? Ok(result.Value) : MapFailure(result);
        }

        [HttpPut("me/profile/update")]
        public async Task<ActionResult<UserProfileResponse>> UpdateMyProfile([FromBody] UserProfileRequest ProfileRequest)
        {
            if (CallerId is not int callerId) return MissingIdentity();

            var result = await userService.UpdateUserProfile(callerId, ProfileRequest);
            return result.IsSuccess ? Ok(result.Value) : MapFailure(result);
        }

        // Self-deletion (anonymization). Not an admin action, so it is never audited.
        [HttpPost("me/delete")]
        public async Task<ActionResult<bool>> DeleteMyAccount([FromBody] DeleteUserRequest request)
        {
            if (CallerId is not int callerId) return MissingIdentity();

            var result = await userService.DeleteUser(callerId, request);
            if (!result.IsSuccess) return MapFailure(result);

            // Account is gone; remove its now-orphaned avatar file (best-effort).
            if (!string.IsNullOrEmpty(result.Value))
            {
                await mediaService.DeleteAvatarAsync(result.Value);
            }

            return Ok(true);
        }

        // Admin-only cross-user endpoints now live in AdminController (api/admin/users).
    }

}
