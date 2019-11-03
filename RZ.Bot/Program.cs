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
        public static DateTime tStart = DateTime.Now;

        static void Main(string[] args)
        {
            MessagingFactory messageFactory;
            NamespaceManager namespaceManager;
            //TopicClient myTopicClient;
            //lPackages.Add("AdobeReader DC MUI");
            tStart = DateTime.Now;
            RZRestAPIv2.CustomerID = "swtesting";
            RZRestAPIv2.DisableBroadcast = true;
            RZRestAPIv2.GetURL(RZRestAPIv2.CustomerID);
            
#if !DEBUG
            RZScan oScan = new RZScan(false, false);
            oScan.SWScan().Wait();
            if(oScan.InstalledSoftware.Count >= 2)
            {
                Console.WriteLine("Please run RZ.Bot.exe on a clean Machine !!!");
                Console.ReadLine();
                return;
            }
#endif
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
                    if((DateTime.Now - tStart).TotalHours >= 6)
                    {
                        Console.WriteLine("Max. runtime of 6h exceeded...");
                        return;
                    }
                    try
                    {
                        if (lPackages.IndexOf(message.Properties["ProductName"].ToString() + message.Properties["ProductVersion"].ToString() + message.Properties["Manufacturer"].ToString()) >= 0)
                        {
                            message.Complete();
                            return;
                        }
                        List<GetSoftware> lCat = RZRestAPIv2.GetCatalog();

                        RZUpdater oRZSW = new RZUpdater();
                        
                        var CatItem = lCat.Find(t => t.ProductName.ToLower() == message.Properties["ProductName"].ToString().ToLower() && t.ProductVersion.ToLower() == message.Properties["ProductVersion"].ToString().ToLower() && t.Manufacturer.ToLower() == message.Properties["Manufacturer"].ToString().ToLower());

                        if(CatItem != null)
                        {
                            oRZSW.SoftwareUpdate = new SWUpdate(CatItem.ShortName);
                            if(oRZSW.SoftwareUpdate.SW.ShortName == null)
                                oRZSW.SoftwareUpdate = new SWUpdate(message.Properties["ProductName"].ToString(), message.Properties["ProductVersion"].ToString(), message.Properties["Manufacturer"].ToString());
                        }
                        else
                        {
                            oRZSW.SoftwareUpdate = new SWUpdate(message.Properties["ProductName"].ToString(), message.Properties["ProductVersion"].ToString(), message.Properties["Manufacturer"].ToString());
                            oRZSW.SoftwareUpdate = new SWUpdate(oRZSW.SoftwareUpdate.SW.ShortName);
                        }

                        if (lPackages.IndexOf(oRZSW.SoftwareUpdate.SW.ShortName) >= 0) //check if there was a previous success
                        {
                            message.Complete();
                            return;
                        }

                        //if(message.Properties["ProductVersion"].ToString() != oRZSW.SoftwareUpdate.SW.ProductVersion)
                        //{
                        //    oRZSW.SoftwareUpdate = new SWUpdate(oRZSW.SoftwareUpdate.SW.ShortName);
                        //}

                        if (sLastPackage != oRZSW.SoftwareUpdate.SW.ShortName)
                        {
                            //oRZSW.SoftwareUpdate = new SWUpdate(oRZSW.SoftwareUpdate.SW.Shortname);

                            oRZSW.SoftwareUpdate.SendFeedback = false; //we already process feedback...
                            if (string.IsNullOrEmpty(oRZSW.SoftwareUpdate.SW.PSInstall))
                            {
                                oRZSW.SoftwareUpdate.GetInstallType();

                                //Console.WriteLine("PreRequisites not valid ...!");
                                //message.Abandon();
                                //return;
                            }

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
                                    oRZSWPreReq.SoftwareUpdate.SendFeedback = false;
                                    Console.WriteLine();
                                    Console.Write("\tDownloading dependencies (" + oRZSWPreReq.SoftwareUpdate.SW.ShortName + ")...");
                                    if (oRZSWPreReq.SoftwareUpdate.Download().Result)
                                    {
                                        Console.WriteLine("... done.");
                                        Console.Write("\tInstalling dependencies (" + oRZSWPreReq.SoftwareUpdate.SW.ShortName + ")...");
                                        if (oRZSWPreReq.SoftwareUpdate.Install(false, true).Result)
                                        {
                                            Console.WriteLine("... done.");
                                            lPackages.Add(oRZSWPreReq.SoftwareUpdate.SW.ShortName);
                                        }
                                        else
                                        {
                                            Console.WriteLine("... Error. The installation failed.");
                                            message.Abandon();
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
                                        //RZRestAPIv2.Feedback(oRZSW.SoftwareUpdate.SW.ProductName, oRZSW.SoftwareUpdate.SW.ProductVersion, oRZSW.SoftwareUpdate.SW.Manufacturer, "true", "RZBot", "ok..").Wait(3000);
                                        sLastPackage = oRZSW.SoftwareUpdate.SW.ShortName;
                                        lPackages.Add(oRZSW.SoftwareUpdate.SW.ShortName);
                                        lPackages.Add(message.Properties["ProductName"].ToString() + message.Properties["ProductVersion"].ToString() + message.Properties["Manufacturer"].ToString());
                                        //return 0;
                                    }
                                    else
                                    {
                                        Console.WriteLine("... Error. Installation failed.");
                                        sLastPackage = oRZSW.SoftwareUpdate.SW.ShortName;
                                        message.DeadLetter();
                                        //return 1603;
                                    }

                                }
                                else
                                {
                                    Console.WriteLine("... Error. Download failed.");
                                    sLastPackage = oRZSW.SoftwareUpdate.SW.ShortName;
                                    message.DeadLetter();
                                    //return 1602;
                                }

                            }
                        }
                        else
                        {
                            //Console.WriteLine("... retry later..");
                            sLastPackage = oRZSW.SoftwareUpdate.SW.ShortName;

                            Thread.Sleep(1000);
                            //message.Abandon(); // retry later....
                            message.DeadLetter(); 
                            return;
                        }

                    }
                    catch(Exception ex)
                    {
                        Console.WriteLine("ERROR: " + ex.Message);
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
