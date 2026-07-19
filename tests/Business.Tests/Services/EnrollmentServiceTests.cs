using Business.Common;
using Business.Dto.Request;
using Business.Services;
using DataAccess.Dto;
using DataAccess.Interfaces;
using Moq;

namespace Business.Tests.Services
{
    public class EnrollmentServiceTests
    {
        [Fact]
        public async Task EnrollStudent_CourseDoesNotExist_ReturnsNotFound()
        {
            // Arrange
            var enrollmentRepository = new Mock<IEnrollmentRepository>();
            enrollmentRepository
                .Setup(r => r.GetCourseEnrollmentInfoAsync(999))
                .ReturnsAsync((CourseEnrollmentInfoDto?)null); // cast so the compiler picks the TResult overload for null

            var sut = new EnrollmentService(enrollmentRepository.Object);

            // Act
            var result = await sut.EnrollStudent(callerId: 1, new EnrollRequest { CourseId = 999 });

            // Assert
            Assert.False(result.IsSuccess);
            // The enum is the contract (controller maps it to a 404); the message is UI copy.
            Assert.Equal(ErrorType.NotFound, result.FailureType);

        }
    }
}
