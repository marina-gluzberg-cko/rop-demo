using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using Rop.Demo.Domain.ApiClient;
using Rop.Demo.Domain.ApiClient.Requests;
using Rop.Demo.Domain.Domain;
using Rop.Demo.Domain.Models;
using Rop.Demo.Domain.Repositories;
using Rop.Demo.Helpers;
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Linq;

namespace Rop.Demo
{
    class Program
    {
        static Mock<IMerchantsRepository> _merchantsRepository = new Mock<IMerchantsRepository>();
        static Mock<IPaymentsRepository> _paymentsRepository = new Mock<IPaymentsRepository>();
        static Mock<IRefundsRepository> _refundsRepository = new Mock<IRefundsRepository>();
        static Mock<IRefundApiClient> _refundApiClient = new Mock<IRefundApiClient>();

        public static void Main(string[] args)
        {
            Guid paymentReference = Guid.NewGuid();
            Guid refundReference = Guid.NewGuid();
            long merchantId = 100;

            Payment payment = new Payment() { Reference = paymentReference, Amount = 100, Created = true, MerchantId = merchantId };
            Refund refund = new Refund() { Reference = refundReference, Amount = payment.Amount };
            Merchant merchant = new Merchant() { Id = merchantId };

            _paymentsRepository.Setup(x => x.Read(paymentReference)).Returns(payment);
            _refundsRepository.Setup(x => x.Create(It.IsAny<Refund>())).Returns(true);
            _refundsRepository.Setup(x => x.SetStatusToCreated(refund.Reference)).Returns(true);
            _refundsRepository.Setup(x => x.GetRefundedAmountForPayment(paymentReference)).Returns(100);
            _merchantsRepository.Setup(x => x.Read(merchantId)).Returns(merchant);
            _refundApiClient.Setup(x => x.CreateRefund(It.IsAny<Merchant>(), paymentReference, It.IsAny<CreateRefundRequest>())).Returns(HttpStatusCode.Created);

            string paymentId = Base32.Encode(paymentReference.ToByteArray());
            string captureId = Base32.Encode(refundReference.ToByteArray());
            var request = new { amount = (int?)null };

            string opt = args.Any() ? args[0] : "methods";

            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(
                HttpMethod.Put,
                $"/{opt}/payments/{paymentId}/refunds/{captureId}")
            { Content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json") };

            HttpResponseMessage response = GetMockClient().SendAsync(httpRequestMessage).GetAwaiter().GetResult();

            Console.WriteLine($"Response: {response.StatusCode}");
        }

        private static HttpClient GetMockClient()
        {
            HttpClient httpClient;


            TestServer testServer = TestHelper.CreateInternalTestServer(
                configureServices: x =>
                {
                    x.AddSingleton(_paymentsRepository.Object);
                    x.AddSingleton(_merchantsRepository.Object);
                    x.AddSingleton(_refundsRepository.Object);
                    x.AddSingleton(_refundApiClient.Object);
                });

            httpClient = testServer.CreateClient();
            httpClient.DefaultRequestHeaders.Add("ContentType", "application/json");

            return httpClient;
        }
    }
}
