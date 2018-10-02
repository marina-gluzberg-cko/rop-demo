using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Optional.Unsafe;
using Rop.Demo.Domain.ApiClient;
using Rop.Demo.Domain.Domain;
using Rop.Demo.Domain.Models;
using Rop.Demo.Domain.Repositories;
using Rop.Demo.WebApi.Domain;
using Rop.Demo.WebApi.Models;
using System;
using System.Net;

namespace Rop.Demo.WebApi.Controllers.ServiceResult
{
    [Route("results/payments")]
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
            ServiceResult<Guid, IActionResult> getPaymentReferenceResult = GetReference(paymentId);

            if (getPaymentReferenceResult.HasError)
            {
                return getPaymentReferenceResult.Error.ValueOrFailure();
            }

            Guid paymentReference = paymentReference = getPaymentReferenceResult.Result.ValueOrFailure();


            ServiceResult<Guid, IActionResult> getRefundReferenceResult = GetReference(refundId);

            if (getRefundReferenceResult.HasError)
            {
                return getRefundReferenceResult.Error.ValueOrFailure();
            }

            Guid refundReference = getRefundReferenceResult.Result.ValueOrFailure();


            ServiceResult<Payment, IActionResult> retrievePaymentResult = RetrievePayment(paymentReference);

            if (retrievePaymentResult.HasError)
            {
                return retrievePaymentResult.Error.ValueOrFailure();
            }

            Payment payment = retrievePaymentResult.Result.ValueOrFailure();


            ServiceResult<Refund, IActionResult> getRefundWithAmountResult = GetRefundWithAmount(refundReference, payment, request);

            if (getRefundWithAmountResult.HasError)
            {
                return getRefundWithAmountResult.Error.ValueOrFailure();
            }

            Refund refund = getRefundWithAmountResult.Result.ValueOrFailure();


            ServiceResult<Refund, IActionResult> storeRequestedRefundResult = StoreRequestedRefund(refund);

            if (storeRequestedRefundResult.HasError)
            {
                return storeRequestedRefundResult.Error.ValueOrFailure();
            }


            ServiceResult<Merchant, IActionResult> retrieveMerchantResult = RetrieveMerchant(payment.MerchantId);

            if (retrieveMerchantResult.HasError)
            {
                return retrieveMerchantResult.Error.ValueOrFailure();
            }

            Merchant merchant = retrieveMerchantResult.Result.ValueOrFailure();


            ServiceResult<Payment, IActionResult> validatePaymentResult = ValidatePayment(payment);

            if (validatePaymentResult.HasError)
            {
                return validatePaymentResult.Error.ValueOrFailure();
            }


            ServiceResult<HttpStatusCode, IActionResult> createRefundResult = CreateRefund(merchant, payment, refund);

            if (createRefundResult.HasError)
            {
                return createRefundResult.Error.ValueOrFailure();
            }

            HttpStatusCode statusCode = createRefundResult.Result.ValueOrFailure();


            ServiceResult<HttpStatusCode, IActionResult> processResponseResult = ProcessResponse(statusCode);

            if (processResponseResult.HasError)
            {
                return processResponseResult.Error.ValueOrFailure();
            }


            IActionResult successfulResult = PrepareSuccessfulResult(refund.Amount);


