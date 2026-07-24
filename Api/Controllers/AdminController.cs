using Business.Dto.Request;
using Business.Interfaces;
using Business.Services; // IMediaService lives in this namespace
using DataAccess.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static DataAccess.Common.clsPageResult;

namespace Api.Controllers
{
    // Admin-only cross-user actions. The authorization boundary is the whole
    // controller: one class-level role gate instead of per-action attributes.
    // The audit rows are written inside AdminService, next to each mutation;
    // this controller only adds the security-event ILogger lines.
    // CallerId + MapFailure come from ApiControllerBase.
    [ApiController]
    [Route("api/admin")]
    [Authorize(Roles = "admin")]
    public class AdminController(IAdminService adminService, IAdminActionService adminActionService, ILogger<AdminController> logger, IMediaService mediaService) : ApiControllerBase
    {
        // Read the audit log (newest first, paged). Rows are immutable at the DB level.
        [HttpGet("actions")]
        public async Task<ActionResult<PageResult<AdminActionDto>>> GetAdminActions(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            var result = await adminActionService.GetAdminActions(pageNumber, pageSize);
            return result.IsSuccess ? Ok(result.Value) : MapFailure(result);
        }

        // List all users (newest first, paged) for the admin user-management view.
        // Slim rows (incl. display name + avatar) — full account detail is on
        // GET users/{userId}. Optional status filter (active/banned/suspended/deleted);
        // omit for all.
        [HttpGet("users")]
        public async Task<ActionResult<PageResult<UserListItemDto>>> GetUsers(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? status = null)
        {
            var result = await adminService.GetUsers(pageNumber, pageSize, status);
            return result.IsSuccess ? Ok(result.Value) : MapFailure(result);
        }

        // Read any user's account + profile (Profile is null if they never created
        // one). Shows banned/suspended accounts; deleted (anonymized) → 404.
        [HttpGet("users/{userId:int}")]
        public async Task<ActionResult<UserAndProfileDto>> GetUser(int userId)
        {
            var result = await adminService.GetUser(userId);
            return result.IsSuccess ? Ok(result.Value) : MapFailure(result);
        }

        // Delete (anonymize) any user. No password — authority is the admin role +
        // the audit-log entry. Always audited (unlike self-delete).
        [HttpDelete("users/{userId:int}")]
        public async Task<ActionResult> DeleteUser(int userId)
        {
            // Identity guard BEFORE the destructive call: the audit row needs the
            // admin id, so no id must mean no delete.
            if (CallerId is not int adminId) return MissingIdentity();

            var result = await adminService.DeleteUser(adminId, userId);
            if (!result.IsSuccess) return MapFailure(result);

            // Account is gone; remove its now-orphaned avatar file (best-effort).
            if (!string.IsNullOrEmpty(result.Value))
            {
                await mediaService.DeleteAvatarAsync(result.Value);
            }

            logger.LogInformation("Admin {AdminId} deleted user {TargetId}", adminId, userId);

            return NoContent();
        }

        // Ban a user: blocks login, revokes their refresh tokens (access tokens die
        // within their 20-min lifetime — same accepted window as delete). Reversible
        // via unban, unlike delete which anonymizes.
        [HttpPost("users/{userId:int}/ban")]
        public async Task<ActionResult> BanUser(int userId)
            => await SetUserStatus(userId, "banned");

        // Suspend a user: same enforcement as ban (login blocked, sessions revoked),
        // just a softer label for a temporary measure.
        [HttpPost("users/{userId:int}/suspend")]
        public async Task<ActionResult> SuspendUser(int userId)
            => await SetUserStatus(userId, "suspended");

        // Reactivate a banned or suspended user. Audited (by AdminService) as
        // 'unban' or 'unsuspend' depending on which state the account was in.
        [HttpPost("users/{userId:int}/unban")]
        public async Task<ActionResult> UnbanUser(int userId)
            => await SetUserStatus(userId, "active");

        // Suspend a course: hides it from everyone including enrolled students
        // (unlike an instructor unpublish). The instructor cannot re-publish while
        // suspended. Audited (by AdminService) as 'suspend' on target_table 'courses'.
        [HttpPost("courses/{courseId:int}/suspend")]
        public async Task<ActionResult> SuspendCourse(int courseId)
            => await SetCourseSuspension(courseId, suspend: true);

        // Lift a suspension: the course goes straight back to published.
        [HttpPost("courses/{courseId:int}/unsuspend")]
        public async Task<ActionResult> UnsuspendCourse(int courseId)
            => await SetCourseSuspension(courseId, suspend: false);

        // Course takedown: PERMANENTLY purges the course's content (sections, lessons,
        // content_blocks, reviews) and its bucket media, then tombstones the course row.
        // Irreversible — unlike suspend. Payment/enrollment records are deliberately kept.
        // Audited (by AdminService) as 'delete' on target_table 'courses'. Optional body
        // carries a removal reason.
        [HttpPost("courses/{courseId:int}/takedown")]
        public async Task<ActionResult> TakedownCourse(int courseId, [FromBody] DeleteCourseRequest? request)
        {
            if (CallerId is not int adminId) return MissingIdentity();

            var result = await adminService.TakedownCourse(adminId, courseId, request?.RemovalReason);
            if (!result.IsSuccess) return MapFailure(result);

            logger.LogInformation("Admin {AdminId} took down course {CourseId}", adminId, courseId);

            return NoContent();
        }

        // Shared flow for the two course-suspension endpoints, mirroring SetUserStatus.
        private async Task<ActionResult> SetCourseSuspension(int courseId, bool suspend)
        {
            if (CallerId is not int adminId) return MissingIdentity();

            var result = await adminService.SetCourseSuspension(adminId, courseId, suspend);
            if (!result.IsSuccess) return MapFailure(result);

            logger.LogInformation("Admin {AdminId} {Action} course {CourseId}", adminId, suspend ? "suspended" : "unsuspended", courseId);

            return NoContent();
        }

        // Shared flow for the three status endpoints: service call (which audits), log, 204.
        private async Task<ActionResult> SetUserStatus(int userId, string newStatus)
        {
            if (CallerId is not int adminId) return MissingIdentity();

            var result = await adminService.SetUserStatus(adminId, userId, newStatus);
            if (!result.IsSuccess) return MapFailure(result);

            logger.LogInformation("Admin {AdminId} set user {TargetId} status to {Status}", adminId, userId, newStatus);

            return NoContent();
        }
    }
}
