using DataAccess.Data;
using DataAccess.Entities;

namespace DataAccess.Repositories
{
    public class LoginLogRepository(AppDbContext context)
    {
        public async Task<bool> AddLoginLogAsync(int? userId, string status, string? ipAddress, string? userAgent, string? attemptedIdentifier = null)
        {
            try
            {
                System.Net.IPAddress? parsedIp = null;
                if (ipAddress != null)
                    System.Net.IPAddress.TryParse(ipAddress, out parsedIp);

                var log = new LoginLogEntitiy
                {
                    user_id = userId,
                    attempted_identifier = attemptedIdentifier,
                    status = status,
                    ip_address = parsedIp,
                    user_agent = userAgent,
                    attempted_at = DateTime.UtcNow
                };

                await context.LoginLogs.AddAsync(log);
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
