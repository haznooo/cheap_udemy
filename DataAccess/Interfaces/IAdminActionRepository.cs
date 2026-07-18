using DataAccess.Dto;
using static DataAccess.Common.clsPageResult;

namespace DataAccess.Interfaces
{
    public interface IAdminActionRepository
    {
        Task<bool> AddAdminActionAsync(
            int adminId,
            string actionType,
            string targetTable,
            int targetId,
            object? oldValue,
            object? newValue);

        Task<PageResult<AdminActionDto>?> GetAdminActionsAsync(int pageNumber, int pageSize);
    }
}
