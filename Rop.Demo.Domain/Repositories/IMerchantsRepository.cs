using Rop.Demo.Domain.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Rop.Demo.Domain.Repositories
{
    public interface IMerchantsRepository
    {
        bool Create(Merchant payment);

        Merchant Read(long merchandId);
    }
}
