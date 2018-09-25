using Rop.Demo.Domain.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Rop.Demo.Domain.Repositories
{
    public interface IRefundsRepository
    {
        bool Create(Refund refund);
        Refund Read(Guid refundReference);
        bool SetStatusToCreated(Guid refundReference);
        int GetRefundedAmountForPayment(Guid reference);
    }
}
