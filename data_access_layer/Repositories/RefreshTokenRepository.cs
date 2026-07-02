
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

        // Finds the token by (user_id, token_hash) across ALL states — used, revoked and expired
        // included. Reuse detection needs to SEE a replayed, already-used token; the valid-only
        // lookup below hides it (it would just look "not found").
        public async Task<RefreshTokenEntity?> GetRefreshTokenByHashAsync(int userId, string tokenHash)
        {
            try
            {
                return await context.UserRefreshToken
                    .FirstOrDefaultAsync(rt => rt.user_id == userId && rt.token_hash == tokenHash);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return null;
            }
        }

        // Revokes an entire refresh-token chain (breach response) in one DB round trip via the
        // revoke_breached_chain stored procedure — walks replaced_by_id and updates every linked
        // token server-side, instead of the app fetching+updating one hop at a time.
        public async Task<bool> RevokeBreachedChainAsync(int startTokenId)
        {
            try
            {
                await context.Database.ExecuteSqlInterpolatedAsync($"CALL revoke_breached_chain({startTokenId})");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return false;
            }
        }

  
        // SHA-256 hash
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
        // Bulk-revokes every still-active refresh token for a user (sets used + revoked_at) without
        // deleting the rows, so reuse-detection history survives. Used on password change so a
        // previously stolen token can't outlive the credential it was obtained under. Returns the
        // number of rows revoked, or -1 on error.
        public async Task<int> RevokeAllRefreshTokensAsync(int userId)
        {
            try
            {
                return await context.UserRefreshToken
                    .Where(t => t.user_id == userId && t.revoked_at == null)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(t => t.is_used, true)
                        .SetProperty(t => t.revoked_at, DateTime.UtcNow));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return -1;
            }
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