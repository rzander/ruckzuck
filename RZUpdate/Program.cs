//
// RZUpdate (C) 2017 by Roger Zander
// This Tool is under MS-Pl License (http://ruckzuck.codeplex.com/license)
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.IO;
using RuckZuck_WCF;
using System.Diagnostics;

namespace RZUpdate
{
    class Program
    {
        public static RZScan oScan;
        public static RZUpdater oUpdate;
        public static bool bRunning = true;

        static void Main(string[] args)
        {
            //Get Proxy from IE
            WebRequest.DefaultWebProxy = WebRequest.GetSystemWebProxy();

            List<string> lArgs = args.ToList();
            if (lArgs.Contains("-?") | lArgs.Contains("/?") | lArgs.Count < 1)
            {
                Console.WriteLine("RuckZuck Update Tool (c) 2018 by Roger Zander");
                Console.WriteLine("Usage:");
                Console.WriteLine("Check and Update an existing Software : RZUpdate.exe \"<ProductName>\" \"<ProductVersion>\" [\"Manufacturer\"]");
                Console.WriteLine("Install a Software from Shortname : RZUpdate.exe \"<Shortname>\"[;\"<Shortname2>\"]");
                Console.WriteLine("Install a Software from XML-File: RZUpdate.exe \"<RZXML File.xml>\"");
                Console.WriteLine("Update all installed Software-Versions: RZUpdate.exe /Update");
                Console.WriteLine("");
                return;
            }

            RZRestAPI.sURL = Properties.Settings.Default.WebService;

            if (lArgs.Count == 1)
            {
                if (File.Exists(lArgs[0]))
                {
                    RZUpdater oRZSW = new RZUpdater(lArgs[0]);

                    Console.WriteLine(oRZSW.SoftwareUpdate.SW.ProductName + " " + oRZSW.SoftwareUpdate.SW.ProductVersion + " :");
                    Console.Write("Downloading...");
                    if (oRZSW.SoftwareUpdate.Download().Result)
                    {
                        Console.WriteLine("... done.");
                        Console.Write("Installing...");
                        if (oRZSW.SoftwareUpdate.Install(false,true).Result)
                        {
                            Console.WriteLine("... done.");
                        }
                        else
                        {
                            Console.WriteLine("... Error. The installation failed.");
                        }
                    }
                }
                else
                {
                    if (lArgs[0].ToLower() == "/update" | lArgs[0].ToLower() == "-update")
                    {
                        oUpdate = new RZUpdater();
                        oScan = new RZScan(true, true);
                        Console.Write("Detecting updates...");
                        oScan.OnUpdScanCompleted += OScan_OnUpdScanCompleted;

                        while (bRunning)
                        {
                            System.Threading.Thread.Sleep(100);
                        }
                    }
                    else
                    {
                        foreach (string sArg in lArgs[0].Split(';'))
                        {
                            try
                            {
                                RZUpdater oRZSW = new RZUpdater();
                                oRZSW.SoftwareUpdate = new SWUpdate(sArg.Trim('"').Trim());

                                if (string.IsNullOrEmpty(oRZSW.SoftwareUpdate.SW.ProductName))
                                {
                                    Console.WriteLine("'" + sArg + "' is NOT available in RuckZuck...!");
                                    continue;
                                }


                                Console.WriteLine(oRZSW.SoftwareUpdate.SW.Manufacturer + " " + oRZSW.SoftwareUpdate.SW.ProductName + " " + oRZSW.SoftwareUpdate.SW.ProductVersion);
                                Console.Write("Downloading...");
                                foreach (string sPreReq in oRZSW.SoftwareUpdate.SW.PreRequisites)
                                {
                                    if (!string.IsNullOrEmpty(sPreReq))
                                    {
                                        RZUpdater oRZSWPreReq = new RZUpdater();
                                        oRZSWPreReq.SoftwareUpdate = new SWUpdate(sPreReq);
                                        Console.WriteLine();
                                        Console.Write("\tDownloading dependencies (" + oRZSWPreReq.SoftwareUpdate.SW.Shortname + ")...");
                                        if (oRZSWPreReq.SoftwareUpdate.Download().Result)
                                        {
                                            Console.WriteLine("... done.");
                                            Console.Write("\tInstalling dependencies (" + oRZSWPreReq.SoftwareUpdate.SW.Shortname + ")...");
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

                                }
                                if (oRZSW.SoftwareUpdate.Download().Result)
                                {
                                    Console.WriteLine("... done.");
                                    Console.Write("Installing...");
                                    if (oRZSW.SoftwareUpdate.Install(false,true).Result)
                                    {
                                        Console.WriteLine("... done.");
                                    }
                                    else
                                    {
                                        Console.WriteLine("... Error. The installation failed.");
                                    }
                                }
                            }
                            catch(Exception ex)
                            {
                                Console.WriteLine("Error: " + ex.Message);
                            }
                        }

                    }
                }
            }
            if (lArgs.Count == 2)
            {
                RZUpdater oRZUpdate = new RZUpdater();
                var oUpdate = oRZUpdate.CheckForUpdate(lArgs[0], lArgs[1]);
                if (oUpdate != null)
                {
                    Console.WriteLine("New Version: " + oUpdate.SW.ProductVersion);
                    Console.Write("Downloading...");

                    if (oUpdate.Download().Result)
                    {
                        Console.WriteLine("... done.");
                        Console.Write("Installing...");
                        if (oUpdate.Install(false, true).Result)
                        {
                            Console.WriteLine("... done.");
                        }
                        else
                        {
                            Console.WriteLine("... Error. The update installation failed.");
                        }
                    }
                }
                else
                {
                    Console.WriteLine("No Update found...");
                }
            }

            if (lArgs.Count == 3)
            {
                RZUpdater oRZUpdate = new RZUpdater();
                var oUpdate = oRZUpdate.CheckForUpdate(lArgs[0], lArgs[1], lArgs[2]);
                if (oUpdate != null)
                {
                    Console.WriteLine("New Version: " + oUpdate.SW.ProductVersion);
                    Console.Write("Downloading...");

                    if (oUpdate.Download().Result)
                    {
                        Console.WriteLine("... done.");
                        Console.Write("Installing...");
                        if (oUpdate.Install(false, true).Result)
                        {
                            Console.WriteLine("... done.");
                        }
                        else
                        {
                            Console.WriteLine("... Error. The update installation failed.");
                        }
                    }
                }
                else
                {
                    Console.WriteLine("No Update found...");
                }
            }

            System.Threading.Thread.Sleep(1000);
        }

        private static void OScan_OnUpdScanCompleted(object sender, EventArgs e)
        {
            Console.WriteLine("... done.");
            Console.WriteLine(oScan.NewSoftwareVersions.Count + " updates found.");

            foreach (RuckZuck_WCF.AddSoftware oSW in oScan.NewSoftwareVersions)
            {
                try
                {
                    Console.WriteLine(oSW.Manufacturer + " " + oSW.ProductName + " new Version: " + oSW.ProductVersion);
                    oUpdate.SoftwareUpdate.SW = oSW;
                    oUpdate.SoftwareUpdate.GetInstallType();
                    Console.Write("Downloading...");
                    if (oUpdate.SoftwareUpdate.Download().Result)
                    {
                        Console.WriteLine("... done.");
                        Console.Write("Installing...");
                        if (oUpdate.SoftwareUpdate.Install(false, true).Result)
                        {
                            Console.WriteLine("... done.");
                        }
                        else
                        {
                            Console.WriteLine("... Error. The update installation failed.");
                        }
                    }
                }
                catch(Exception ex)
                {
                    Console.WriteLine("E131:" + ex.Message);
                }
            }
            bRunning = false;       
        }
    }
}
