using Newtonsoft.Json.Linq;
using Rebus.Activation;
using Rebus.Config;
using Rebus.Persistence.FileSystem;
using Rebus.Persistence.SqlServer;
using Rebus.Routing.TypeBased;
using Rebus.Transport.Msmq;
using Rebus.Encryption;
using RebusStarbucks.Messages;
using System;
using System.IO;
using Rebus.Transport.FileSystem;
using Rebus.SagaStorage;

namespace RebusStarbucks.Client
{
    class Program
    {
        static readonly string JsonFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "rebus_subscriptions.json");
        static readonly string sagasJsonFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sagas.json");
        static readonly string messageQueueFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MessageQueues");

        static void Main(string[] args)
        {
            using (var activator = new BuiltinHandlerActivator())
            {
                var theBus = Configure.With(activator)
                            .Transport(t => t.UseFileSystem(messageQueueFilePath, "rebusStarbucks.client"))
                            //.Transport(t => t.UseMsmq("rebusStarbucks.client"))
                            //.Transport(t => { t.Register(context => new Msmq.MsmqTransport("rebusStarbucks.client", true)); })
                            .Routing(r =>
                            {
                                r.TypeBased()
                                //.Map<SubmitPaymentMessage>("rebusStarbucks.cashier")
                                .Map<DrinkReadyMessage>("rebusStarbucks.barista");
                            })
                            .Subscriptions(s => s.UseJsonFile(JsonFilePath))
                            //.Sagas(s => s.StoreInSqlServer("Server=.\\sqlexpress;Database=rebus;Trusted_Connection=True;", "sagas", "sagas_index"))
                            .Sagas(x => x.StoreInJsonFile(AppDomain.CurrentDomain.BaseDirectory))
                            //.Options(op => op.SetMaxParallelism(3))
                            .Options(o =>
                            {
                                o.EnableEncryption("VW6DcqJioLHnV1b9oPnDFCYAGB7VxJcY");
                            })
                            .Start();


                //var asd = new JArray(JObject.FromObject(new CustomerSagaData()));


                activator.Register((bus, context) => new CustomerSaga(bus));
                //activator.Register((bus, context) => new CollectLegalInfoSaga(bus));

                //var id = Guid.NewGuid();
                //theBus.SendLocal(new StartProcess { CorrelationId = id }).Wait();

                activator.Bus.Subscribe<DrinkReadyMessage>();
                Extensions.ActionWithCyan(() => {
                    Console.WriteLine("Enter your name to place your order or press 'q' to quit");
                });
                var command = Console.ReadLine();
                
                while(command != "q")
                {
                    var clientId = Guid.NewGuid();

                    theBus.Publish(new NewOrderMessage { CorrelationId = clientId, Item = "latte", Name = command, Size = "grande" }).Wait();
                    
                    Extensions.ActionWithCyan(() => {
                        Console.WriteLine("Enter your name to place your order or press 'q' to quit");
                    });

                    command = Console.ReadLine();
                }

            }
        }
    }

    public static class Extensions
    {
        public static void ActionWithCyan(Action action)
        {
            var color = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Cyan;
            action();
            Console.ForegroundColor = color;
        }
    }

}
