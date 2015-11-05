using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Sagas;
using RebusStarbucks.Messages;
using System;
using System.Threading.Tasks;

namespace RebusStarbucks.Client
{
    public class CustomerSaga : Saga<CustomerSagaData>,
        IAmInitiatedBy<PaymentDueMessage>,
        IHandleMessages<DrinkReadyMessage>
    {
        private readonly IBus _bus;
        public CustomerSaga(IBus bus)
        {
            _bus = bus;
        }

        protected override void CorrelateMessages(ICorrelationConfig<CustomerSagaData> config)
        {
            config.Correlate<PaymentDueMessage>(x => x.CorrelationId, x => x.ClientId);
            config.Correlate<DrinkReadyMessage>(x => x.CorrelationId, x => x.OtherId);
        }

        public async Task Handle(DrinkReadyMessage message)
        {
            if (Data.CurrentState != CustomerSagaData.State.WaitingForDrink)
            {
                return;
            }

            Extensions.ActionWithCyan(() => {
                Console.WriteLine("{0} got his/her {1}!", message.Name, message.Drink);
            });
            MarkAsComplete();
        }

        public async Task Handle(PaymentDueMessage message)
        {
            if(!IsNew)
            {
                return;
            }

            Extensions.ActionWithCyan(() => {
                Console.WriteLine("Oh yeah...  gotta pay");
            });

            var submitPaymentMessage = new SubmitPaymentMessage
            {
                CorrelationId = message.CorrelationId,
                PaymentType = PaymentType.CreditCard,
                Amount = message.Amount*1.2m
            };

            await _bus.Reply(submitPaymentMessage);
            Data.ClientId = message.CorrelationId;
            Data.OtherId = message.CorrelationId;
            Data.CurrentState = CustomerSagaData.State.WaitingForDrink;
        }

        
    }

    public class CustomerSagaData : ISagaData
    {
        public Guid Id { get; set; }

        public int Revision { get; set; }

        public Guid ClientId { get; set; }

        public Guid OtherId { get; set; }
        public State CurrentState { get; set; }


        public enum State
        {
            WaitingForAmount, WaitingForDrink
        }
    }
}
