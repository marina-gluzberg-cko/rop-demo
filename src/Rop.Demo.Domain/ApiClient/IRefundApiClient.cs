using Rop.Demo.Domain.ApiClient.Requests;
using Rop.Demo.Domain.Models;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Rop.Demo.Domain.ApiClient
{
    public interface IRefundApiClient
    {
        HttpStatusCode CreateRefund(Merchant merchant, Guid paymentReference, CreateRefundRequest createRefundRequest);
    }
}
