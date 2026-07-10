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
    }
}
