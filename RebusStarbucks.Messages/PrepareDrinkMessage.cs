using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RebusStarbucks.Messages
{
    public class PrepareDrinkMessage
    {
        public Guid CorrelationId { get; set; }
        public string Drink { get; set; }
        public string Name { get; set; }
    }
}
