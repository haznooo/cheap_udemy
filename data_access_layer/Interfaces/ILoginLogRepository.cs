namespace DataAccess.Interfaces
{
    public interface ILoginLogRepository
    {
        Task<bool> AddLoginLogAsync(int? userId, string status, string? ipAddress, string? userAgent, string? attemptedIdentifier = null);
    }
}
