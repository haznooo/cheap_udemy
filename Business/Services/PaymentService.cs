using Business.Common;
using Business.Dto.Request;
using Business.Interfaces;
using DataAccess.Dto;
using DataAccess.Entities;
using DataAccess.Interfaces;
using static DataAccess.Common.clsPageResult;

namespace Business.Services
{
    public class PaymentService(IPaymentRepository paymentRepository, IEnrollmentRepository enrollmentRepository) : IPaymentService
    {
        // Simulated checkout: the "payment" always succeeds (provider = 'simulated', no real
        // gateway). Flow: validate → insert a completed payment row → create the enrollment
        // it pays for → link the payment to that enrollment. callerId comes from the JWT.
        public async Task<MyResult<PaymentDto>> Checkout(int callerId, CheckoutRequest request)
        {
            if (callerId <= 0)
                return MyResult<PaymentDto>.Failure(ErrorType.BadRequest, "Invalid user ID.");

            if (request.CourseId <= 0)
                return MyResult<PaymentDto>.Failure(ErrorType.BadRequest, "Invalid course ID.");

            var course = await enrollmentRepository.GetCourseEnrollmentInfoAsync(request.CourseId);

            // Same visibility rule as EnrollStudent: draft/retired/suspended/deleted courses
            // are non-purchasable and must look non-existent.
            if (course == null || course.IsDeleted || course.Status != "published")
                return MyResult<PaymentDto>.Failure(ErrorType.NotFound, "Course not found.");

            if (course.InstructorId == callerId)
                return MyResult<PaymentDto>.Failure(ErrorType.BadRequest, "Instructors cannot buy their own course.");

            if (course.Price <= 0)
                return MyResult<PaymentDto>.Failure(ErrorType.BadRequest, "This course is free — enroll directly instead of checking out.");

            string? existingStatus = await enrollmentRepository.GetEnrollmentStatusAsync(callerId, request.CourseId);

            if (existingStatus != null && existingStatus != "dropped")
                return MyResult<PaymentDto>.Failure(ErrorType.Conflict, "User is already enrolled in this course.");

            var payment = new PaymentEntitiy
            {
                user_id = callerId,
                course_id = request.CourseId,
                amount = course.Price,
                currency = "USD",
                status = "completed",
                provider = "simulated",
                provider_reference = $"SIM-{Guid.NewGuid():N}",
                payment_date = DateTime.UtcNow
            };

            var paymentDto = await paymentRepository.AddPaymentAsync(payment);
            if (paymentDto == null)
                return MyResult<PaymentDto>.Failure(ErrorType.Failure, "Failed to record the payment.");

            // Legacy 'dropped' rows must be reactivated, not re-inserted — the enrollments
            // table has UNIQUE(user_id, course_id). Same handling as the free-enroll path.
            EnrollmentDto? enrollment;
            if (existingStatus == "dropped")
            {
                enrollment = await enrollmentRepository.ReactivateDroppedEnrollmentAsync(callerId, request.CourseId);
            }
            else
            {
                enrollment = await enrollmentRepository.EnrollStudentAsync(new EnrollmentEntitiy
                {
                    user_id = callerId,
                    course_id = request.CourseId,
                    enrollment_date = DateTime.UtcNow,
                    status = "active",
                    progress_percentage = 0
                });
            }

            // No transaction around payment+enrollment (same accepted trade-off as the
            // refresh-token mint/revoke pair) — fail loudly so the gap is visible.
            if (enrollment == null)
                return MyResult<PaymentDto>.Failure(ErrorType.Failure, "The payment was recorded but enrollment failed. Please try enrolling again or contact the site owner.");

            bool linked = await paymentRepository.LinkEnrollmentAsync(paymentDto.PaymentId, enrollment.EnrollmentId);
            if (!linked)
                return MyResult<PaymentDto>.Failure(ErrorType.Failure, "You are enrolled, but the payment record could not be linked to the enrollment.");

            paymentDto.EnrollmentId = enrollment.EnrollmentId;
            return MyResult<PaymentDto>.Success(paymentDto);
        }

        // A user can only read their own payment history (identity from the token).
        public async Task<MyResult<PageResult<PaymentDto>>> GetMyPayments(int callerId, int pageNumber, int pageSize)
        {
            if (callerId <= 0)
                return MyResult<PageResult<PaymentDto>>.Failure(ErrorType.BadRequest, "Invalid user ID.");

            if (pageNumber <= 0 || pageSize <= 0)
                return MyResult<PageResult<PaymentDto>>.Failure(ErrorType.BadRequest, "Invalid page number or page size.");

            var r = await paymentRepository.GetPaymentsByUserIdAsync(callerId, pageNumber, pageSize);

            if (r == null)
                return MyResult<PageResult<PaymentDto>>.Failure(ErrorType.Failure, "Failed to retrieve payments.");

            return MyResult<PageResult<PaymentDto>>.Success(r);
        }
    }
}
