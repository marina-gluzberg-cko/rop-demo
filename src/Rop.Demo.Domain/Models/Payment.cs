using System;

namespace Rop.Demo.Domain.Models
{
    public class Payment
    {
        public Guid Reference { get; set; }
        public long MerchantId { get; set; }
        public int Amount { get; set; }
        public bool Created { get; set; }
    }
}
