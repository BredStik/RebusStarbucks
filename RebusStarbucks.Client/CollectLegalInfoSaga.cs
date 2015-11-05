using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Sagas;
using System;
using System.Threading.Tasks;

namespace RebusStarbucks.Client
{
    public class CollectLegalInfoSagaData : ISagaData
    {

        public Guid Id { get; set; }

        public int Revision { get; set; }

        public Guid CorrelationId { get; set; }

        public bool GotAnswerFromSystemOne { get; set; }
        public bool GotAnswerFromSystemTwo { get; set; }
    }

    public class CollectLegalInfoSaga : Saga<CollectLegalInfoSagaData>,
        IAmInitiatedBy<StartProcess>,
        IHandleMessages<GetInfoFromSystemOne>,
        IHandleMessages<GetInfoFromSystemTwo>,
        IHandleMessages<InfoFromSystemOneReceived>,
        IHandleMessages<InfoFromSystemTwoReceived>
    {
        private readonly IBus _bus;

        public CollectLegalInfoSaga(IBus bus)
        {
            _bus = bus;
        }

        public async Task Handle(GetInfoFromSystemTwo message)
        {
            await Task.Delay(TimeSpan.FromSeconds(5));
            await _bus.SendLocal(new InfoFromSystemTwoReceived { CorrelationId = message.CorrelationId });
        }

        public async Task Handle(InfoFromSystemTwoReceived message)
        {
            Data.GotAnswerFromSystemTwo = true;

            CompleteIfDone();
        }

        private void CompleteIfDone()
        {
            if(Data.GotAnswerFromSystemOne && Data.GotAnswerFromSystemTwo)
            {
                MarkAsComplete();
            }
        }

        public async Task Handle(InfoFromSystemOneReceived message)
        {
            Data.GotAnswerFromSystemOne = true;

            CompleteIfDone();
        }

        public async Task Handle(GetInfoFromSystemOne message)
        {
            await Task.Delay(TimeSpan.FromSeconds(10));
            await _bus.SendLocal(new InfoFromSystemOneReceived { CorrelationId = message.CorrelationId });
        }

        protected override void CorrelateMessages(ICorrelationConfig<CollectLegalInfoSagaData> config)
        {
            config.Correlate<StartProcess>(x => x.CorrelationId, x => x.CorrelationId);
            config.Correlate<GetInfoFromSystemOne>(x => x.CorrelationId, x => x.CorrelationId);
            config.Correlate<GetInfoFromSystemTwo>(x => x.CorrelationId, x => x.CorrelationId);
            config.Correlate<InfoFromSystemOneReceived>(x => x.CorrelationId, x => x.CorrelationId);
            config.Correlate<InfoFromSystemTwoReceived>(x => x.CorrelationId, x => x.CorrelationId);
        }

        public async Task Handle(StartProcess message)
        {
            Data.CorrelationId = message.CorrelationId;

            await _bus.SendLocal(new GetInfoFromSystemOne { CorrelationId = message.CorrelationId });
            await _bus.SendLocal(new GetInfoFromSystemTwo { CorrelationId = message.CorrelationId });
        }
    }

    public class GetInfoFromSystemOne
    {
        public Guid CorrelationId { get; set; }
    }

    public class InfoFromSystemOneReceived
    {
        public Guid CorrelationId { get; set; }
    }

    public class GetInfoFromSystemTwo
    {
        public Guid CorrelationId { get; set; }
    }

    public class InfoFromSystemTwoReceived
    {
        public Guid CorrelationId { get; set; }
    }

    public class StartProcess
    {
        public Guid CorrelationId { get; set; }
    }
}
