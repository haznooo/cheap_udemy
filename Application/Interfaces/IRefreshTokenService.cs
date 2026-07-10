using Business.Common;
using Business.Dto.Rsponse;
using DataAccess.Dto;

namespace Business.Interfaces
{
    // Instance surface only — RefreshTokenService.HashRefreshToken is a static
    // pure function and stays on the class (statics don't belong on interfaces).
    public interface IRefreshTokenService
    {
        Task<MyResult<RefreshTokenDto>> AddNewRefreshTokenFirstTime(int userId, string deviceInfo, string ipAddress, DateTime? expiresAt = null);
        Task<MyResult<LoginResponse>> RefreshAccessToken(string refreshToken, int userId, string deviceInfo, string ipAddress);
        Task<MyResult<bool>> RevokeRefreshToken(string refreshToken, int userId);
        Task<MyResult<bool>> RevokeAllForUser(int userId);
        string GenerateRefreshToken();
    }
}
