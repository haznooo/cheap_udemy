using Business.Common;
using Business.Interfaces;
using DataAccess.Dto;
using DataAccess.Interfaces;

namespace Business.Services
{
    // Admin-only cross-user actions (the api/admin use cases). Sits on the same
    // user repository as UserService/AuthenticationService — repos stay per-table,
    // services per-use-case. The audit row is written HERE, right next to the
    // mutation, so no caller can perform an admin action and skip the audit.
    public class AdminService(IUserAndProfileRepository userRepository, IRefreshTokenService refreshTokenService, IAdminActionService adminActionService) : IAdminService
    {
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
    }
}
