using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Pipeline;
using Rebus.Sagas;
using RebusStarbucks.Messages;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RebusStarbucks.Barista
{
    public class BaristaSaga : Saga<BaristaSagaData>,
      IAmInitiatedBy<NewOrderMessage>,
      IHandleMessages<PaymentCompleteMessage>,
        IHandleMessages<DrinkReadyMessage>
    {
        private readonly IBus _bus;
        private readonly IMessageContext _context;

        public BaristaSaga(IBus bus, IMessageContext context)
        {
            _bus = bus;
            _context = context;
        }

        protected override void CorrelateMessages(ICorrelationConfig<BaristaSagaData> config)
        {
            // ensure idempotency by setting up correlation for this one in addition to
            // allowing CustomerCreated to initiate a new saga instance
            config.Correlate<NewOrderMessage>(m => m.CorrelationId, d => d.ClientId);

            // ensure proper correlation for the other messages
            config.Correlate<PaymentCompleteMessage>(m => m.CorrelationId, d => d.ClientId);
            config.Correlate<DrinkReadyMessage>(m => m.CorrelationId, d => d.ClientId);
        }

        public async Task Handle(PaymentCompleteMessage message)
        {
            Data.ReceivedPayment = true;

            //if (Data.CurrentState != BaristaSagaData.State.WaitingForPayment)
            //{
            //    MarkAsUnchanged();

            //    //drink not ready, yet try again later
            //    Console.WriteLine("Well, the drink is not ready yet...");

            //    var nbRetries = 0;

            //    if(_context.Message.Headers.ContainsKey("nbRetries"))
            //    {
            //        nbRetries = Convert.ToInt32(_context.Message.Headers["nbRetries"]) + 1;
            //    }

            //    if (nbRetries > 10)
            //    {
            //        //todo: handle too many retries
            //        Console.WriteLine("You told me too many times...");
            //        return;
            //    }

            //    var headers = new Dictionary<string, string>
            //    {
            //        {"nbRetries", nbRetries.ToString()}
            //    };

            //    await _bus.Defer(TimeSpan.FromSeconds(1), message, headers);
            //    return;
            //}
            var color = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("Payment Complete for '{0}' got it!", Data.Name);
            Console.ForegroundColor = color;
            await CompleteIfDone();
        }

        public async Task Handle(NewOrderMessage message)
        {
            if(!IsNew)
            {
                return;
            }
            Data.ClientId = message.CorrelationId;
            Data.Name = message.Name;
            Data.Drink = string.Format("{0} {1}", message.Size, message.Item);
            var color = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(string.Format("{0} for {1}, got it!", Data.Drink, Data.Name));
            Console.ForegroundColor = color;
            //await _bus.Defer(TimeSpan.FromSeconds(2), new PrepareDrinkMessage { CorrelationId = message.CorrelationId });
            await _bus.SendLocal(new PrepareDrinkMessage { CorrelationId = message.CorrelationId, Drink = message.Item, Name = message.Name });

            Data.CurrentState = BaristaSagaData.State.AwatingPrepareDrink;
        }
        

        public async Task Handle(DrinkReadyMessage message)
        {
            Data.CurrentState = BaristaSagaData.State.DrinkDone;

            await CompleteIfDone();
        }

        private async Task CompleteIfDone()
        {
            if(Data.ReceivedPayment && Data.CurrentState == BaristaSagaData.State.DrinkDone)
            {
                var color = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine(string.Format("I've got a {0} ready for {1}!", Data.Drink, Data.Name));
                Console.ForegroundColor = color;
                var drinkReadyMessage = new DrinkReadyMessage
                {
                    CorrelationId = Data.ClientId,
                    Drink = Data.Drink,
                    Name = Data.Name
                };

                await _bus.Publish(drinkReadyMessage);
                MarkAsComplete();
            }
        }
    }

    public class BaristaSagaData : ISagaData
    {
        public Guid Id { get; set; }
        public int Revision { get; set; }

        public Guid ClientId { get; set; }
        public string Drink { get; set; }
        public string Name { get; set; }
        public State CurrentState { get; set; }
        public enum State
        {
            JustWaiting, AwatingPrepareDrink, DrinkDone
        }

        public bool ReceivedPayment { get; set; }
    }
}
