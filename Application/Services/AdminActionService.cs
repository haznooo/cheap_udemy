using Business.Common;
using Business.Interfaces;
using DataAccess.Dto;
using DataAccess.Interfaces;
using static DataAccess.Common.clsPageResult;

namespace Business.Services
{
    public class AdminActionService(IAdminActionRepository adminActionRepository) : IAdminActionService
    {
        // Writes an immutable audit row to admin_actions.
        // action_type must be one of: 'create', 'update', 'delete', 'ban', 'unban', 'suspend', 'unsuspend'.
        // Keep oldValue/newValue to small, non-sensitive snapshots (never passwords/tokens).
        public async Task LogAsync(
            int adminId,
            string actionType,
            string targetTable,
            int targetId,
            object? oldValue = null,
            object? newValue = null)
        {
            await adminActionRepository.AddAdminActionAsync(adminId, actionType, targetTable, targetId, oldValue, newValue);
        }

        // Newest-first paged read of the audit log for admins.
        public async Task<MyResult<PageResult<AdminActionDto>>> GetAdminActions(int pageNumber, int pageSize)
        {
            if (pageNumber <= 0 || pageSize <= 0)
                return MyResult<PageResult<AdminActionDto>>.Failure(ErrorType.BadRequest, "Invalid page number or page size.");

            var actions = await adminActionRepository.GetAdminActionsAsync(pageNumber, pageSize);

            if (actions == null)
                return MyResult<PageResult<AdminActionDto>>.Failure(ErrorType.Failure, "Failed to retrieve admin actions.");

            return MyResult<PageResult<AdminActionDto>>.Success(actions);
        }
    }
}
