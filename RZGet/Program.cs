//
// RZUpdate (C) 2017 by Roger Zander
// This Tool is under MS-Pl License (http://ruckzuck.codeplex.com/license)
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.IO;
using RuckZuck.Base;
using RZUpdate;
using Newtonsoft.Json;

namespace RZGet
{
    class Program
    {
        public static RZScan oScan;
        public static RZUpdater oUpdate;
        public static bool bRunning = true;
        public static bool bRetry = false;

        static int Main(string[] args)
        {
            bool bError = false;

            //Get Proxy from IE
            WebRequest.DefaultWebProxy = WebRequest.GetSystemWebProxy();

            if (!string.IsNullOrEmpty(Properties.Settings.Default.Customerid))
                RZRestAPIv2.CustomerID = Properties.Settings.Default.Customerid;

            RZRestAPIv2.DisableBroadcast = Properties.Settings.Default.DisableBroadcast;

            List<string> lArgs = args.ToList();
            if (lArgs.Contains("-?") | lArgs.Contains("/?") | lArgs.Count < 1)
            {
                Console.WriteLine("RuckZuck CommandLine Tool (c) 2021 by Roger Zander");
                Console.WriteLine("Install:");
                Console.WriteLine("Install a Software from Shortname : RZGet.exe install \"<Shortname>\"[;\"<Shortname2>\"]");
                Console.WriteLine("Install a Software from JSON File : RZGet.exe install \"<JSON full path>\"[;\"<JSON full path>\"]");
                Console.WriteLine("Install a Sepcific Version : RZGet.exe install --name \"<ProductName>\" --vendor \"<Manufacturer>\" --version \"<ProductVersion>\"");
                Console.WriteLine("");
                Console.WriteLine("Update:");
                Console.WriteLine("Update all missing updates : RZGet.exe update --all [--retry]");
                Console.WriteLine("Update all missing updates : RZGet.exe update --all --exclude \"<Shortname>\"[;\"<Shortname2>\"] [--retry]");
                Console.WriteLine("Show all missing updates : RZGet.exe update --list --all");
                Console.WriteLine("check if a Software requires an update : RZGet.exe update --list \"<Shortname>\"");
                Console.WriteLine("Update a Software from Shortname : RZGet.exe update \"<Shortname>\"[;\"<Shortname2>\"] [--retry]");
                Console.WriteLine("");
                Console.WriteLine("Show:");
                Console.WriteLine("Show Metadata : RZGet.exe show \"<Shortname>\"");
                Console.WriteLine("Show Metadata for a specific Version : RZGet.exe show --name \"<ProductName>\" --vendor \"<Manufacturer>\" --version \"<ProductVersion>\"");
                Console.WriteLine("");
                Console.WriteLine("Search:");
                Console.WriteLine("Show full Catalog JSON: RZGet.exe search");
                Console.WriteLine("Search for a Keyword: RZGet.exe search zip");
                Console.WriteLine("Search SW in a Category: RZGet.exe search --categories compression");
                Console.WriteLine("Search for installed SW: RZGet.exe search --isinstalled true");
                Console.WriteLine("Search for a manufacturer: RZGet.exe search --manufacturer zander");
                Console.WriteLine("Search for a shortname and return PowerShell Object: RZGet.exe search --shortname ruckzuck | convertfrom-json");
                return 0;
            }

            if(lArgs[0].ToLower() == "install")
            {
                if(lArgs.Contains("--name", StringComparer.CurrentCultureIgnoreCase) || lArgs.Contains("--vendor", StringComparer.CurrentCultureIgnoreCase) || lArgs.Contains("--version", StringComparer.CurrentCultureIgnoreCase))
                {

                    try
                    {
                        RZUpdater oRZSW = new RZUpdater();
                        string ProductName = lArgs[lArgs.FindIndex(t => t.IndexOf("--name", StringComparison.CurrentCultureIgnoreCase) >= 0) + 1];
                        string Manufacturer = lArgs[lArgs.FindIndex(t => t.IndexOf("--vendor", StringComparison.CurrentCultureIgnoreCase) >= 0) + 1];
                        
                        if(string.IsNullOrEmpty(Manufacturer))
                            Manufacturer = lArgs[lArgs.FindIndex(t => t.IndexOf("--manufacturer", StringComparison.CurrentCultureIgnoreCase) >= 0) + 1];

                        string ProductVersion = lArgs[lArgs.FindIndex(t => t.IndexOf("--version", StringComparison.CurrentCultureIgnoreCase) >= 0) + 1];
                        oRZSW.SoftwareUpdate = new SWUpdate(ProductName, ProductVersion, Manufacturer);

                        if (string.IsNullOrEmpty(oRZSW.SoftwareUpdate.SW.ProductName))
                        {
                            Console.WriteLine("'" + ProductName + "' is NOT available in RuckZuck...!");
                            return 99;
                        }

                        if (string.IsNullOrEmpty(oRZSW.SoftwareUpdate.SW.PSInstall))
                        {
                            oRZSW.SoftwareUpdate.GetInstallType();
                            if (string.IsNullOrEmpty(oRZSW.SoftwareUpdate.SW.PSInstall))
                            {
                                Console.WriteLine("PreRequisites not valid for '" + oRZSW.SoftwareUpdate.SW.ShortName + "'...!");
                                return 91;
                            }
                        }

                        if (Install(oRZSW))
                            return 0;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error: " + ex.Message);
                        return 1;
                    }
                }

                if (lArgs.Contains("--retry"))
                    bRetry = true;

                if (lArgs.Contains("--noretry"))
                    bRetry = false;


                foreach (string sArg in args.Skip(1))
                {
                    if (!sArg.StartsWith("--"))
                    {
                        if (File.Exists(sArg) || File.Exists(Path.Combine(Environment.ExpandEnvironmentVariables("%TEMP%"), sArg)) || File.Exists(Path.Combine(Environment.ExpandEnvironmentVariables("%TEMP%"), sArg + ".json")))
                        {
                            string sJFile = sArg;
                            if (!File.Exists(sJFile))
                                sJFile = Path.Combine(Environment.ExpandEnvironmentVariables("%TEMP%"), sArg);

                            if (!File.Exists(sJFile))
                                sJFile = Path.Combine(Environment.ExpandEnvironmentVariables("%TEMP%"), sArg + ".json");

                            RZUpdater oRZSW = new RZUpdater(sJFile);

                            if (string.IsNullOrEmpty(oRZSW.SoftwareUpdate.SW.ProductName))
                            {
                                Console.WriteLine("'" + sArg + "' is NOT available in RuckZuck...!");
                                bError = true;
                                continue;
                            }

                            if (string.IsNullOrEmpty(oRZSW.SoftwareUpdate.SW.PSInstall))
                            {
                                oRZSW.SoftwareUpdate.GetInstallType();
                                if (string.IsNullOrEmpty(oRZSW.SoftwareUpdate.SW.PSInstall))
                                {
                                    Console.WriteLine("PreRequisites not valid for '" + sArg + "'...!");
                                    bError = false;
                                    continue;
                                }
                            }

                            if (Install(oRZSW))
                                continue;
                        }
                        else
                        {
                            try
                            {
                                RZUpdater oRZSW = new RZUpdater();
                                oRZSW.SoftwareUpdate = new SWUpdate(sArg.Trim('"').Trim());

                                if (string.IsNullOrEmpty(oRZSW.SoftwareUpdate.SW.ProductName))
                                {
                                    Console.WriteLine("'" + sArg + "' is NOT available in RuckZuck...!");
                                    bError = true;
                                    continue;
                                }

                                if (string.IsNullOrEmpty(oRZSW.SoftwareUpdate.SW.PSInstall))
                                {
                                    oRZSW.SoftwareUpdate.GetInstallType();
                                    if (string.IsNullOrEmpty(oRZSW.SoftwareUpdate.SW.PSInstall))
                                    {
                                        Console.WriteLine("PreRequisites not valid for '" + sArg + "'...!");
                                        bError = false;
                                        continue;
                                    }
                                }

                                if (Install(oRZSW))
                                    continue;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("Error: " + ex.Message);
                                bError = true;
                            }

                        }
                    }
                }

                if (bError)
                    return 1;
                else
                    return 0;
            }

            if (lArgs[0].ToLower() == "update")
            {
                if (lArgs.Contains("--retry"))
                    bRetry = true;

                if (lArgs.Contains("--noretry"))
                    bRetry = false;

                bool bUpdateAll = false;
                bool bList = false;
                bool bExclude = false;
                List<string> lExclude = new List<string>();

                if (lArgs.Contains("--all", StringComparer.CurrentCultureIgnoreCase))
                    bUpdateAll = true;
                if (lArgs.Contains("--list", StringComparer.CurrentCultureIgnoreCase))
                {
                    bList = true;
                }
                if (lArgs.Contains("--exclude", StringComparer.CurrentCultureIgnoreCase))
                {
                    bExclude = true;
                }

                RZScan oScan = new RZScan(false);
                oScan.GetSWRepository().Wait(10000);
                oScan.SWScanAsync().Wait(10000);
                oScan._CheckUpdates(null);

                List<string> lUpdate = new List<string>();
                if (!bUpdateAll)
                {
                    foreach (string sArg in args.Skip(1))
                    {
                        if (oScan.NewSoftwareVersions.Count(t => t.ShortName.ToLower() == sArg.ToLower()) > 0)
                        {
                            lUpdate.Add(sArg);
                        }
                    }
                } 
                else
                {
                    if (!bExclude)
                    {
                        lUpdate = oScan.NewSoftwareVersions.Select(t => t.ShortName).ToList();
                    }
                    else
                    {
                        int iex = lArgs.IndexOf("--exclude", 0);
                        string scl = lArgs[iex + 1];
                        lExclude = scl.ToLower().Split(';').ToList();
                        lUpdate = oScan.NewSoftwareVersions.Where(r=> !lExclude.Contains(r.ShortName.ToLower())).Select(t => t.ShortName.ToLower()).ToList();
                    }
                }

                foreach (string sArg in lUpdate)
                {
                    if (bList)
                    {
                        Console.WriteLine(sArg);
                        continue;
                    }
                    if (File.Exists(sArg))
                    {
                        RZUpdater oRZSW = new RZUpdater(sArg);

                        if (string.IsNullOrEmpty(oRZSW.SoftwareUpdate.SW.ProductName))
                        {
                            Console.WriteLine("'" + sArg + "' is NOT available in RuckZuck...!");
                            bError = true;
                            continue;
                        }

                        if (string.IsNullOrEmpty(oRZSW.SoftwareUpdate.SW.PSInstall))
                        {
                            oRZSW.SoftwareUpdate.GetInstallType();
                            if (string.IsNullOrEmpty(oRZSW.SoftwareUpdate.SW.PSInstall))
                            {
                                Console.WriteLine("PreRequisites not valid for '" + sArg + "'...!");
                                bError = false;
                                continue;
                            }
                        }

                        if (Install(oRZSW))
                            continue;
                    }
                    else
                    {
                        try
                        {
                            RZUpdater oRZSW = new RZUpdater();
                            oRZSW.SoftwareUpdate = new SWUpdate(sArg.Trim('"').Trim());

                            if (string.IsNullOrEmpty(oRZSW.SoftwareUpdate.SW.ProductName))
                            {
                                Console.WriteLine("'" + sArg + "' is NOT available in RuckZuck...!");
                                bError = true;
                                continue;
                            }

                            if (string.IsNullOrEmpty(oRZSW.SoftwareUpdate.SW.PSInstall))
                            {
                                oRZSW.SoftwareUpdate.GetInstallType();
                                if (string.IsNullOrEmpty(oRZSW.SoftwareUpdate.SW.PSInstall))
                                {
                                    Console.WriteLine("PreRequisites not valid for '" + sArg + "'...!");
                                    bError = false;
                                    continue;
                                }
                            }

                            if (Install(oRZSW))
                                continue;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Error: " + ex.Message);
                            bError = true;
                        }

                    }
                }
                
                if (bError)
                    return 1;
                else
                    return 0;
            }

            //if (lArgs[0].ToLower() == "hash")
            //{
            //    Console.WriteLine("hash");
            //    Console.WriteLine(string.Join(";", args.Skip(1)));
            //    return 0;
            //}

            if (lArgs[0].ToLower() == "search")
            {
                if (lArgs.Count > 1)
                {
                    if (lArgs[1].StartsWith("--")) 
                    {
                        string sProp = lArgs[1].ToLower().TrimStart('-');
                        string sSearch = lArgs[2].ToLower();

                        if(sProp == "shortname")
                            Console.WriteLine(JsonConvert.SerializeObject(RZRestAPIv2.GetCatalog().Where(t => t.ShortName.ToLower().Contains(sSearch)), Formatting.Indented).ToString());

                        if (sProp == "manufacturer")
                            Console.WriteLine(JsonConvert.SerializeObject(RZRestAPIv2.GetCatalog().Where(t => t.Manufacturer.ToLower().Contains(sSearch)), Formatting.Indented).ToString());

                        if (sProp == "productname" || sProp == "name")
                            Console.WriteLine(JsonConvert.SerializeObject(RZRestAPIv2.GetCatalog().Where(t => t.ProductName.ToLower().Contains(sSearch)), Formatting.Indented).ToString());

                        if (sProp == "description")
                            Console.WriteLine(JsonConvert.SerializeObject(RZRestAPIv2.GetCatalog().Where(t => t.Description.ToLower().Contains(sSearch)), Formatting.Indented).ToString());

                        if (sProp == "categories" || sProp == "category")
                            Console.WriteLine(JsonConvert.SerializeObject(RZRestAPIv2.GetCatalog().Where(t => string.Join(";", t.Categories.ToArray()).ToLower().Contains(sSearch)), Formatting.Indented).ToString());

                        if (sProp == "producturl" || sProp == "url")
                            Console.WriteLine(JsonConvert.SerializeObject(RZRestAPIv2.GetCatalog().Where(t => t.ProductURL.ToLower().Contains(sSearch)), Formatting.Indented).ToString());

                        if (sProp == "productversion" || sProp == "version")
                            Console.WriteLine(JsonConvert.SerializeObject(RZRestAPIv2.GetCatalog().Where(t => t.ProductVersion.ToLower().Contains(sSearch)), Formatting.Indented).ToString());

                        if (sProp == "isinstalled")
                        {
                            oScan = new RZScan(false, false);
                            oScan.SWScanAsync().Wait();
                            oScan.GetSWRepository().Wait();
                            foreach(var osw in oScan.InstalledSoftware)
                            {
                                var oItem = oScan.SoftwareRepository.FirstOrDefault(t => t.ProductName == osw.ProductName && t.Manufacturer == osw.Manufacturer && t.ProductVersion == osw.ProductVersion);
                                if (oItem != null)
                                    oItem.isInstalled = true;
                            }

                            Console.WriteLine(JsonConvert.SerializeObject(oScan.SoftwareRepository.Where(t => t.isInstalled.ToString().ToLower().Contains(sSearch)), Formatting.Indented).ToString());
                        }

                    }
                    else
                    {
                        string sSearch = lArgs[1].ToLower();
                        Console.WriteLine(JsonConvert.SerializeObject(RZRestAPIv2.GetCatalog().Where(t => t.ProductName.ToLower().Contains(sSearch) || t.Manufacturer.ToLower().Contains(sSearch) || t.ShortName.ToLower().Contains(sSearch)), Formatting.Indented).ToString());
                    }
                }
                else
                {
                    Console.WriteLine(JsonConvert.SerializeObject(RZRestAPIv2.GetCatalog(), Formatting.Indented).ToString());
                }
                return 0;
            }

            if (lArgs[0].ToLower() == "show")
            {
                if (lArgs.Contains("--name", StringComparer.CurrentCultureIgnoreCase) || lArgs.Contains("--vendor", StringComparer.CurrentCultureIgnoreCase) || lArgs.Contains("--version", StringComparer.CurrentCultureIgnoreCase))
                {

                    try
                    {
                        RZUpdater oRZSW = new RZUpdater();
                        string ProductName = lArgs[lArgs.FindIndex(t => t.IndexOf("--name", StringComparison.CurrentCultureIgnoreCase) >= 0) + 1];
                        string Manufacturer = lArgs[lArgs.FindIndex(t => t.IndexOf("--vendor", StringComparison.CurrentCultureIgnoreCase) >= 0) + 1];

                        if (string.IsNullOrEmpty(Manufacturer))
                            Manufacturer = lArgs[lArgs.FindIndex(t => t.IndexOf("--manufacturer", StringComparison.CurrentCultureIgnoreCase) >= 0) + 1];

                        string ProductVersion = lArgs[lArgs.FindIndex(t => t.IndexOf("--version", StringComparison.CurrentCultureIgnoreCase) >= 0) + 1];
                        oRZSW.SoftwareUpdate = new SWUpdate(ProductName, ProductVersion, Manufacturer);

                        if (string.IsNullOrEmpty(oRZSW.SoftwareUpdate.SW.ProductName))
                        {
                            Console.WriteLine("'" + ProductName + "' is NOT available in RuckZuck...!");
                            return 99;
                        }

                        Console.WriteLine(JsonConvert.SerializeObject(oRZSW.SoftwareUpdate.SW, Formatting.Indented).ToString());

                        return 0;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error: " + ex.Message);
                        return 1;
                    }
                }

                foreach (string sArg in args.Skip(1))
                {
                    if (File.Exists(sArg))
                    {
                        RZUpdater oRZSW = new RZUpdater(sArg);

                        Console.WriteLine(JsonConvert.SerializeObject(oRZSW.SoftwareUpdate.SW, Formatting.Indented).ToString());
                    }
                    else
                    {
                        try
                        {
                            RZUpdater oRZSW = new RZUpdater();
                            oRZSW.SoftwareUpdate = new SWUpdate(sArg.Trim('"').Trim());

                            if (string.IsNullOrEmpty(oRZSW.SoftwareUpdate.SW.ProductName))
                            {
                                Console.WriteLine("'" + sArg + "' is NOT available in RuckZuck...!");
                                bError = true;
                                continue;
                            }

                            Console.WriteLine(JsonConvert.SerializeObject(oRZSW.SoftwareUpdate.SW, Formatting.Indented).ToString());

                            return 0;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Error: " + ex.Message);
                            return 1;
                        }

                    }
                }

                return 0;
            }

            return 0;
        }

