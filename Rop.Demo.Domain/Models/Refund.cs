using System;
using System.Collections.Generic;
using System.Text;

namespace Rop.Demo.Domain.Models
{
    public class Refund
    {
        public Guid Reference { get; set; }
        public int Amount { get; set; }
        public bool Created { get; set; }
    }
}
