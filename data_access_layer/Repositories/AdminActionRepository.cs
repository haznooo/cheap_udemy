using System.Text.Json;
using DataAccess.Data;
using DataAccess.Entities;
using DataAccess.Interfaces;

namespace DataAccess.Repositories
{
    public class AdminActionRepository(AppDbContext context) : IAdminActionRepository
    {
        // action_type must be one of: 'create', 'update', 'delete', 'ban', 'unban'
        // NOTE: the DB trigger trg_verify_admin_action rejects the insert if adminId is not an admin.
        public async Task<bool> AddAdminActionAsync(
            int adminId,
            string actionType,
            string targetTable,
            int targetId,
            object? oldValue,
            object? newValue)
        {
            try
            {
                var action = new AdminActionEntitiy
                {
                    admin_id = adminId,
                    action_type = actionType,
                    target_table = targetTable,
                    target_id = targetId,
                    // Serialize only the small, non-sensitive snapshots passed by the caller.
                    old_value = oldValue is null ? null : JsonSerializer.SerializeToDocument(oldValue),
                    new_value = newValue is null ? null : JsonSerializer.SerializeToDocument(newValue),
                    performed_at = DateTime.UtcNow
                };

                await context.AdminActions.AddAsync(action);
                await context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return false;
            }
        }
    }
}
