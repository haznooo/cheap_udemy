namespace Business.Interfaces
{
    public interface ILoginLogService
    {
        Task LogAsync(int? userId, string status, string? ipAddress, string? userAgent, string? attemptedIdentifier = null);
    }
}
