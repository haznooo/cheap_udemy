using DataAccess.Data;
using DataAccess.Dto;
using DataAccess.Entities;
using DataAccess.Interfaces;
using Microsoft.EntityFrameworkCore;
using static DataAccess.Common.clsPageResult;

namespace DataAccess.Repositories
{
    public class PaymentRepository(AppDbContext context) : IPaymentRepository
    {
        public async Task<PaymentDto?> AddPaymentAsync(PaymentEntitiy payment)
        {
            context.Payments.Add(payment);

            try
            {
                var rows = await context.SaveChangesAsync();
                if (rows <= 0) return null;

                string courseTitle = await context.Courses
                    .Where(c => c.course_id == payment.course_id)
                    .Select(c => c.title)
                    .FirstOrDefaultAsync() ?? "Unknown Course";

                return new PaymentDto
                {
                    PaymentId = payment.payment_id,
                    UserId = payment.user_id,
                    CourseId = payment.course_id,
                    CourseTitle = courseTitle,
                    EnrollmentId = payment.enrollment_id,
                    Amount = payment.amount,
                    Currency = payment.currency,
                    Status = payment.status,
                    Provider = payment.provider,
                    ProviderReference = payment.provider_reference,
                    PaymentDate = payment.payment_date
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return null;
            }
        }

        // Points a payment at the enrollment it produced (checkout creates the payment
        // row first, the enrollment second — see PaymentService.Checkout).
        public async Task<bool> LinkEnrollmentAsync(int paymentId, int enrollmentId)
        {
            try
            {
                var payment = await context.Payments
                    .FirstOrDefaultAsync(p => p.payment_id == paymentId);

                if (payment == null) return false;

                payment.enrollment_id = enrollmentId;
                await context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return false;
            }
        }

        public async Task<PageResult<PaymentDto>> GetPaymentsByUserIdAsync(int userId, int pageNumber, int pageSize)
        {
            try
            {
                var query = context.Payments
                    .Where(p => p.user_id == userId)
                    .AsNoTracking();

                var totalCount = await query.CountAsync();

                var items = await query
                    .OrderByDescending(p => p.payment_date)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(p => new PaymentDto
                    {
                        PaymentId = p.payment_id,
                        UserId = p.user_id,
                        CourseId = p.course_id,
                        CourseTitle = p.course.title,
                        EnrollmentId = p.enrollment_id,
                        Amount = p.amount,
                        Currency = p.currency,
                        Status = p.status,
                        Provider = p.provider,
                        ProviderReference = p.provider_reference,
                        PaymentDate = p.payment_date
                    })
                    .ToListAsync();

                return new PageResult<PaymentDto>
                {
                    Items = items,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return null;
            }
        }
    }
}
