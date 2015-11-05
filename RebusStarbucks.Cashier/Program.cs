using Rebus.Activation;
using Rebus.Config;
using Rebus.Encryption;
using Rebus.Routing.TypeBased;
using Rebus.SagaStorage;
using Rebus.Transport.FileSystem;
using Rebus.Transport.Msmq;
using RebusStarbucks.Messages;
using System;
using System.IO;

namespace RebusStarbucks.Cashier
{
    class Program
    {
        static readonly string messageQueueFilePath = @"..\..\..\RebusStarbucks.Client\bin\Debug\MessageQueues";
        static void Main(string[] args)
        {
            using (var activator = new BuiltinHandlerActivator())
            {
                var bus = Configure.With(activator)
                            .Transport(t => t.UseFileSystem(messageQueueFilePath, "rebusStarbucks.cashier"))
                            //.Transport(t => t.UseMsmq("rebusStarbucks.cashier"))
                            //.Transport(t => { t.Register(context => new Msmq.MsmqTransport("rebusStarbucks.cashier", true)); })
                            .Routing(r =>
                            {
                                r.TypeBased()
                                .Map<PaymentDueMessage>("rebusStarbucks.client")
                                .Map<NewOrderMessage>("rebusStarbucks.client")
                                .Map<PaymentCompleteMessage>("rebusStarbucks.barista");
                            })
                            .Options(op => {
                                op.SetMaxParallelism(1).EnableEncryption("VW6DcqJioLHnV1b9oPnDFCYAGB7VxJcY");
                            })
                            .Sagas(x => x.StoreInJsonFile(AppDomain.CurrentDomain.BaseDirectory))
                            .Start();

                activator.Bus.Subscribe<NewOrderMessage>();

                activator.Register(() => new CashierSaga(bus));
                Console.ReadLine();
            }
        }
    }
}
