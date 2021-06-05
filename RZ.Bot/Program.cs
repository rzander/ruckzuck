using System;
//using Microsoft.ServiceBus;
//using Microsoft.ServiceBus.Messaging;
using System.Threading;
using RZUpdate;
using RuckZuck.Base;
using System.Collections.Generic;
using System.Net.Http;
using System.Xml;
using Newtonsoft.Json.Linq;
using System.Web;
using System.Threading.Tasks;

namespace RZ.Bot
{
    class Program
    {
        public static List<string> lPackages = new List<string>();
        public static List<string> lDelete = new List<string>();
        public static List<string> lDone = new List<string>();
        public static DateTime tStart = DateTime.Now;

        static void Main(string[] args)
        {
            tStart = DateTime.Now;
            RZRestAPIv2.CustomerID = "swtesting";
            RZRestAPIv2.DisableBroadcast = true;
            RZRestAPIv2.GetURL(RZRestAPIv2.CustomerID);

#if !DEBUG
            RZScan oScan = new RZScan(false, false);
            oScan.SWScanAsync().Wait();
            if (oScan.InstalledSoftware.Count >= 5)
            {
                Console.WriteLine("Please run RZ.Bot.exe on a clean Machine !!!");
                Console.ReadLine();
                return;
            }
#endif
            bool bLoop = true;
            while (bLoop)
            {
                int iCount = ProcessBotQueue();
                //Console.WriteLine(iCount.ToString() + " processed");
                if (iCount == 0)
                {
                    Console.WriteLine("no failed installations in the queue.... Waiting 5min...");
#if !DEBUG
                    Thread.Sleep(300000); //sleep 5min
#endif
#if DEBUG
                    Thread.Sleep(60000); //sleep 60s
#endif
                }

                if (iCount > 0)
                    Thread.Sleep(15000);

                if (iCount < 0)
                    bLoop = false;
            }
            return;
        }

