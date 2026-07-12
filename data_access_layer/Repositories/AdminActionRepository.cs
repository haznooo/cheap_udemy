using System.Text.Json;
using DataAccess.Data;
using DataAccess.Dto;
using DataAccess.Entities;
using DataAccess.Interfaces;
using Microsoft.EntityFrameworkCore;
using static DataAccess.Common.clsPageResult;

namespace DataAccess.Repositories
{
    public class AdminActionRepository(AppDbContext context) : IAdminActionRepository
    {
        // action_type must be one of: 'create', 'update', 'delete', 'ban', 'unban', 'suspend', 'unsuspend'
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

        // Newest-first paged read of the audit log (ix_admin_actions_performed_at
        // covers the ordering). Read-only view — rows are immutable at the DB level.
        public async Task<PageResult<AdminActionDto>?> GetAdminActionsAsync(int pageNumber, int pageSize)
        {
            try
            {
                var query = context.AdminActions.AsNoTracking();

                var totalCount = await query.CountAsync();

                var items = await query
                    .OrderByDescending(a => a.performed_at)
                    .ThenByDescending(a => a.id)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(a => new AdminActionDto
                    {
                        Id = a.id,
                        AdminId = a.admin_id,
                        AdminUsername = a.admin.username,
                        ActionType = a.action_type,
                        TargetTable = a.target_table,
                        TargetId = a.target_id,
                        OldValue = a.old_value,
                        NewValue = a.new_value,
                        PerformedAt = a.performed_at
                    })
                    .ToListAsync();

                return new PageResult<AdminActionDto>
                {
                    Items = items,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return null;
            }
        }
    }
}
