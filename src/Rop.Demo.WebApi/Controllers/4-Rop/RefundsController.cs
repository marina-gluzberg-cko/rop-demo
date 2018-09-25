using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Rop.Demo.Domain.ApiClient;
using Rop.Demo.Domain.Domain;
using Rop.Demo.Domain.Models;
using Rop.Demo.Domain.Repositories;
using Rop.Demo.WebApi.Models;
using System;
using System.Net;

namespace Rop.Demo.WebApi.Controllers.Rop
{
    [Route("rop/payments")]
    public class RefundsController : Controller
    {
        private readonly ILogger _logger;
        private readonly IMerchantsRepository _businessesRepository;
        private readonly IPaymentsRepository _paymentsRepository;
        private readonly IRefundsRepository _refundsRepository;
        private readonly IRefundApiClient _refundApiClient;        

        public RefundsController(
            ILoggerFactory loggerFactory,
            IMerchantsRepository businessesRepository,
            IPaymentsRepository paymentsRepository,
            IRefundsRepository refundsRepository,
            IRefundApiClient refundApiClient)
        {
            _logger = loggerFactory.CreateLogger<RefundsController>();
            _businessesRepository = businessesRepository;
            _paymentsRepository = paymentsRepository;
            _refundsRepository = refundsRepository;
            _refundApiClient = refundApiClient;
        }

        [HttpPut("{paymentId}/refunds/{refundId}")]
        public IActionResult Put(string paymentId, string refundId, [FromBody]Models.CreateRefundRequest request)
        {
            return GetReference(paymentId)
                .Bind(RetrievePayment)
                .Bind(ValidatePayment)
                .Bind(payment =>
                    RetrieveMerchant(payment.MerchantId)
                    .Bind(merchant =>
                        GetReference(refundId)						
                        .Bind(refundReference => GetRefundWithAmount(refundReference, payment, request))
                        .Bind(StoreRequestedRefund)
                        .Bind(refund =>
                            CreateRefund(merchant, payment, refund)
                            .Bind(ProcessResponse)
                            .Map(createRefundResult => PrepareSuccessfulResult(refund.Amount)))))
                .Either();
        }

        protected Result<Guid, IActionResult> GetReference(string id)
        {
            try
            {
                return Result<Guid, IActionResult>.Succeeded(new Guid(Base32.Decode(id)));
            }
            catch (ArgumentException)
            {
                _logger.LogWarning("Unable to extract guid from Id: {Id}.", id);
                return Result<Guid, IActionResult>.Failed(StatusCode(StatusCodes.Status404NotFound));
            }
        }

        protected Result<Merchant, IActionResult> RetrieveMerchant(long merchantId)
        {
            Merchant merchant = _businessesRepository.Read(merchantId);

            if (merchant != null)
            {
                return Result<Merchant, IActionResult>.Succeeded(merchant);
            }

            _logger.LogWarning("Merchant with id: {merchantId} is not onboarded.", merchantId);

            return Result<Merchant, IActionResult>.Failed(StatusCode(StatusCodes.Status403Forbidden));
        }

        protected Result<Payment, IActionResult> RetrievePayment(Guid paymentReference)
        {
            Payment payment = _paymentsRepository.Read(paymentReference);

            if (payment != null)
            {
                return Result<Payment, IActionResult>.Succeeded(payment);
            }

            _logger.LogWarning("Unable to find payment with paymentId: {Reference}.", paymentReference);
            return Result<Payment, IActionResult>.Failed(StatusCode(StatusCodes.Status404NotFound));
        }
    

        private Result<Payment, IActionResult> ValidatePayment(Payment payment)
        {
            // Only initialzed payments (not pending or failed to initialize) are valid in the sense that they exist and retrievable from Klarna 
            return payment.Created ?
                Result<Payment, IActionResult>.Succeeded(payment) :
                Result<Payment, IActionResult>.Failed(StatusCode(StatusCodes.Status403Forbidden));
        }

        private Result<Refund, IActionResult> GetRefundWithAmount(Guid refundReference, Payment payment, Models.CreateRefundRequest request)
        {
            int amount = default(int);

            if (request.Amount.HasValue)
            {
                amount = request.Amount.Value;
            }
            else
            {
                int refundedAmount = _refundsRepository.GetRefundedAmountForPayment(payment.Reference);

                amount = payment.Amount - refundedAmount;

                if (amount < 0)
                {
                    return Result<Refund, IActionResult>.Failed(StatusCode(StatusCodes.Status403Forbidden));
                }
            }

            Refund refund = new Refund() { Reference = refundReference, Amount = amount };

            return Result<Refund, IActionResult>.Succeeded(refund);
        }

        private Result<Refund, IActionResult> StoreRequestedRefund(Refund refund)
        {
            if (_refundsRepository.Create(refund))
            {
                return Result<Refund, IActionResult>.Succeeded(refund);
            }

            _logger.LogWarning("Refund with reference: {Reference} already exists.", refund.Reference);

            return Result<Refund, IActionResult>.Failed(StatusCode(StatusCodes.Status400BadRequest));
        }
   
        private Result<HttpStatusCode, IActionResult> CreateRefund(Merchant merchant, Payment payment, Refund refund)
        {
            HttpStatusCode createRefundStatus = _refundApiClient.CreateRefund(
                merchant,
                payment.Reference,
                new Demo.Domain.ApiClient.Requests.CreateRefundRequest { RefundedAmount = refund.Amount });

            bool successful = createRefundStatus == HttpStatusCode.Created;

            if (successful)
            {
                bool statusUpdateResult = _refundsRepository.SetStatusToCreated(refund.Reference);

                if (!statusUpdateResult)
                {
                    _logger.LogError("Refund with refundRed={refundReference} not found when setting status to Created.", refund.Reference);
                    return Result<HttpStatusCode, IActionResult>.Failed(StatusCode(StatusCodes.Status500InternalServerError));
                }
            }

            return Result<HttpStatusCode, IActionResult>.Succeeded(createRefundStatus);
        }

        private Result<HttpStatusCode, IActionResult> ProcessResponse(HttpStatusCode createRefundResult)
        {
            switch (createRefundResult) {
                case HttpStatusCode.Created:
                    return Result<HttpStatusCode, IActionResult>.Succeeded(createRefundResult);

                case HttpStatusCode.Forbidden:
                    return Result<HttpStatusCode, IActionResult>.Failed(StatusCode(StatusCodes.Status400BadRequest));

                case HttpStatusCode.BadRequest:
                    return Result<HttpStatusCode, IActionResult>.Failed(StatusCode(StatusCodes.Status422UnprocessableEntity));

                case HttpStatusCode.NotFound:
                    return Result<HttpStatusCode, IActionResult>.Failed(StatusCode(StatusCodes.Status502BadGateway));

                case HttpStatusCode.InternalServerError:
                    return Result<HttpStatusCode, IActionResult>.Failed(StatusCode(StatusCodes.Status502BadGateway));

                case HttpStatusCode.RequestTimeout:
                    return Result<HttpStatusCode, IActionResult>.Failed(StatusCode(StatusCodes.Status504GatewayTimeout));

                default:
                    return Result<HttpStatusCode, IActionResult>.Failed(StatusCode(StatusCodes.Status500InternalServerError));
            }
        }

        private IActionResult PrepareSuccessfulResult(int refundedAmount)
        {
            RefundResponse response = new RefundResponse() { RefundedAmount = refundedAmount };

            return Accepted(response);
        }
    }
}
