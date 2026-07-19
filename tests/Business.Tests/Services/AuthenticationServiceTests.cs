using Business.Common;
using Business.Dto.Request;
using Business.Dto.Rsponse;
using Business.Interfaces;
using Business.Services;
using DataAccess.Dto;
using DataAccess.Entities;
using DataAccess.Interfaces;
using FluentAssertions;
using Moq;

namespace Business.Tests.Services
{
    public class AuthenticationServiceTests
    {
        [Theory]
        [InlineData("")]                        // empty
        [InlineData("a23456789012345678901")]   // 21 chars — one past the 20-char boundary
        [InlineData(".user")]                   // leading dot
        [InlineData("user.")]                   // trailing dot
        [InlineData("us..er")]                  // consecutive dots
        [InlineData("user name")]               // space
        [InlineData("usér")]                    // non-ASCII letter
        [InlineData("user!")]                   // symbol outside the allowlist
        public async Task UserSignUp_InvalidUsername_ReturnsBadRequest(string username)
        {
            // Arrange — the username guard runs before ANY dependency call,
            // so loose mocks with zero setups are enough.
            var userRepository = new Mock<IUserAndProfileRepository>();
            var refreshTokenService = new Mock<IRefreshTokenService>();
            var loginLogService = new Mock<ILoginLogService>();
            var sut = new AuthenticationService(userRepository.Object, refreshTokenService.Object, loginLogService.Object);

            // Act — email and password are valid so the username is the only possible rejection.
            var result = await sut.UserSignUp(new SignUpRequest(username, "user@example.com", "secret1"), "device", "1.2.3.4");

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorType.BadRequest, result.FailureType);
        }

        [Theory]
        [InlineData("a")]                       // 1 char — lower boundary
        [InlineData("a2345678901234567890")]    // 20 chars — upper boundary
        public async Task UserSignUp_BoundaryLengthUsername_Succeeds(string username)
        {
            // Arrange — the valid side of the boundary must survive the whole flow,
            // so the mocks walk it through to a successful signup.
            var userRepository = new Mock<IUserAndProfileRepository>();
            var refreshTokenService = new Mock<IRefreshTokenService>();
            var loginLogService = new Mock<ILoginLogService>();

            // IsUsernameUsedAsync / IsEmailUsedAsync need no setup: loose-mock default false = "not taken".
            userRepository
                .Setup(r => r.AddUserAsync(It.Is<UserEntity>(u => u.username == username)))
                .ReturnsAsync(new UserAndProfileDto
                {
                    UserId = 5,
                    Username = username,
                    Email = "user@example.com",
                    Role = "student",
                    Status = "active"
                });

            refreshTokenService
                .Setup(s => s.AddNewRefreshTokenFirstTime(5, "device", "1.2.3.4", null))
                .ReturnsAsync(MyResult<RefreshTokenDto>.Success(new RefreshTokenDto { RefreshToken = "refresh-token" }));

            var sut = new AuthenticationService(userRepository.Object, refreshTokenService.Object, loginLogService.Object);

            // Act
            var result = await sut.UserSignUp(new SignUpRequest(username, "user@example.com", "secret1"), "device", "1.2.3.4");

            // Assert
            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task LoginUser_ValidCredentials_ReturnsFullLoginResponse()
        {
            // Arrange — BCrypt.Verify runs against a REAL hash (hashing isn't mocked;
            // it's a static pure function inside the unit under test).
            var userRepository = new Mock<IUserAndProfileRepository>();
            var refreshTokenService = new Mock<IRefreshTokenService>();
            var loginLogService = new Mock<ILoginLogService>();

            string hashedPassword = BCrypt.Net.BCrypt.HashPassword("secret123");
            var expiresAt = new DateTime(2026, 7, 26, 0, 0, 0, DateTimeKind.Utc);

            // Exact-arg setup on the LOWERCASE email: the request below uses mixed case,
            // so this setup only matches if the service normalizes before the lookup.
            userRepository
                .Setup(r => r.GetUserForLoginAsync("student@example.com"))
                .ReturnsAsync(new LoginLookupDto
                {
                    UserId = 5,
                    Username = "student",
                    Email = "student@example.com",
                    Role = "student",
                    Status = "active",
                    HashedPassword = hashedPassword,
                    Profile = new UserProfileDto { DisplayName = "Student One", Bio = "hi", ImageUrl = "avatar.png" }
                });

            refreshTokenService
                .Setup(s => s.AddNewRefreshTokenFirstTime(5, "device", "1.2.3.4", null))
                .ReturnsAsync(MyResult<RefreshTokenDto>.Success(
                    new RefreshTokenDto { RefreshToken = "new-refresh-token", ExpiresAt = expiresAt }));

            var sut = new AuthenticationService(userRepository.Object, refreshTokenService.Object, loginLogService.Object);

            // Act
            var result = await sut.LoginUser(new LoginRequest("Student@Example.com", "secret123"), "device", "1.2.3.4");

            // Assert — one structural comparison of the WHOLE response (incl. the nested
            // Profile record) instead of a property-by-property assert list.
            Assert.True(result.IsSuccess);
            result.Value.Should().BeEquivalentTo(new LoginResponse
            {
                Id = 5,
                Username = "student",
                Email = "student@example.com",
                Role = "student",
                Status = "active",
                AccessToken = null!, // minted by the controller, never by the service
                RefreshToken = "new-refresh-token",
                RefreshTokenExpiresAt = expiresAt,
                Profile = new UserProfileResponse("Student One", "hi", "avatar.png")
            });
        }
    }
}
