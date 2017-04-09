using System;
using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;
using System.Threading;
using RZUpdate;
using RuckZuck_WCF;

namespace RZ.Bot
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
                //SqlFilter dashboardFilter = new SqlFilter(Properties.Settings.Default.SQLFilter);
                //namespaceManager.CreateSubscription(TopicName, string.Format(Properties.Settings.Default.Filtername, Environment.MachineName), dashboardFilter);
                return;
            }

            var Client = messageFactory.CreateSubscriptionClient(TopicName, string.Format(Properties.Settings.Default.Filtername, Environment.MachineName), ReceiveMode.PeekLock);
            Client.OnMessage((message) =>
            {
                try
                {
                    /*if (message.Label.Contains(@"Feedback/failure"))
                        Console.ForegroundColor = ConsoleColor.Red;
                    if (message.Label.Contains(@"Feedback/success"))
                        Console.ForegroundColor = ConsoleColor.Green;*/
                    //Console.WriteLine(message.EnqueuedTimeUtc.ToLocalTime().ToString("HH:mm") + " " + message.Properties["WorkerServiceHost"].ToString() + "(" + message.Properties["Queue"].ToString() + ") : " + message.Properties["TargetComputer"].ToString()  + " : " + message.GetBody<string>());
                    //Console.WriteLine(message.EnqueuedTimeUtc.ToLocalTime().ToString("HH:mm") + " " + message.Label + " " + message.GetBody<string>());

                    Mutex mutex = new Mutex(false, "RuckZuck");
                    try
                    {
                        RZUpdater oRZSW = new RZUpdater();

                        oRZSW.SoftwareUpdate = new SWUpdate(message.Properties["ProductName"].ToString(), message.Properties["ProductVersion"].ToString(), message.Properties["Manufacturer"].ToString());
                        //oRZSW.SoftwareUpdate = new SWUpdate(message.Properties["ProductName"].ToString());

                        oRZSW.SoftwareUpdate.SendFeedback = false; //we already process feedback...

                        if (string.IsNullOrEmpty(oRZSW.SoftwareUpdate.SW.ProductName))
                        {
                            Console.WriteLine("Error: ProductName not valid... "  + message.Properties["ProductName"].ToString());
                            message.Abandon();
                            //Console.WriteLine("Error: Product not found in Repository...");
                        }
                        else
                        {
                            Console.WriteLine(oRZSW.SoftwareUpdate.SW.Manufacturer + " " + oRZSW.SoftwareUpdate.SW.ProductName + " " + oRZSW.SoftwareUpdate.SW.ProductVersion);
                            if (mutex.WaitOne(new TimeSpan(0, 15, 0), false))
                            {
                                Console.Write("Downloading...");
                                foreach (string sPreReq in oRZSW.SoftwareUpdate.SW.PreRequisites)
                                {
                                    RZUpdater oRZSWPreReq = new RZUpdater();
                                    oRZSWPreReq.SoftwareUpdate = new SWUpdate(sPreReq);
                                    Console.WriteLine();
                                    Console.Write("\tDownloading dependencies (" + oRZSWPreReq.SoftwareUpdate.SW.Shortname + ")...");
                                    if (oRZSWPreReq.SoftwareUpdate.Download().Result)
                                    {
                                        Console.WriteLine("... done.");
                                        Console.Write("\tInstalling dependencies (" + oRZSWPreReq.SoftwareUpdate.SW.Shortname + ")...");
                                        if (oRZSWPreReq.SoftwareUpdate.Install().Result)
                                        {
                                            Console.WriteLine("... done.");
                                        }
                                        else
                                        {
                                            Console.WriteLine("... Error. The installation failed.");
                                        }
                                    }
                                }
                                if (oRZSW.SoftwareUpdate.Download().Result)
                                {
                                    Console.WriteLine("... done.");

                                    Console.Write("Installing...");
                                    if (oRZSW.SoftwareUpdate.Install().Result)
                                    {
                                        Console.WriteLine("... done.");
                                        message.Complete();
                                        RZRestAPI.Feedback(oRZSW.SoftwareUpdate.SW.ProductName, oRZSW.SoftwareUpdate.SW.ProductVersion, oRZSW.SoftwareUpdate.SW.Manufacturer,  "true", "RZBot", "ok..").Wait(3000);
                                        //return 0;
                                    }
                                    else
                                    {
                                        Console.WriteLine("... Error. Installation failed.");
                                        message.Abandon();
                                        //return 1603;
                                    }

                                }
                                else
                                {
                                    Console.WriteLine("... Error. Download failed.");
                                    message.Abandon();
                                    //return 1602;
                                }
                            }
                        }

                    }
                    catch
                    {
                        message.Abandon();
                    }
                    finally
                    {
                        if (mutex != null)
                        {
                            mutex.ReleaseMutex();
                        }
                    }


                    Console.ResetColor();
                }
                catch { }
            });

            Console.ReadLine();
        }
    }
}
