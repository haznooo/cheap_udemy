using Business.Common;
using Business.Interfaces;
using DataAccess.Dto;
using DataAccess.Interfaces;
using static DataAccess.Common.clsPageResult;

namespace Business.Services
{
    // Admin-only cross-user actions (the api/admin use cases). Sits on the same
    // user repository as UserService/AuthenticationService — repos stay per-table,
    // services per-use-case. The audit row is written HERE, right next to the
    // mutation, so no caller can perform an admin action and skip the audit.
    public class AdminService(IUserAndProfileRepository userRepository, IRefreshTokenService refreshTokenService, IAdminActionService adminActionService, ICoursesRepository coursesRepository, IReviewRepository reviewRepository, IMediaService mediaService) : IAdminService
    {
        // The account states an admin can filter by (matches the users.status CHECK).
        private static readonly HashSet<string> ValidUserStatuses = new() { "active", "banned", "suspended", "deleted" };

        // Paged list of accounts (newest-first) for the admin user-management view. Slim
        // rows (id/username/email/role/status/create_date + display name/avatar) — includes
        // deleted (anonymized) accounts, since those still occupy a row; full detail per
        // user is on GetUser below. Optional status filter; null/empty = all statuses.
        public async Task<MyResult<PageResult<UserListItemDto>>> GetUsers(int pageNumber, int pageSize, string? status = null)
        {
            if (pageNumber <= 0 || pageSize <= 0)
                return MyResult<PageResult<UserListItemDto>>.Failure(ErrorType.BadRequest, "Invalid page number or page size.");

            if (!string.IsNullOrWhiteSpace(status) && !ValidUserStatuses.Contains(status))
                return MyResult<PageResult<UserListItemDto>>.Failure(ErrorType.BadRequest, "Invalid status filter.");

            var users = await userRepository.GetUsersAsync(pageNumber, pageSize, status);

            if (users == null)
                return MyResult<PageResult<UserListItemDto>>.Failure(ErrorType.Failure, "Failed to retrieve users.");

            return MyResult<PageResult<UserListItemDto>>.Success(users);
        }

        // Account + optional profile view of any user. Unlike the self read
        // (UserService.GetUserProfile), this works for accounts with no profile row
        // and shows banned/suspended accounts; deleted (anonymized) stay a 404.
        public async Task<MyResult<UserAndProfileDto>> GetUser(int userId)
        {
            if (userId <= 0) return MyResult<UserAndProfileDto>.Failure(ErrorType.BadRequest, "user id can not be zero or negative");

            var user = await userRepository.GetUserWithProfileForAdminAsync(userId);

            if (user == null || user.Status == "deleted")
                return MyResult<UserAndProfileDto>.Failure(ErrorType.NotFound, "user not found");

            return MyResult<UserAndProfileDto>.Success(user);
        }

        // Admin-initiated delete. Authority is the caller's admin role + the audit-log
        // entry — NOT the target's credentials (an admin never knows another user's
        // password), so there is NO password confirmation. On success the value is the
        // target's avatar file name (captured before the anonymize trigger wipes the
        // profile row) for best-effort storage cleanup by the caller.
        public async Task<MyResult<string?>> DeleteUser(int adminId, int targetUserId)
        {
            if (targetUserId <= 0) return MyResult<string?>.Failure(ErrorType.BadRequest, "user id can not be zero or negative");

            // Reject a missing / already-deleted target with a clean 404 instead of
            // silently "succeeding" on a no-op anonymize.
            if (!await userRepository.DoesUserExistByIdAsync(targetUserId))
                return MyResult<string?>.Failure(ErrorType.NotFound, "user not found");

            // Capture the avatar name BEFORE the delete wipes the profile row.
            string? avatarFileName = (await userRepository.GetUserProfileByIdAsync(targetUserId))?.ImageUrl;

            var result = await userRepository.DeleteUserAsync_Anonymize(targetUserId);

            if (!result) return MyResult<string?>.Failure(ErrorType.Failure, "failed to delete user");

            await adminActionService.LogAsync(
                adminId,
                actionType: "delete",
                targetTable: "users",
                targetId: targetUserId,
                oldValue: new { user_id = targetUserId });

            return MyResult<string?>.Success(avatarFileName);
        }

        // Sets users.status to "banned", "suspended" or "active" and writes the
        // audit row for the transition.
        public async Task<MyResult<bool>> SetUserStatus(int adminId, int targetUserId, string newStatus)
        {
            if (targetUserId <= 0) return MyResult<bool>.Failure(ErrorType.BadRequest, "user id can not be zero or negative");

            // An admin locking themselves out (or "unbanning" themselves) makes no sense.
            if (adminId == targetUserId) return MyResult<bool>.Failure(ErrorType.BadRequest, "you cannot change your own account status");

            var target = await userRepository.GetUserStatusAndRoleAsync(targetUserId);

            // Deleted accounts are anonymized and can't come back — treat like missing.
            if (target == null || target.Status == "deleted")
                return MyResult<bool>.Failure(ErrorType.NotFound, "user not found");

            // Admins can't ban/suspend each other — demote via the DB first if ever needed.
            if (target.Role == "admin")
                return MyResult<bool>.Failure(ErrorType.BadRequest, "cannot change the status of an admin account");

            if (target.Status == newStatus)
                return MyResult<bool>.Failure(ErrorType.Conflict, $"user is already {newStatus}");

            var updated = await userRepository.UpdateUserStatusAsync(targetUserId, newStatus);

            if (!updated) return MyResult<bool>.Failure(ErrorType.Failure, "failed to update user status");

            // Ban/suspend must kill existing sessions, same as a password change: the
            // refresh chain dies now, access tokens die within their 20-min lifetime
            // (same accepted stale-token window as account deletion). /refresh is
            // already closed for non-active users (active-only user fetch).
            if (newStatus != "active")
            {
                await refreshTokenService.RevokeAllForUser(targetUserId);
            }

            // Audit type derives from the transition: ban/suspend name the new state,
            // reactivation names the state it undoes (unban vs unsuspend).
            string actionType = newStatus switch
            {
                "banned" => "ban",
                "suspended" => "suspend",
                _ => target.Status == "suspended" ? "unsuspend" : "unban",
            };

            await adminActionService.LogAsync(
                adminId,
                actionType: actionType,
                targetTable: "users",
                targetId: targetUserId,
                oldValue: new { status = target.Status },
                newValue: new { status = newStatus });

            return MyResult<bool>.Success(true);
        }

