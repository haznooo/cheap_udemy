using DataAccess.Entities;

namespace DataAccess.Interfaces
{
    public interface IRefreshTokenRepository
    {
        Task<RefreshTokenEntity> AddRefreshTokenAsync(RefreshTokenEntity RefreshToken);
        Task<RefreshTokenEntity> UpdateRefreshTokenAsync(RefreshTokenEntity OldRefreshToken);
        Task<RefreshTokenEntity?> GetRefreshTokenByHashAsync(int userId, string tokenHash);
        Task<bool> RevokeBreachedChainAsync(int startTokenId);
        Task<RefreshTokenEntity?> GetValidRefreshTokenByHashAsync(int userId, string tokenHash);
        Task<int> RevokeAllRefreshTokensAsync(int userId);
    }
}
