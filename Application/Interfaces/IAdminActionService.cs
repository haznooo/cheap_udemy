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
    }
}
