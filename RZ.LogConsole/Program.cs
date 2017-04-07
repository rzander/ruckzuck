using System;
using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;

namespace RZ.LogConsole
{
    class Program
    {


        static void Main(string[] args)
        {
            MessagingFactory messageFactory;
            NamespaceManager namespaceManager;
            //TopicClient myTopicClient;

            System.Net.ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
            System.Net.ServicePointManager.CheckCertificateRevocationList = false;

            Console.Write("Connecting ServiceBus...");
            string sConnString = Properties.Settings.Default.ConnectionString;
            messageFactory = MessagingFactory.CreateFromConnectionString(sConnString);
            namespaceManager = NamespaceManager.CreateFromConnectionString(sConnString);

            if (namespaceManager == null)
            {
                Console.WriteLine("\nUnexpected Error");
                return;
            }

            string TopicName = Properties.Settings.Default.TopicName;

            if (!namespaceManager.TopicExists(TopicName))
            {
                namespaceManager.CreateTopic(TopicName);
            }

            Console.WriteLine("... connected.");

            if (!namespaceManager.SubscriptionExists(TopicName, string.Format(Properties.Settings.Default.Filtername, Environment.MachineName)))
            {
                SqlFilter dashboardFilter = new SqlFilter(Properties.Settings.Default.SQLFilter);
                namespaceManager.CreateSubscription(TopicName, string.Format(Properties.Settings.Default.Filtername, Environment.MachineName), dashboardFilter);
            }

            var Client = messageFactory.CreateSubscriptionClient(TopicName, string.Format(Properties.Settings.Default.Filtername, Environment.MachineName), ReceiveMode.PeekLock);
            Client.OnMessage((message) =>
            {
                try
                {
                    if (message.Label.Contains(@"Feedback/failure"))
                        Console.ForegroundColor = ConsoleColor.Red;
                    if (message.Label.Contains(@"Feedback/success"))
                        Console.ForegroundColor = ConsoleColor.Green;
                    //Console.WriteLine(message.EnqueuedTimeUtc.ToLocalTime().ToString("HH:mm") + " " + message.Properties["WorkerServiceHost"].ToString() + "(" + message.Properties["Queue"].ToString() + ") : " + message.Properties["TargetComputer"].ToString()  + " : " + message.GetBody<string>());
                    Console.WriteLine(message.EnqueuedTimeUtc.ToLocalTime().ToString("HH:mm") + " " + message.Label + " " + message.GetBody<string>());


                    Console.ResetColor();
                }
                catch { }
            });

            Console.ReadLine();
        }
    }
}
