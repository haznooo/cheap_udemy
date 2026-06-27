
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

        // Returns the single still-usable token (not revoked, not used, not expired) matching the
        // given SHA-256 hash for the user. The hash is deterministic, so this is a direct indexed
        // equality lookup — no need to fetch every candidate and verify one by one.
        public async Task<RefreshTokenEntity?> GetValidRefreshTokenByHashAsync(int userId, string tokenHash)
        {
            try
            {
                return await context.UserRefreshToken
                    .FirstOrDefaultAsync(rt => rt.user_id == userId
                        && rt.token_hash == tokenHash
                        && rt.revoked_at == null
                        && !rt.is_used
                        && rt.expires_at > DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return null;
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