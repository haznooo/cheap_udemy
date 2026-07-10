using Business.Dto.Rsponse;
using Business.Interfaces;
using Business.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    // Admin-only cross-user actions. The authorization boundary is the whole
    // controller: one class-level role gate instead of per-action attributes.
    // CallerId + MapFailure come from ApiControllerBase.
    [ApiController]
    [Route("api/admin")]
    [Authorize(Roles = "admin")]
    public class AdminController(IUserService userService, IAdminActionService adminActionService, ILogger<AdminController> logger, IMediaService mediaService) : ApiControllerBase
    {
        // Read any user's profile.
        [HttpGet("users/{userId:int}")]
        public async Task<ActionResult<UserProfileResponse>> GetUser(int userId)
        {
            var result = await userService.GetUserProfile(userId);
            return result.IsSuccess ? Ok(result.Value) : MapFailure(result);
        }

        // Delete (anonymize) any user. No password — authority is the admin role +
        // the audit-log entry. Always audited (unlike self-delete).
        [HttpDelete("users/{userId:int}")]
        public async Task<ActionResult> DeleteUser(int userId)
        {
            var result = await userService.AdminDeleteUser(userId);
            if (!result.IsSuccess) return MapFailure(result);

            // Account is gone; remove its now-orphaned avatar file (best-effort).
            if (!string.IsNullOrEmpty(result.Value))
            {
                await mediaService.DeleteAvatarAsync(result.Value);
            }

            if (CallerId is int adminId)
            {
                await adminActionService.LogAsync(
                    adminId,
                    actionType: "delete",
                    targetTable: "users",
                    targetId: userId,
                    oldValue: new { user_id = userId });

                logger.LogInformation("Admin {AdminId} deleted user {TargetId}", adminId, userId);
            }

            return NoContent();
        }
    }
}
