using DataAccess.Data;
using DataAccess.Repositories;

namespace Business.Services
{
    public class LoginLogService(AppDbContext context)
    {
        // status must be one of: 'success', 'failed', 'locked'
        // userId may be null (e.g. a failed attempt for an unknown email); pass the
        // attempted identifier so those attempts are still auditable (never the password).
        public async Task LogAsync(int? userId, string status, string? ipAddress, string? userAgent, string? attemptedIdentifier = null)
        {
            var repo = new LoginLogRepository(context);
            await repo.AddLoginLogAsync(userId, status, ipAddress, userAgent, attemptedIdentifier);
        }
    }
}
