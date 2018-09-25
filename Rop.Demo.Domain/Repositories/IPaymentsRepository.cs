using Rop.Demo.Domain.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Rop.Demo.Domain.Repositories
{
    public interface IPaymentsRepository
    {
        bool Create(Payment payment);

        Payment Read(Guid paymentReference);
    }
}
