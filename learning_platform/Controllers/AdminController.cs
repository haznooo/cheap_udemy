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

        // Ban a user: blocks login, revokes their refresh tokens (access tokens die
        // within their 20-min lifetime — same accepted window as delete). Reversible
        // via unban, unlike delete which anonymizes.
        [HttpPost("users/{userId:int}/ban")]
        public async Task<ActionResult> BanUser(int userId)
            => await SetUserStatus(userId, "banned", auditActionType: "ban");

        // Suspend a user: same enforcement as ban (login blocked, sessions revoked),
        // just a softer label for a temporary measure.
        [HttpPost("users/{userId:int}/suspend")]
        public async Task<ActionResult> SuspendUser(int userId)
            => await SetUserStatus(userId, "suspended", auditActionType: "suspend");

        // Reactivate a banned or suspended user. Audited as 'unban' or 'unsuspend'
        // depending on which state the account was actually in.
        [HttpPost("users/{userId:int}/unban")]
        public async Task<ActionResult> UnbanUser(int userId)
            => await SetUserStatus(userId, "active", auditActionType: null);

        // Shared flow for the three status endpoints: service call, audit row, log, 204.
        // auditActionType == null means "derive from the old status" (the unban path).
        private async Task<ActionResult> SetUserStatus(int userId, string newStatus, string? auditActionType)
        {
            if (CallerId is not int adminId) return MissingIdentity();

            var result = await userService.AdminSetUserStatus(adminId, userId, newStatus);
            if (!result.IsSuccess) return MapFailure(result);

            string oldStatus = result.Value!;
            string actionType = auditActionType ?? (oldStatus == "suspended" ? "unsuspend" : "unban");

            await adminActionService.LogAsync(
                adminId,
                actionType: actionType,
                targetTable: "users",
                targetId: userId,
                oldValue: new { status = oldStatus },
                newValue: new { status = newStatus });

            logger.LogInformation("Admin {AdminId} set user {TargetId} status to {Status}", adminId, userId, newStatus);

            return NoContent();
        }
    }
}