        private static int ProcessBotQueue()
        {
            Console.WriteLine("Get failed installations from queue...");
            List<GetSoftware> lCat = RZRestAPIv2.GetCatalog();

            string sURL = "https://ruckzuck.queue.core.windows.net/rzbot/messages";
            string sasToken = Properties.Settings.Default.SASToken;

            List<string> DLQueue = new List<string>();
            List<string> BotQueue = new List<string>();
            Dictionary<string, string> IDQueue = new Dictionary<string, string>();
            int iMessageCount = 33;
            int iResult = 0;
            int icount = 0;
            //Get bulk of 32 Messages
            while (iMessageCount >= 32 && icount <= 20 && iResult <= 20)
            {
#if !DEBUG
                if ((DateTime.Now - tStart).TotalHours >= 2)
                {
                    Console.WriteLine("Max. runtime of 2h exceeded...");

                    return -1;
                }
#endif

                using (HttpClient oClient = new HttpClient())
                {
                    string url = $"{sURL}?numofmessages=32&{sasToken}";
                    var oRes = oClient.GetStringAsync(url);
                    oRes.Wait();
                    string sXML = oRes.Result.ToString();
                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.LoadXml(sXML);
                    iMessageCount = xmlDoc.SelectNodes("QueueMessagesList/QueueMessage").Count;
                    icount++;
                    foreach (XmlNode xNode in xmlDoc.SelectNodes("QueueMessagesList/QueueMessage"))
                    {
                        iResult++;
                        //if (iResult > 10)
                        //    continue;
                        try
                        {
                            string sMessageID = xNode["MessageId"].InnerText;
                            string Shortname = xNode["MessageText"].InnerText;
                            string sPopReceipt = xNode["PopReceipt"].InnerText;

                            if (lDelete.Contains(Shortname.ToLower()))
                            {
                                IDQueue.Add(sMessageID, sPopReceipt);
                                continue;
                            }

                            if (lDone.Contains(Shortname.ToLower()))
                            {
                                continue;
                            }

                            try
                            {
                                try
                                {
                                    RZUpdater oRZSW = new RZUpdater();
                                    oRZSW.SoftwareUpdate = new SWUpdate(Shortname);

                                    oRZSW.SoftwareUpdate.SendFeedback = false; //we already process feedback...
                                    if (string.IsNullOrEmpty(oRZSW.SoftwareUpdate.SW.PSInstall))
                                    {
                                        oRZSW.SoftwareUpdate.GetInstallType();
                                    }

                                    if (string.IsNullOrEmpty(oRZSW.SoftwareUpdate.SW.ProductName))
                                    {
                                        Console.ForegroundColor = ConsoleColor.Red;
                                        Console.WriteLine("Error: Product not valid... " + Shortname);
                                        Console.ResetColor();
                                        continue;
                                    }
                                    else
                                    {
                                        Console.ForegroundColor = ConsoleColor.Blue;
                                        Console.WriteLine(oRZSW.SoftwareUpdate.SW.Manufacturer + " " + oRZSW.SoftwareUpdate.SW.ProductName + " " + oRZSW.SoftwareUpdate.SW.ProductVersion);
                                        Console.ResetColor();

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
                                                Console.ForegroundColor = ConsoleColor.Green;
                                                Console.WriteLine("... done.");
                                                Console.ResetColor();
                                                Console.Write("\tInstalling dependencies (" + oRZSWPreReq.SoftwareUpdate.SW.ShortName + ")...");
                                                if (oRZSWPreReq.SoftwareUpdate.Install(false, true).Result)
                                                {
                                                    Console.ForegroundColor = ConsoleColor.Green;
                                                    Console.WriteLine("... done.");
                                                    Console.ResetColor();
                                                    //lPackages.Add(oRZSWPreReq.SoftwareUpdate.SW.ShortName);
                                                }
                                                else
                                                {
                                                    Console.ForegroundColor = ConsoleColor.Red;
                                                    Console.WriteLine("... Error. The installation failed.");
                                                    Console.ResetColor();
                                                    continue;
                                                }
                                            }
                                        }
                                        if (oRZSW.SoftwareUpdate.Download().Result)
                                        {
                                            Console.ForegroundColor = ConsoleColor.Green;
                                            Console.WriteLine("... done.");
                                            Console.ResetColor();

                                            Console.Write("Installing...");
                                            if (oRZSW.SoftwareUpdate.Install(false, true).Result)
                                            {
                                                Console.ForegroundColor = ConsoleColor.Green;
                                                Console.WriteLine("... done.");
                                                DeleteFromQueueAsync(sURL, sasToken, sMessageID, sPopReceipt).Wait(2000);
                                                lDone.Add(Shortname.ToLower());
                                                lDelete.Add(Shortname.ToLower());
                                                Console.ResetColor();
                                            }
                                            else
                                            {
                                                Console.ForegroundColor = ConsoleColor.Red;
                                                Console.WriteLine("... Error. Installation failed.");
                                                Console.ResetColor();
                                                lDone.Add(Shortname.ToLower());
                                                continue;
                                            }

                                        }
                                        else
                                        {
                                            Console.ForegroundColor = ConsoleColor.Red;
                                            Console.WriteLine("... Error. Download failed.");
                                            Console.ResetColor();
                                            lDone.Add(Shortname.ToLower());
                                            continue;
                                        }

                                    }

                                }
                                catch (Exception ex)
                                {
                                    Console.ForegroundColor = ConsoleColor.Red;
                                    Console.WriteLine("ERROR(1): " + ex.Message);
                                    Console.ResetColor();
                                    lDone.Add(Shortname.ToLower());
                                    continue;
                                }

                                Console.ResetColor();

                                //lDone.Add(Shortname.ToLower());
                                //IDQueue.Add(sMessageID, sPopReceipt);
                            }
                            catch { }



                        }
                        catch (Exception ex)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("ERROR(2): " + ex.Message);
                            Console.ResetColor();
                        }
                    }
                }
            }

            foreach (var sID in IDQueue)
            {
                DeleteFromQueueAsync(sURL, sasToken, sID.Key, sID.Value).Wait(2000);
            }

            return iResult;
        }

        private static async Task<bool> DeleteFromQueueAsync(string sURL, string sasToken, string sKey, string sValue)
        {
            try
            {
                using (HttpClient oClient = new HttpClient())
                {
                    string url = $"{sURL}/{sKey}?{sasToken}&popreceipt={HttpUtility.UrlEncode(sValue)}";
                    var oRes = await oClient.DeleteAsync(url);
                    if (oRes.StatusCode == System.Net.HttpStatusCode.OK)
                        return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR(3): " + ex.Message.ToString());
            }

            return false;
        }
    }
}
