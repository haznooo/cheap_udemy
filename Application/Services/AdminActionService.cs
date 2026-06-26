using DataAccess.Data;
using DataAccess.Repositories;

namespace Business.Services
{
    public class AdminActionService(AppDbContext context)
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
            var repo = new AdminActionRepository(context);
            await repo.AddAdminActionAsync(adminId, actionType, targetTable, targetId, oldValue, newValue);
        }
    }
}
