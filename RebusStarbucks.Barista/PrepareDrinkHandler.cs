using Rebus.Bus;
using Rebus.Handlers;
using RebusStarbucks.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RebusStarbucks.Barista
{
    public class PrepareDrinkHandler : IHandleMessages<PrepareDrinkMessage>
    {
        private readonly IBus _bus;

        public PrepareDrinkHandler(IBus bus)
        {
            _bus = bus;
        }

        public async Task Handle(PrepareDrinkMessage message)
        {
            for (int i = 0; i < 10; i++)
            {
                await Task.Delay(i * 200);
                Console.WriteLine("[wwhhrrrr....psssss...chrhrhrhrrr]");
            }

            await _bus.Reply(new DrinkReadyMessage { CorrelationId = message.CorrelationId });
        }
    }
}
