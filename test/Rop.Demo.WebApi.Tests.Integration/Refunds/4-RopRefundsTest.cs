using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Rop.Demo.WebApi.Tests.Integration.Refunds
{
    class describe_RopRefunds : describe_ReorderBaseRefunds
    {
        protected override string Opt => "rop";
    }
}
