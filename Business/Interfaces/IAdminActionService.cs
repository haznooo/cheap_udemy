using Business.Common;
using DataAccess.Dto;
using static DataAccess.Common.clsPageResult;

namespace Business.Interfaces
{
    public interface IAdminActionService
    {
        Task LogAsync(
            int adminId,
            string actionType,
            string targetTable,
            int targetId,
            object? oldValue = null,
            object? newValue = null);

        Task<MyResult<PageResult<AdminActionDto>>> GetAdminActions(int pageNumber, int pageSize);
    }
}