        private static bool Install(RZUpdater oRZSW)
        {
            if (string.IsNullOrEmpty(oRZSW.SoftwareUpdate.SW.PSInstall))
            {
                oRZSW.SoftwareUpdate.GetInstallType();
                if (string.IsNullOrEmpty(oRZSW.SoftwareUpdate.SW.PSInstall))
                {
                    Console.WriteLine("PreRequisites not valid for '" + oRZSW.SoftwareUpdate.SW.ShortName + "'...!");
                    return false;
                }
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
                    Console.Write("\tDownloading dependencies (" + oRZSWPreReq.SoftwareUpdate.SW.ShortName + ")...");
                    if (oRZSWPreReq.SoftwareUpdate.Download().Result)
                    {
                        Console.WriteLine("... done.");
                        Console.Write("\tInstalling dependencies (" + oRZSWPreReq.SoftwareUpdate.SW.ShortName + ")...");
                        if (oRZSWPreReq.SoftwareUpdate.Install(false, bRetry).Result)
                        {
                            Console.WriteLine("... done.");
                        }
                        else
                        {
                            Console.WriteLine("... Error. The installation failed.");
                            return false;
                        }
                    }
                }

            }
            if (oRZSW.SoftwareUpdate.Download().Result)
            {
                Console.WriteLine("... done.");
                Console.Write("Installing...");
                if (oRZSW.SoftwareUpdate.Install(false, bRetry).Result)
                {
                    Console.WriteLine("... done.");
                    return true;
                }
                else
                {
                    Console.WriteLine("... Error. The installation failed.");
                    return false;
                }
            }

            return false;
        }

        private static void OScan_OnUpdScanCompleted(object sender, EventArgs e)
        {
            Console.WriteLine("... done.");
            Console.WriteLine(oScan.NewSoftwareVersions.Count + " updates found.");

            foreach (AddSoftware oSW in oScan.NewSoftwareVersions)
            {
                try
                {
                    if (Properties.Settings.Default.Excludes.Cast<string>().ToList().FirstOrDefault(t => t.ToLower() == oSW.ShortName.ToLower()) != null)
                    {
                        Console.WriteLine("Skipping: " + oSW.ShortName + " (excluded)");
                        continue;
                    }
                    Console.WriteLine(oSW.Manufacturer + " " + oSW.ProductName + " new Version: " + oSW.ProductVersion);
                    oUpdate.SoftwareUpdate.SW = oSW;
                    oUpdate.SoftwareUpdate.GetInstallType();
                    Console.Write("Downloading...");
                    if (oUpdate.SoftwareUpdate.Download().Result)
                    {
                        Console.WriteLine("... done.");
                        Console.Write("Installing...");
                        if (oUpdate.SoftwareUpdate.Install(false, bRetry).Result)
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
