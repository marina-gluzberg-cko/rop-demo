using AutoFixture;
using Moq;
using NSpec;
using Rop.Demo.Domain.Domain;
using Rop.Demo.Domain.Repositories;
using SemanticComparison.Fluent;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.TestHost;
using Rop.Demo.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Rop.Demo.Domain.ApiClient;
using Rop.Demo.Domain.Models;
using Newtonsoft.Json;
using Rop.Demo.Domain.ApiClient.Requests;

namespace Rop.Demo.WebApi.Tests.Integration.Refunds
{
    abstract class describe_ReorderBaseRefunds : nspec
    {
        protected IFixture _fixture = new Fixture();

        protected HttpClient _httpClient;

        protected readonly Mock<IMerchantsRepository> _merchantsRepository = new Mock<IMerchantsRepository>();
        protected readonly Mock<IPaymentsRepository> _paymentsRepository = new Mock<IPaymentsRepository>();
        protected readonly Mock<IRefundsRepository> _refundsRepository = new Mock<IRefundsRepository>();
        private readonly Mock<IRefundApiClient> _refundApiClient = new Mock<IRefundApiClient>();

        protected string _paymentId = null;
        protected string _refundId = null;
        
        protected int? _amount = null;

        protected abstract string Opt { get; }

        protected HttpRequestMessage GetHttpRequestMessage() =>
            new HttpRequestMessage(HttpMethod.Put, $"{Opt}/payments/{_paymentId}/refunds/{_refundId}")
            { Content = new StringContent(JsonConvert.SerializeObject(new { amount = _amount }), Encoding.UTF8, "application/json") };

        protected HttpResponseMessage _httpResponse = null;

        void before_each()
        {
            _fixture = new Fixture();

            _merchantsRepository.Reset();
            _paymentsRepository.Reset();
            _refundsRepository.Reset();
            _refundApiClient.Reset();
        }

        void before_all()
        {
            TestServer testServer = TestHelper.CreateInternalTestServer(
                configureServices: x =>
                {
                    x.AddSingleton(_paymentsRepository.Object);
                    x.AddSingleton(_merchantsRepository.Object);
                    x.AddSingleton(_refundsRepository.Object);
                    x.AddSingleton(_refundApiClient.Object);
                });

            _httpClient = testServer.CreateClient();
            _httpClient.DefaultRequestHeaders.Add("ContentType", "application/json");
        }

