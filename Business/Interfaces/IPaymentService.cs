using Business.Common;
using Business.Dto.Request;
using DataAccess.Dto;
using static DataAccess.Common.clsPageResult;

namespace Business.Interfaces
{
    public interface IPaymentService
    {
        Task<MyResult<PaymentDto>> Checkout(int callerId, CheckoutRequest request);
        Task<MyResult<PageResult<PaymentDto>>> GetMyPayments(int callerId, int pageNumber, int pageSize);
    }
}
