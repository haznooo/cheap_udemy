
using DataAccess.Data;
using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;


namespace DataAccess.Repositories
{
    public class RefreshTokenRepository(AppDbContext context)
    {

        public async Task<RefreshTokenEntity> AddRefreshTokenAsync(RefreshTokenEntity RefreshToken)
        {

            try
            {

                context.UserRefreshToken.Add(RefreshToken);
                await context.SaveChangesAsync();
                return RefreshToken;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return null;

            }

        }

        public async Task<RefreshTokenEntity> UpdateRefreshTokenAsync(RefreshTokenEntity OldRefreshToken)
        {
            try
            {
                context.UserRefreshToken.Update(OldRefreshToken);
                await context.SaveChangesAsync();
                return OldRefreshToken;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return null;
            }
        }
        public async Task<RefreshTokenEntity> GetRefreshTokenEntityByUserIdAsync(int userId)
        {
            try
            {
                var refreshToken = await context.UserRefreshToken.FirstOrDefaultAsync(rt => rt.user_id == userId);
                return refreshToken;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return null;
            }
        }

        // Returns the user's tokens that are still usable (not revoked, not used, not expired).
        // The caller BCrypt-verifies the presented token against each hash to find the match.
        public async Task<List<RefreshTokenEntity>> GetValidRefreshTokensByUserIdAsync(int userId)
        {
            try
            {
                return await context.UserRefreshToken
                    .Where(rt => rt.user_id == userId
                        && rt.revoked_at == null
                        && !rt.is_used
                        && rt.expires_at > DateTime.UtcNow)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return new List<RefreshTokenEntity>();
            }
        }

        public async Task<string?> GetRefreshTokenByUserIdAsync(int userId)
        {

            return await context.UserRefreshToken.Where(u => u.user_id == userId).Select(u => u.token_hash).FirstOrDefaultAsync();

        }
        public async Task<bool> DeleteAllRefreshTokensAsync(int userId)
        {
            try
            {
               var results =   context.UserRefreshToken
                      .Where(t => t.user_id == userId)
                  .ExecuteDelete();

                return results > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return false;
            }

        }
    }
}