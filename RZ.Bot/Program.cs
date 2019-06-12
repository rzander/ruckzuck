using System;
using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;
using System.Threading;
using RZUpdate;
using RuckZuck.Base;
using System.Collections.Generic;

namespace RZ.Bot
{
    class Program
    {
        public static List<string> lPackages = new List<string>();

        static void Main(string[] args)
        {
            MessagingFactory messageFactory;
            NamespaceManager namespaceManager;
            //TopicClient myTopicClient;
            lPackages.Add("AdobeReader DC MUI");

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
                return;
            }
            string sLastPackage = "";
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

                    try
                    {
                        /*if (message.Properties["IP"].ToString() == "193.5.178.34")
                        {
                            message.Abandon();
                            return;
                        }
                        if (message.Properties["ProductName"].ToString() == "Adobe Acrobat Reader DC")
                        {
                            message.Abandon();
                            return;
                        }
                        if (message.Properties["ProductName"].ToString() == "Adobe Acrobat Reader DC MUI")
                        {
                            message.Abandon();
                            return;
                        }*/


                        RZUpdater oRZSW = new RZUpdater();

                        oRZSW.SoftwareUpdate = new SWUpdate(message.Properties["ProductName"].ToString(), message.Properties["ProductVersion"].ToString(), message.Properties["Manufacturer"].ToString());
                        //oRZSW.SoftwareUpdate = new SWUpdate(message.Properties["ProductName"].ToString());
                        if (lPackages.IndexOf(oRZSW.SoftwareUpdate.SW.ShortName) >= 0) //check if there was a previous success
                        {
                            message.Complete();
                            return;
                        }

                        if (sLastPackage != oRZSW.SoftwareUpdate.SW.ShortName)
                        {
                            //oRZSW.SoftwareUpdate = new SWUpdate(oRZSW.SoftwareUpdate.SW.Shortname);

                            oRZSW.SoftwareUpdate.SendFeedback = false; //we already process feedback...

                            if (string.IsNullOrEmpty(oRZSW.SoftwareUpdate.SW.ProductName))
                            {
                                Console.WriteLine("Error: ProductName not valid... " + message.Properties["ProductName"].ToString());
                                message.Abandon();
                                //Console.WriteLine("Error: Product not found in Repository...");
                            }
                            else
                            {
                                Console.WriteLine(oRZSW.SoftwareUpdate.SW.Manufacturer + " " + oRZSW.SoftwareUpdate.SW.ProductName + " " + oRZSW.SoftwareUpdate.SW.ProductVersion);

                                Console.Write("Downloading...");
                                foreach (string sPreReq in oRZSW.SoftwareUpdate.SW.PreRequisites)
                                {
                                    RZUpdater oRZSWPreReq = new RZUpdater();
                                    oRZSWPreReq.SoftwareUpdate = new SWUpdate(sPreReq);
                                    Console.WriteLine();
                                    Console.Write("\tDownloading dependencies (" + oRZSWPreReq.SoftwareUpdate.SW.ShortName + ")...");
                                    if (oRZSWPreReq.SoftwareUpdate.Download().Result)
                                    {
                                        Console.WriteLine("... done.");
                                        Console.Write("\tInstalling dependencies (" + oRZSWPreReq.SoftwareUpdate.SW.ShortName + ")...");
                                        if (oRZSWPreReq.SoftwareUpdate.Install(false, true).Result)
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
                                    if (oRZSW.SoftwareUpdate.Install(false, true).Result)
                                    {
                                        Console.WriteLine("... done.");
                                        message.Complete();
                                        RZRestAPIv2.Feedback(oRZSW.SoftwareUpdate.SW.ProductName, oRZSW.SoftwareUpdate.SW.ProductVersion, oRZSW.SoftwareUpdate.SW.Manufacturer, "true", "RZBot", "ok..").Wait(3000);
                                        sLastPackage = oRZSW.SoftwareUpdate.SW.ShortName;
                                        lPackages.Add(oRZSW.SoftwareUpdate.SW.ShortName);
                                        //return 0;
                                    }
                                    else
                                    {
                                        Console.WriteLine("... Error. Installation failed.");
                                        sLastPackage = oRZSW.SoftwareUpdate.SW.ShortName;
                                        message.Abandon();
                                        //return 1603;
                                    }

                                }
                                else
                                {
                                    Console.WriteLine("... Error. Download failed.");
                                    sLastPackage = oRZSW.SoftwareUpdate.SW.ShortName;
                                    message.Abandon();
                                    //return 1602;
                                }

                            }
                        }
                        else
                        {
                            Console.WriteLine("... retry later..");
                            Thread.Sleep(5000);
                            //message.Abandon(); // retry later....
                        }

                    }
                    catch
                    {
                        message.Abandon();
                    }

                    Console.ResetColor();
                }
                catch { }
            });

            Console.ReadLine();
        }
    }
}
