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

        [Fact]
        public async Task EnrollStudent_PaidCourse_ReturnsBadRequest()
        {
            // Arrange — the DTO must pass every guard BEFORE the price check
            // (exists, not deleted, published, instructor != caller) so the test
            // exercises exactly the paid-course branch and nothing earlier.
            var enrollmentRepository = new Mock<IEnrollmentRepository>();
            enrollmentRepository
                .Setup(r => r.GetCourseEnrollmentInfoAsync(7))
                .ReturnsAsync(new CourseEnrollmentInfoDto
                {
                    InstructorId = 2,       // caller is 1, so the self-enroll guard passes
                    Status = "published",
                    IsDeleted = false,
                    Price = 49.99m          // the branch under test
                });
            // No setup for GetEnrollmentStatusAsync — the price guard returns first,
            // so that call is never reached (loose-mock defaults cover it).

            var sut = new EnrollmentService(enrollmentRepository.Object);

            // Act
            var result = await sut.EnrollStudent(callerId: 1, new EnrollRequest { CourseId = 7 });

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorType.BadRequest, result.FailureType);
        }
    }
}