        // Course moderation: suspend hides a course from EVERYONE (even enrolled
        // students — unlike an instructor unpublish, which preserves their access);
        // unsuspend puts it back to published. Only a published course can be
        // suspended (a draft/retired one is invisible anyway), so unsuspend always
        // restores exactly "published" — no need to remember a previous status.
        // The instructor can't lift it: CourseService.PublishCourse rejects suspended.
        public async Task<MyResult<bool>> SetCourseSuspension(int adminId, int courseId, bool suspend)
        {
            if (courseId <= 0) return MyResult<bool>.Failure(ErrorType.BadRequest, "course id can not be zero or negative");

            // Soft-deleted courses are invisible everywhere — treat like missing.
            var course = await coursesRepository.GetRawCourseAsync(courseId);
            if (course == null)
                return MyResult<bool>.Failure(ErrorType.NotFound, "course not found");

            if (suspend && course.status == "suspended")
                return MyResult<bool>.Failure(ErrorType.Conflict, "course is already suspended");
            if (suspend && course.status != "published")
                return MyResult<bool>.Failure(ErrorType.Conflict, "only a published course can be suspended");
            if (!suspend && course.status != "suspended")
                return MyResult<bool>.Failure(ErrorType.Conflict, "course is not suspended");

            string newStatus = suspend ? "suspended" : "published";

            var updated = await coursesRepository.UpdateCourseStatusAsync(courseId, newStatus);
            if (updated == null) return MyResult<bool>.Failure(ErrorType.Failure, "failed to update course status");

            await adminActionService.LogAsync(
                adminId,
                actionType: suspend ? "suspend" : "unsuspend",
                targetTable: "courses",
                targetId: courseId,
                oldValue: new { status = course.status },
                newValue: new { status = newStatus });

            return MyResult<bool>.Success(true);
        }

        // Course takedown: a PERMANENT content purge, unlike suspend (a reversible flag).
        // Destroys the course's content (sections/lessons/content_blocks via DB cascade),
        // its reviews, and its bucket media, then tombstones the course row (deleted_at)
        // so it 404s everywhere. The FK-protected payment/enrollment records are KEPT
        // (refunds/disputes/accounting) — a student's history still resolves the historical
        // title off the tombstone. Any live status is purgeable; only a missing or
        // already-tombstoned course is rejected.
        public async Task<MyResult<bool>> TakedownCourse(int adminId, int courseId, string? removalReason)
        {
            if (courseId <= 0) return MyResult<bool>.Failure(ErrorType.BadRequest, "course id can not be zero or negative");

            var course = await coursesRepository.GetRawCourseAsync(courseId);
            if (course == null)
                return MyResult<bool>.Failure(ErrorType.NotFound, "course not found");

            // Capture what must be cleaned from storage BEFORE the rows (and the block
            // data pointing at those files) are gone — same pattern as the avatar name
            // captured before the anonymize trigger in DeleteUser.
            string? thumbnail = course.thumbnail_url;
            var lessonBlocks = await coursesRepository.GetCourseLessonContentBlocksAsync(courseId);
            var mediaNames = new HashSet<string>();
            foreach (var blocks in lessonBlocks)
                mediaNames.UnionWith(ContentBlockMedia.ExtractFileNames(blocks));

            // Reviews are destroyed too. Best-effort: a stray review would 404 with the
            // tombstoned course anyway, so a failure here must not block the takedown.
            await reviewRepository.DeleteAllReviewsForCourseAsync(courseId);

            if (!await coursesRepository.PurgeCourseContentAsync(courseId, removalReason))
                return MyResult<bool>.Failure(ErrorType.Failure, "failed to take down course");

            await adminActionService.LogAsync(
                adminId,
                actionType: "delete",
                targetTable: "courses",
                targetId: courseId,
                oldValue: new { status = course.status, title = course.title },
                newValue: new { deleted = true, removal_reason = removalReason, purged = true });

            // Storage cleanup AFTER the DB commit, best-effort (a leaked file is harmless,
            // a broken DB reference is not) — same media-cleanup policy as everywhere else.
            foreach (var name in mediaNames)
                await mediaService.DeleteCourseMediaAsync(name);
            if (!string.IsNullOrEmpty(thumbnail))
                await mediaService.DeleteCourseThumbnailAsync(thumbnail);

            return MyResult<bool>.Success(true);
        }
    }
}
