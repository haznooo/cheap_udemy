using DataAccess.Entities;

namespace DataAccess.Interfaces
{
    public interface IRefreshTokenRepository
    {
        Task<RefreshTokenEntity> AddRefreshTokenAsync(RefreshTokenEntity RefreshToken);
        Task<RefreshTokenEntity> UpdateRefreshTokenAsync(RefreshTokenEntity OldRefreshToken);
        Task<RefreshTokenEntity> GetRefreshTokenEntityByUserIdAsync(int userId);
        Task<RefreshTokenEntity?> GetRefreshTokenByHashAsync(int userId, string tokenHash);
        Task<bool> RevokeBreachedChainAsync(int startTokenId);
        Task<RefreshTokenEntity?> GetValidRefreshTokenByHashAsync(int userId, string tokenHash);
        Task<string?> GetRefreshTokenByUserIdAsync(int userId);
        Task<int> RevokeAllRefreshTokensAsync(int userId);
        Task<bool> DeleteAllRefreshTokensAsync(int userId);
    }
}
