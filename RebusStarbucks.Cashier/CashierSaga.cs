using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Sagas;
using RebusStarbucks.Messages;
using System;
using System.Threading.Tasks;

namespace RebusStarbucks.Cashier
{
    public class CashierSaga : Saga<CashierSagaData>,
      IAmInitiatedBy<NewOrderMessage>,
      IHandleMessages<SubmitPaymentMessage>
    {
        private readonly IBus _bus;

        public CashierSaga(IBus bus)
        {
            _bus = bus;
        }

        protected override void CorrelateMessages(ICorrelationConfig<CashierSagaData> config)
        {
            // ensure idempotency by setting up correlation for this one in addition to
            // allowing CustomerCreated to initiate a new saga instance
            config.Correlate<NewOrderMessage>(m => m.CorrelationId, d => d.ClientId);

            // ensure proper correlation for the other messages
            config.Correlate<SubmitPaymentMessage>(m => m.CorrelationId, d => d.ClientId);
        }

        public async Task Handle(SubmitPaymentMessage message)
        {
            //only handle this message when waiting for a payment
            if(Data.CurrentState != CashierSagaData.State.WaitingForPayment)
            {
                return;
            }

            var color = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Cyan;

            if (message.Amount > Data.Price)
            {
                Console.WriteLine("Thanks for the tip!");
            }
            else if (message.Amount < Data.Price)
            {
                Console.WriteLine("What are you, some kind of charity case?");
            }

            var paymentType = message.PaymentType;

            Console.WriteLine("Received a payment of {0} for {1} ({2})", paymentType, Data.ItemOrdered, Data.Size);

            if (paymentType == PaymentType.CreditCard)
            {
                Console.Write("Authorizing Card...");
                await Task.Delay(4000);
                Console.WriteLine("done!");
            }

            var completeMessage = new PaymentCompleteMessage
            {
                CorrelationId = message.CorrelationId,
            };

            await _bus.Send(completeMessage);

            MarkAsComplete();

            Console.ForegroundColor = color;
        }

        public async Task Handle(NewOrderMessage message)
        {
            if(!IsNew)
            {
                return;
            }

            // store the CRM customer ID in the saga
            Data.ClientId = message.CorrelationId;
            Data.Name = message.Name;
            Data.ItemOrdered = message.Item;
            Data.Size = message.Size;
            Data.Price = GetPriceForSize(message.Size);

            // command that legal information be acquired for the customer
            await _bus.Reply(new PaymentDueMessage
            {
                CorrelationId = Data.ClientId,
                Amount = Data.Price
            });

            Data.CurrentState = CashierSagaData.State.WaitingForPayment;
        }

        private decimal GetPriceForSize(string size)
        {
            switch (size.ToLower())
            {
                case "tall":
                    return 3.25m;
                case "grande":
                    return 4.00m;
                case "venti":
                    return 4.75m;
                default:
                    throw new Exception(string.Format("We don't have that size ({0})", size));
            }
        }
    }

    public class CashierSagaData : ISagaData
    {
        // these two are required by Rebus
        public Guid Id { get; set; }
        public int Revision { get; set; }

        public Guid ClientId { get; set; }
        public string Name { get; set; }
        public string ItemOrdered { get; set; }
        public string Size { get; set; }
        public decimal Price { get; set; }

        public State CurrentState { get; set; }

        public enum State
        {
            Initial, WaitingForPayment
        }
    }
}