        async Task act_each()
        {
            HttpRequestMessage httpRequestMessage = GetHttpRequestMessage();

            try
            {
                _httpResponse = await _httpClient.SendAsync(httpRequestMessage);
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        void given_invalid_payment_id()
        {
            before = () =>
            {
                _paymentId = _fixture.Create<string>();
                _refundId = _fixture.Create<string>();
            };

            it[$"response code should be {HttpStatusCode.NotFound}"] = () =>
            {
                _httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            };
        }

        void given_non_existing_payment()
        {
            before = () =>
            {
                Guid paymentRef = Guid.NewGuid();

                _paymentId = Base32.Encode(paymentRef.ToByteArray());
                _refundId = _fixture.Create<string>();

                _paymentsRepository.Setup(x => x.Read(paymentRef)).Returns(null as Payment);
            };

            it[$"response code should be {HttpStatusCode.NotFound}"] = () =>
            {
                _httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            };
        }

        void given_not_created_payment()
        {
            before = () =>
            {
                Guid paymentRef = Guid.NewGuid();
                Guid refundRef = Guid.NewGuid();

                _paymentId = Base32.Encode(paymentRef.ToByteArray());
                _refundId = Base32.Encode(refundRef.ToByteArray());

                _fixture.Customize<Payment>(c => c.With(p => p.Reference, paymentRef).With(p => p.Created, false));

                Payment payment = _fixture.Create<Payment>();

                _paymentsRepository.Setup(x => x.Read(paymentRef)).Returns(payment);
            };

            it[$"response code should be {HttpStatusCode.Forbidden}"] = () =>
            {
                _httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            };
        }

        void given_not_onboarded_merchant()
        {
            before = () =>
            {
                Guid paymentRef = Guid.NewGuid();
                Guid refundRef = Guid.NewGuid();

                _paymentId = Base32.Encode(paymentRef.ToByteArray());
                _refundId = Base32.Encode(refundRef.ToByteArray());

                _fixture.Customize<Payment>(c => c.With(p => p.Reference, paymentRef));

                Payment payment = _fixture.Create<Payment>();

                _paymentsRepository.Setup(x => x.Read(paymentRef)).Returns(payment);

                _merchantsRepository.Setup(x => x.Read(payment.MerchantId)).Returns(null as Merchant);
            };

            it[$"response code should be {HttpStatusCode.Forbidden}"] = () =>
            {
                _httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            };
        }

        void given_invalid_refund_id()
        {
            before = () =>
            {
                Guid paymentRef = Guid.NewGuid();

                _paymentId = Base32.Encode(paymentRef.ToByteArray());
                _refundId = _fixture.Create<string>();

                _fixture.Customize<Payment>(c => c.With(p => p.Reference, paymentRef));

                Payment payment = _fixture.Create<Payment>();

                _fixture.Customize <Merchant>(c => c.With(p => p.Id, payment.MerchantId));

                _paymentsRepository.Setup(x => x.Read(paymentRef)).Returns(payment);

                _merchantsRepository.Setup(x => x.Read(payment.MerchantId)).Returns(_fixture.Create<Merchant>());
            };

            it[$"response code should be {HttpStatusCode.NotFound}"] = () =>
            {
                _httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            };
        }

        void given_exceeding_amount()
        {
            before = () =>
            {
                Guid paymentRef = Guid.NewGuid();

                _paymentId = Base32.Encode(paymentRef.ToByteArray());
                _refundId = Base32.Encode(Guid.NewGuid().ToByteArray());

                _fixture.Customize<Payment>(c => c.With(p => p.Reference, paymentRef));

                Payment payment = _fixture.Create<Payment>();

                _fixture.Customize<Merchant>(c => c.With(p => p.Id, payment.MerchantId));

                _paymentsRepository.Setup(x => x.Read(paymentRef)).Returns(payment);

                _merchantsRepository.Setup(x => x.Read(payment.MerchantId)).Returns(_fixture.Create<Merchant>());

                _refundsRepository.Setup(x => x.GetRefundedAmountForPayment(paymentRef)).Returns((int)(0.5 * payment.Amount));

                _amount = (int)(payment.Amount * 0.7);
            };

            it[$"response code should be {HttpStatusCode.Forbidden}"] = () =>
            {
                _httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            };
        }

        void given_already_existing_refund_id()
        {
            before = () =>
            {
                Guid paymentRef = Guid.NewGuid();
                Guid refundRef = Guid.NewGuid();

                _paymentId = Base32.Encode(paymentRef.ToByteArray());
                _refundId = Base32.Encode(refundRef.ToByteArray());

                _fixture.Customize<Payment>(c => c.With(p => p.Reference, paymentRef));

                Payment payment = _fixture.Create<Payment>();

                _fixture.Customize<Merchant>(c => c.With(p => p.Id, payment.MerchantId));

                _paymentsRepository.Setup(x => x.Read(paymentRef)).Returns(payment);

                _merchantsRepository.Setup(x => x.Read(payment.MerchantId)).Returns(_fixture.Create<Merchant>());

                _refundsRepository.Setup(x => x.GetRefundedAmountForPayment(paymentRef)).Returns((int)(0.5 * payment.Amount));

                _amount = (int)(payment.Amount * 0.5);

                _refundsRepository.Setup(x => x.Create(It.IsAny<Refund>())).Returns(false);
            };

            it[$"response code should be {HttpStatusCode.UnprocessableEntity}"] = () =>
            {
                _httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            };
        }

        void given_refund_creation_was_successful_but_status_update_failed()
        {
            before = () =>
            {
                Guid paymentRef = Guid.NewGuid();
                Guid refundRef = Guid.NewGuid();

                _paymentId = Base32.Encode(paymentRef.ToByteArray());
                _refundId = Base32.Encode(refundRef.ToByteArray());

                _fixture.Customize<Payment>(c => c.With(p => p.Reference, paymentRef).With(p => p.Created, true));

                Payment payment = _fixture.Create<Payment>();

                _paymentsRepository.Setup(x => x.Read(paymentRef)).Returns(payment);

                _refundsRepository.Setup(x => x.GetRefundedAmountForPayment(paymentRef)).Returns((int)(0.5 * payment.Amount));

                _amount = (int)(payment.Amount * 0.5);

                _refundsRepository.Setup(x => x.Create(It.IsAny<Refund>())).Returns(true);


                _fixture.Customize<Merchant>(c => c.With(p => p.Id, payment.MerchantId));

                Merchant merchant = _fixture.Create<Merchant>();

                _merchantsRepository.Setup(x => x.Read(payment.MerchantId)).Returns(merchant);

                _refundApiClient
                    .Setup(x => x.CreateRefund(merchant, paymentRef, It.IsAny<CreateRefundRequest>()))
                    .Returns(HttpStatusCode.Created);

                _refundsRepository.Setup(x => x.SetStatusToCreated(refundRef)).Returns(false);
            };

            it[$"response code should be {HttpStatusCode.InternalServerError}"] = () =>
            {
                _httpResponse.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
            };
        }

        void given_refund_creation_was_unsuccessful()
        {
            Guid paymentRef = default(Guid);
            Merchant merchant = null;

            before = () =>
            {
                paymentRef = Guid.NewGuid();
                Guid refundRef = Guid.NewGuid();

                _paymentId = Base32.Encode(paymentRef.ToByteArray());
                _refundId = Base32.Encode(refundRef.ToByteArray());

                _fixture.Customize<Payment>(c => c.With(p => p.Reference, paymentRef).With(p => p.Created, true));

                Payment payment = _fixture.Create<Payment>();

                _paymentsRepository.Setup(x => x.Read(paymentRef)).Returns(payment);

                _refundsRepository.Setup(x => x.GetRefundedAmountForPayment(paymentRef)).Returns((int)(0.5 * payment.Amount));

                _amount = (int)(payment.Amount * 0.5);

                _refundsRepository.Setup(x => x.Create(It.IsAny<Refund>())).Returns(true);


                _fixture.Customize<Merchant>(c => c.With(p => p.Id, payment.MerchantId));

                merchant = _fixture.Create<Merchant>();

                _merchantsRepository.Setup(x => x.Read(payment.MerchantId)).Returns(merchant);

                _refundsRepository.Setup(x => x.SetStatusToCreated(paymentRef)).Returns(true);
            };

            new Each<HttpStatusCode, HttpStatusCode>()
            {
                { HttpStatusCode.Forbidden, HttpStatusCode.BadRequest },
                { HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity },
                { HttpStatusCode.NotFound, HttpStatusCode.BadGateway },
                { HttpStatusCode.InternalServerError, HttpStatusCode.BadGateway },
                { HttpStatusCode.RequestTimeout, HttpStatusCode.GatewayTimeout }
            }.Do((apiStatusCode, expectedstatusCode) =>
            {
                context[$"when the api returns {apiStatusCode}"] = () =>
                {
                    before = () =>
                    {
                        _refundApiClient
                            .Setup(x => x.CreateRefund(merchant, paymentRef, It.IsAny<CreateRefundRequest>()))
                            .Returns(apiStatusCode);
                    };

                    it[$"response code should be {expectedstatusCode}"] = () =>
                    {
                        _httpResponse.StatusCode.ShouldBe(expectedstatusCode);
                    };
                };
            });
        }

        void given_successful_response()
        {
            before = () =>
            {
                Guid paymentRef = Guid.NewGuid();
                Guid refundRef = Guid.NewGuid();

                _paymentId = Base32.Encode(paymentRef.ToByteArray());
                _refundId = Base32.Encode(refundRef.ToByteArray());

                _fixture.Customize<Payment>(c => c.With(p => p.Reference, paymentRef).With(p => p.Created, true));

                Payment payment = _fixture.Create<Payment>();

                _paymentsRepository.Setup(x => x.Read(paymentRef)).Returns(payment);

                _refundsRepository.Setup(x => x.GetRefundedAmountForPayment(paymentRef)).Returns((int)(0.5 * payment.Amount));

                _amount = (int)(payment.Amount * 0.5);

                _refundsRepository.Setup(x => x.Create(It.IsAny<Refund>())).Returns(true);


                _fixture.Customize<Merchant>(c => c.With(p => p.Id, payment.MerchantId));

                Merchant merchant = _fixture.Create<Merchant>();

                _merchantsRepository.Setup(x => x.Read(payment.MerchantId)).Returns(merchant);

                _refundApiClient
                    .Setup(x => x.CreateRefund(merchant, paymentRef, It.IsAny<CreateRefundRequest>()))
                    .Returns(HttpStatusCode.Created);

                _refundsRepository.Setup(x => x.SetStatusToCreated(refundRef)).Returns(true);
            };

            it[$"response code should be {HttpStatusCode.Accepted}"] = () =>
            {
                _httpResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);
            };
        }
    }
}
