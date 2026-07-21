using Business.Dto.Request;
using Business.Interfaces;
using DataAccess.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static DataAccess.Common.clsPageResult;

namespace Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/Payments")]
    public class PaymentController(IPaymentService paymentService) : ApiControllerBase
    {
        // Simulated checkout — no real gateway. Buying a paid course records a completed
        // payment and enrolls the caller in one step.
        [HttpPost("checkout")]
        public async Task<ActionResult<PaymentDto>> Checkout(CheckoutRequest request)
        {
            if (CallerId is not int callerId) return MissingIdentity();

            var result = await paymentService.Checkout(callerId, request);

            if (!result.IsSuccess) return MapFailure(result);

            return Ok(result.Value);
        }

        // The caller's own payment history (identity from the access token).
        [HttpGet("me")]
        public async Task<ActionResult<PageResult<PaymentDto>>> GetMyPayments(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            if (CallerId is not int callerId) return MissingIdentity();

            var result = await paymentService.GetMyPayments(callerId, pageNumber, pageSize);

            if (!result.IsSuccess) return MapFailure(result);

            return Ok(result.Value);
        }
    }
}
