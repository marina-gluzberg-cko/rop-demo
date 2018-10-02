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

namespace Rop.Demo.WebApi.Controllers.Methods
{
    [Route("methods/payments")]
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
            Guid paymentReference = default(Guid);
            Guid refundReference = default(Guid);

            try
            {
                paymentReference = GetReference(paymentId);
            }
            catch (ArgumentException)
            {
                _logger.LogWarning("Unable to extract guid from Id: {Id}.", paymentId);
                return StatusCode(StatusCodes.Status404NotFound);
            }

            try
            {
                refundReference = GetReference(refundId);
            }
            catch (ArgumentException)
            {
                _logger.LogWarning("Unable to extract guid from Id: {Id}.", refundId);
                return StatusCode(StatusCodes.Status404NotFound);
            }

            Payment payment = RetrievePayment(paymentReference);

            if (payment == null)
            {
                _logger.LogWarning("Unable to find payment with paymentId: {Reference}.", paymentReference);
                return StatusCode(StatusCodes.Status404NotFound);
            }

            Refund refund = GetRefundWithAmount(refundReference, payment, request);

            if (refund == null) 
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }

            if (!(StoreRequestedRefund(refund)))
            {
                _logger.LogWarning("Refund with reference: {Reference} already exists.", refund.Reference);

                return StatusCode(StatusCodes.Status400BadRequest);
            }

            Merchant merchant = RetrieveMerchant(payment.MerchantId);

            if (merchant == null)
            {
                _logger.LogWarning("Merchant with id: {merchantId} is not onboarded.", payment.MerchantId);
                return StatusCode(StatusCodes.Status403Forbidden);
            }

            if (!ValidatePayment(payment))
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }

            HttpStatusCode createRefundResult = CreateRefund(merchant, payment, refund);

            if (createRefundResult == HttpStatusCode.Created)
            {
                bool statusUpdateResult = _refundsRepository.SetStatusToCreated(refund.Reference);

                if (!statusUpdateResult)
                {
                    _logger.LogError("Refund with refundRed={refundReference} not found when setting status to Created.", refund.Reference);
                    return StatusCode(StatusCodes.Status500InternalServerError);
                }
            }
            else 
            {
                return ProcessResponse(createRefundResult);
            }

            return PrepareSuccessfulResult(refund.Amount);
        }

        protected Guid GetReference(string id)
        {
            return new Guid(Base32.Decode(id));
        }

        protected Merchant RetrieveMerchant(long merchantId)
        {
            return _businessesRepository.Read(merchantId);
        }

        protected Payment RetrievePayment(Guid paymentReference)
        {
            return _paymentsRepository.Read(paymentReference);
        }
    
        private bool ValidatePayment(Payment payment)
        {
            // Only initialzed payments (not pending or failed to initialize) are valid in the sense that they exist and retrievable from Klarna 
            return payment.Created;
        }

        private Refund GetRefundWithAmount(Guid refundReference, Payment payment, Models.CreateRefundRequest request)
        {
            int amount = default(int);

            int refundedAmount = _refundsRepository.GetRefundedAmountForPayment(payment.Reference);

            if (request.Amount.HasValue)
            {
                amount = request.Amount.Value;

                if (amount > (payment.Amount - refundedAmount))
                {
                    return null;
                }
            }
            else
            {
                amount = payment.Amount - refundedAmount;
            }

            return new Refund() { Reference = refundReference, Amount = amount };
        }

        private bool StoreRequestedRefund(Refund refund)
        {
            return _refundsRepository.Create(refund);
        }
   
        private HttpStatusCode CreateRefund(Merchant merchant, Payment payment, Refund refund)
        {
            return _refundApiClient.CreateRefund(
                merchant,
                payment.Reference,
                new Demo.Domain.ApiClient.Requests.CreateRefundRequest { RefundedAmount = refund.Amount });
        }

        private StatusCodeResult ProcessResponse(HttpStatusCode createRefundResult)
        {
            switch (createRefundResult) 
            {                
                case HttpStatusCode.Forbidden:
                    return StatusCode(StatusCodes.Status400BadRequest);

                case HttpStatusCode.BadRequest:
                    return StatusCode(StatusCodes.Status422UnprocessableEntity);

                case HttpStatusCode.NotFound:
                    return StatusCode(StatusCodes.Status502BadGateway);

                case HttpStatusCode.InternalServerError:
                    return StatusCode(StatusCodes.Status502BadGateway);

                case HttpStatusCode.RequestTimeout:
                    return StatusCode(StatusCodes.Status504GatewayTimeout);

                default:
                    return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }

        private IActionResult PrepareSuccessfulResult(int refundedAmount)
        {
            RefundResponse response = new RefundResponse() { RefundedAmount = refundedAmount };

            return Accepted(response);
        }
    }
}
