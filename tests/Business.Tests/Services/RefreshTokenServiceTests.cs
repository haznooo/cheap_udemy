using Business.Common;
using Business.Services;
using DataAccess.Dto;
using DataAccess.Entities;
using DataAccess.Interfaces;
using Moq;

namespace Business.Tests.Services
{
    // Capstone: the same method returns the same 401 for two very different reasons —
    // only the SIDE EFFECTS distinguish theft (chain revoked) from a stale token
    // (nothing touched). The return value alone can't prove the branch, Verify can.
    public class RefreshTokenServiceTests
    {
        private static UserAndProfileDto ActiveUser() => new()
        {
            UserId = 1,
            Username = "student",
            Email = "student@example.com",
            Role = "student",
            Status = "active"
        };

        [Fact]
        public async Task RefreshAccessToken_ReplayedRotatedToken_RevokesChainAndReturnsUnauthorized()
        {
            // Arrange — replaced_by_id != null means this token was already rotated
            // away; presenting it again means two parties hold the chain (theft).
            var refreshTokenRepository = new Mock<IRefreshTokenRepository>();
            var userRepository = new Mock<IUserAndProfileRepository>();

            userRepository.Setup(r => r.GetUserByIdAsync(1)).ReturnsAsync(ActiveUser());

            const string presentedToken = "stolen-token";
            // The service hashes the plain token before the lookup, so the setup must
            // expect the hash — reusing the real (deterministic) hasher keeps it exact-arg.
            string tokenHash = RefreshTokenService.HashRefreshToken(presentedToken);

            refreshTokenRepository
                .Setup(r => r.GetRefreshTokenByHashAsync(1, tokenHash))
                .ReturnsAsync(new RefreshTokenEntity { token_id = 10, user_id = 1, replaced_by_id = 11 });
            refreshTokenRepository
                .Setup(r => r.RevokeBreachedChainAsync(10))
                .ReturnsAsync(true);

            var sut = new RefreshTokenService(refreshTokenRepository.Object, userRepository.Object);

            // Act
            var result = await sut.RefreshAccessToken(presentedToken, userId: 1, "device", "1.2.3.4");

            // Assert — 401 AND the whole chain revoked, starting from the replayed link.
            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorType.Unauthorized, result.FailureType);
            refreshTokenRepository.Verify(r => r.RevokeBreachedChainAsync(10), Times.Once);
        }

        [Fact]
        public async Task RefreshAccessToken_LoggedOutToken_ReturnsUnauthorizedWithoutBreach()
        {
            // Arrange — logout sets is_used but never links a child (replaced_by_id
            // stays null), so a replay of a logged-out token is NOT theft: plain 401,
            // no chain revocation, no new token minted.
            var refreshTokenRepository = new Mock<IRefreshTokenRepository>();
            var userRepository = new Mock<IUserAndProfileRepository>();

            userRepository.Setup(r => r.GetUserByIdAsync(1)).ReturnsAsync(ActiveUser());

            const string presentedToken = "logged-out-token";
            string tokenHash = RefreshTokenService.HashRefreshToken(presentedToken);

            refreshTokenRepository
                .Setup(r => r.GetRefreshTokenByHashAsync(1, tokenHash))
                .ReturnsAsync(new RefreshTokenEntity
                {
                    token_id = 10,
                    user_id = 1,
                    is_used = true,
                    replaced_by_id = null,
                    expires_at = DateTime.UtcNow.AddDays(3) // not expired — is_used alone ends it
                });

            var sut = new RefreshTokenService(refreshTokenRepository.Object, userRepository.Object);

            // Act
            var result = await sut.RefreshAccessToken(presentedToken, userId: 1, "device", "1.2.3.4");

            // Assert — same 401 as the breach case; the Times.Never calls are what
            // prove the benign branch was taken.
            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorType.Unauthorized, result.FailureType);
            refreshTokenRepository.Verify(r => r.RevokeBreachedChainAsync(It.IsAny<int>()), Times.Never);
            refreshTokenRepository.Verify(r => r.AddRefreshTokenAsync(It.IsAny<RefreshTokenEntity>()), Times.Never);
        }
    }
}
