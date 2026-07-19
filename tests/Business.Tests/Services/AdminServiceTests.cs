using Business.Interfaces;
using Business.Services;
using DataAccess.Dto;
using DataAccess.Interfaces;
using Moq;

namespace Business.Tests.Services
{
    // Interaction tests: SetUserStatus's contract isn't just its return value —
    // it's WHICH side effects fire (session revocation, audit row). Verify/Times
    // assert those calls happened (or didn't), It.Is pins the argument that matters.
    public class AdminServiceTests
    {
        // Shared arrange: target user exists with the given status, DB update succeeds.
        private static AdminService CreateSut(
            string targetCurrentStatus,
            string newStatus,
            out Mock<IRefreshTokenService> refreshTokenService,
            out Mock<IAdminActionService> adminActionService)
        {
            var userRepository = new Mock<IUserAndProfileRepository>();
            refreshTokenService = new Mock<IRefreshTokenService>();
            adminActionService = new Mock<IAdminActionService>();

            userRepository
                .Setup(r => r.GetUserStatusAndRoleAsync(7))
                .ReturnsAsync(new UserStatusRoleDto { Status = targetCurrentStatus, Role = "student" });
            userRepository
                .Setup(r => r.UpdateUserStatusAsync(7, newStatus))
                .ReturnsAsync(true);

            return new AdminService(userRepository.Object, refreshTokenService.Object, adminActionService.Object, new Mock<ICoursesRepository>().Object);
        }

        [Fact]
        public async Task SetUserStatus_Ban_RevokesAllSessionsOnce()
        {
            // Arrange
            var sut = CreateSut("active", "banned", out var refreshTokenService, out _);

            // Act
            var result = await sut.SetUserStatus(adminId: 1, targetUserId: 7, newStatus: "banned");

            // Assert — a ban that leaves sessions alive is a security bug, so the
            // revocation call IS the behaviour under test.
            Assert.True(result.IsSuccess);
            refreshTokenService.Verify(s => s.RevokeAllForUser(7), Times.Once);
        }

        [Fact]
        public async Task SetUserStatus_Unban_NeverRevokesSessions()
        {
            // Arrange
            var sut = CreateSut("banned", "active", out var refreshTokenService, out _);

            // Act
            var result = await sut.SetUserStatus(adminId: 1, targetUserId: 7, newStatus: "active");

            // Assert — reactivation must NOT log anyone out. It.IsAny belongs here
            // (in Verify): "never called, with any argument".
            Assert.True(result.IsSuccess);
            refreshTokenService.Verify(s => s.RevokeAllForUser(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task SetUserStatus_UnbanSuspendedUser_AuditsAsUnsuspend()
        {
            // Arrange — same "active" transition as above, but the OLD status decides
            // the audit label: suspended → "unsuspend", not "unban".
            var sut = CreateSut("suspended", "active", out _, out var adminActionService);

            // Act
            var result = await sut.SetUserStatus(adminId: 1, targetUserId: 7, newStatus: "active");

            // Assert — It.Is pins the one argument the test is about; the JSONB
            // snapshots are incidental here, so It.IsAny is fine for them.
            Assert.True(result.IsSuccess);
            adminActionService.Verify(s => s.LogAsync(
                1,
                It.Is<string>(actionType => actionType == "unsuspend"),
                "users",
                7,
                It.IsAny<object?>(),
                It.IsAny<object?>()), Times.Once);
        }
    }
}
