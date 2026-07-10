using Business.Interfaces;
using DataAccess.Interfaces;

namespace Business.Services
{
    public class AdminActionService(IAdminActionRepository adminActionRepository) : IAdminActionService
    {
        // Writes an immutable audit row to admin_actions.
        // action_type must be one of: 'create', 'update', 'delete', 'ban', 'unban'.
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
    }
}
