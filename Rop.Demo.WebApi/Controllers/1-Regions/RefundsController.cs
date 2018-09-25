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

namespace Rop.Demo.WebApi.Controllers.Areas
{
    [Route("areas/payments")]
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
            #region Get Payment Reference
            Guid paymentReference = default(Guid);
            Guid refundReference = default(Guid);

            try
            {
                paymentReference = new Guid(Base32.Decode(paymentId));
            }
            catch (ArgumentException)
            {
                _logger.LogWarning("Unable to extract guid from Id: {Id}.", paymentId);
                return StatusCode(StatusCodes.Status404NotFound);
            }
            #endregion

            #region Get Refund Reference
            try
            {
                refundReference = new Guid(Base32.Decode(refundId));
            }
            catch (ArgumentException)
            {
                _logger.LogWarning("Unable to extract guid from Id: {Id}.", refundId);
                return StatusCode(StatusCodes.Status404NotFound);
            }
            #endregion

            #region GetRefundWithAmount
            Payment payment = _paymentsRepository.Read(paymentReference);

            if (payment == null)
            {
                _logger.LogWarning("Unable to find payment with paymentId: {Reference}.", paymentReference);
                return StatusCode(StatusCodes.Status404NotFound);
            }
            #endregion

            #region Get Refund With Amount
            int amount = default(int);

            int refundedAmount = default(int);

            if (request.Amount.HasValue)
            {
                amount = request.Amount.Value;
            }
            else
            {
                refundedAmount = _refundsRepository.GetRefundedAmountForPayment(payment.Reference);

                amount = payment.Amount - refundedAmount;

                if (amount < 0)
                {
                    return StatusCode(StatusCodes.Status403Forbidden);
                }
            }

            Refund refund = new Refund() { Reference = refundReference, Amount = amount };
            #endregion

            #region Store Requested Refund
            if (!_refundsRepository.Create(refund))
            {
                _logger.LogWarning("Refund with reference: {Reference} already exists.", refund.Reference);

                return StatusCode(StatusCodes.Status422UnprocessableEntity);
            }
            #endregion

            #region Retrieve Merchant
            long merchantId = payment.MerchantId;

            Merchant merchant = _businessesRepository.Read(payment.MerchantId);

            if (merchant == null)
            {
                _logger.LogWarning("Merchant with id: {merchantId} is not onboarded.", merchantId);

                return StatusCode(StatusCodes.Status403Forbidden);
            }
            #endregion

            #region Validate Payment
            if (!payment.Created)
            {
                return StatusCode(StatusCodes.Status404NotFound);
            }
            #endregion

            #region Create Refund
            HttpStatusCode createRefundStatus = _refundApiClient.CreateRefund(
                merchant,
                payment.Reference,
                new Domain.ApiClient.Requests.CreateRefundRequest { RefundedAmount = refund.Amount });

            bool successful = createRefundStatus == HttpStatusCode.Created;

            if (successful)
            {
                bool statusUpdateResult = _refundsRepository.SetStatusToCreated(refund.Reference);

                if (!statusUpdateResult)
                {
                    _logger.LogError("Refund with refundRef={refundReference} not found when setting status to Created.", refund.Reference);
                    return StatusCode(StatusCodes.Status500InternalServerError);
                }
            }
            #endregion

            #region Process Response
            switch (createRefundStatus)
            {
                case HttpStatusCode.Created:
                    break;

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
            #endregion

            #region Prepare Successful Result
            RefundResponse response = new RefundResponse() { RefundedAmount = refundedAmount };

            return Accepted(response);
            #endregion
        }
    }
}
