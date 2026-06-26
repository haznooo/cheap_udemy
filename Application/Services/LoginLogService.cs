using DataAccess.Data;
using DataAccess.Repositories;

namespace Business.Services
{
    public class LoginLogService(AppDbContext context)
    {
        // status must be one of: 'success', 'failed', 'locked'
        public async Task LogAsync(int userId, string status, string? ipAddress, string? userAgent)
        {
            var repo = new LoginLogRepository(context);
            await repo.AddLoginLogAsync(userId, status, ipAddress, userAgent);
        }
    }
}
