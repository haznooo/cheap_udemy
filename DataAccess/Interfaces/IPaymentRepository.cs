using DataAccess.Dto;
using DataAccess.Entities;
using static DataAccess.Common.clsPageResult;

namespace DataAccess.Interfaces
{
    public interface IPaymentRepository
    {
        Task<PaymentDto?> AddPaymentAsync(PaymentEntitiy payment);
        Task<bool> LinkEnrollmentAsync(int paymentId, int enrollmentId);
        Task<PageResult<PaymentDto>> GetPaymentsByUserIdAsync(int userId, int pageNumber, int pageSize);
    }
}