            return successfulResult;
        }

        protected ServiceResult<Guid, IActionResult> GetReference(string id)
        {
            try
            {
                return ServiceResult<Guid, IActionResult>.WithSuccessStatus(new Guid(Base32.Decode(id)));
            }
            catch (ArgumentException)
            {
                _logger.LogWarning("Unable to extract guid from Id: {Id}.", id);
                return ServiceResult<Guid, IActionResult>.WithFailureStatus(StatusCode(StatusCodes.Status404NotFound));
            }
        }

        protected ServiceResult<Merchant, IActionResult> RetrieveMerchant(long merchantId)
        {
            Merchant merchant = _businessesRepository.Read(merchantId);

            if (merchant != null)
            {
                return ServiceResult<Merchant, IActionResult>.WithSuccessStatus(merchant);
            }

            _logger.LogWarning("Merchant with id: {merchantId} is not onboarded.", merchantId);

            return ServiceResult<Merchant, IActionResult>.WithFailureStatus(StatusCode(StatusCodes.Status403Forbidden));
        }

        protected ServiceResult<Payment, IActionResult> RetrievePayment(Guid paymentReference)
        {
            Payment payment = _paymentsRepository.Read(paymentReference);

            if (payment != null)
            {
                return ServiceResult<Payment, IActionResult>.WithSuccessStatus(payment);
            }

            _logger.LogWarning("Unable to find payment with paymentId: {Reference}.", paymentReference);
            return ServiceResult<Payment, IActionResult>.WithFailureStatus(StatusCode(StatusCodes.Status404NotFound));
        }
    
        private ServiceResult<Payment, IActionResult> ValidatePayment(Payment payment)
        {
            // Only initialzed payments (not pending or failed to initialize) are valid in the sense that they exist and retrievable from Klarna 
            return payment.Created ?
                ServiceResult<Payment, IActionResult>.WithSuccessStatus(payment) :
                ServiceResult<Payment, IActionResult>.WithFailureStatus(StatusCode(StatusCodes.Status403Forbidden));
        }

        private ServiceResult<Refund, IActionResult> GetRefundWithAmount(Guid refundReference, Payment payment, Models.CreateRefundRequest request)
        {
            int amount = default(int);

            int refundedAmount = _refundsRepository.GetRefundedAmountForPayment(payment.Reference);

            if (request.Amount.HasValue)
            {
                amount = request.Amount.Value;

                if (amount > (payment.Amount - refundedAmount))
                {
                    return ServiceResult<Refund, IActionResult>.WithFailureStatus(StatusCode(StatusCodes.Status403Forbidden));
                }
            }
            else
            {
                amount = payment.Amount - refundedAmount;
            }

            Refund refund = new Refund() { Reference = refundReference, Amount = amount };

            return ServiceResult<Refund, IActionResult>.WithSuccessStatus(refund);
        }

        private ServiceResult<Refund, IActionResult> StoreRequestedRefund(Refund refund)
        {
            if (_refundsRepository.Create(refund))
            {
                return ServiceResult<Refund, IActionResult>.WithSuccessStatus(refund);
            }

            _logger.LogWarning("Refund with reference: {Reference} already exists.", refund.Reference);

            return ServiceResult<Refund, IActionResult>.WithFailureStatus(StatusCode(StatusCodes.Status400BadRequest));
        }
   
        private ServiceResult<HttpStatusCode, IActionResult> CreateRefund(Merchant merchant, Payment payment, Refund refund)
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
                    return ServiceResult<HttpStatusCode, IActionResult>.WithFailureStatus(StatusCode(StatusCodes.Status500InternalServerError));
                }
            }

            return ServiceResult<HttpStatusCode, IActionResult>.WithSuccessStatus(createRefundStatus);
        }

        private ServiceResult<HttpStatusCode, IActionResult> ProcessResponse(HttpStatusCode createRefundResult)
        {
            switch (createRefundResult) {
                case HttpStatusCode.Created:
                    return ServiceResult<HttpStatusCode, IActionResult>.WithSuccessStatus(createRefundResult);

                case HttpStatusCode.Forbidden:
                    return ServiceResult<HttpStatusCode, IActionResult>.WithFailureStatus(StatusCode(StatusCodes.Status400BadRequest));

                case HttpStatusCode.BadRequest:
                    return ServiceResult<HttpStatusCode, IActionResult>.WithFailureStatus(StatusCode(StatusCodes.Status422UnprocessableEntity));

                case HttpStatusCode.NotFound:
                    return ServiceResult<HttpStatusCode, IActionResult>.WithFailureStatus(StatusCode(StatusCodes.Status502BadGateway));

                case HttpStatusCode.InternalServerError:
                    return ServiceResult<HttpStatusCode, IActionResult>.WithFailureStatus(StatusCode(StatusCodes.Status502BadGateway));

                case HttpStatusCode.RequestTimeout:
                    return ServiceResult<HttpStatusCode, IActionResult>.WithFailureStatus(StatusCode(StatusCodes.Status504GatewayTimeout));

                default:
                    return ServiceResult<HttpStatusCode, IActionResult>.WithFailureStatus(StatusCode(StatusCodes.Status500InternalServerError));
            }
        }

        private IActionResult PrepareSuccessfulResult(int refundedAmount)
        {
            RefundResponse response = new RefundResponse() { RefundedAmount = refundedAmount };

            return Accepted(response);
        }
    }
}
