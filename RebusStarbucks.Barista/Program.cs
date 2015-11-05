using Rebus.Activation;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Encryption;
using Rebus.Handlers;
using Rebus.Handlers.Reordering;
using Rebus.Persistence.FileSystem;
using Rebus.Pipeline;
using Rebus.Routing.TypeBased;
using Rebus.SagaStorage;
using Rebus.Transport.FileSystem;
using Rebus.Transport.Msmq;
using RebusStarbucks.Messages;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RebusStarbucks.Barista
{
    class Program
    {
        static readonly string JsonFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "rebus_subscriptions.json");
        static readonly string messageQueueFilePath = @"..\..\..\RebusStarbucks.Client\bin\Debug\MessageQueues";
        static void Main(string[] args)
        {
            using (var activator = new BuiltinHandlerActivator())
            {
                var bus = Configure.With(activator)
                            .Transport(t => t.UseFileSystem(messageQueueFilePath, "rebusStarbucks.barista"))
                            //.Transport(t => t.UseMsmq("rebusStarbucks.barista"))
                            //.Transport(t => { t.Register(context => new Msmq.MsmqTransport("rebusStarbucks.barista", true)); })
                            .Routing(r =>
                            {
                                r.TypeBased()
                                .Map<NewOrderMessage>("rebusStarbucks.client");
                            })
                            .Options(op => { op.SetMaxParallelism(10).SetNumberOfWorkers(10).EnableEncryption("VW6DcqJioLHnV1b9oPnDFCYAGB7VxJcY");})
                            .Sagas(x => x.StoreInJsonFile(AppDomain.CurrentDomain.BaseDirectory))
                            .Subscriptions(s => s.UseJsonFile(JsonFilePath))
                            .Start();

                activator.Bus.Subscribe<NewOrderMessage>();
                activator
                    .Register((b, c) => new BaristaSaga(b, c))
                    .Register((b, c) => new PrepareDrinkHandler(b));
                Console.ReadLine();
            }
        }
    }
}
