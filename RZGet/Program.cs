//
// RZUpdate (C) 2017 by Roger Zander
// This Tool is under MS-Pl License (http://ruckzuck.codeplex.com/license)
//

using RuckZuck.Base;
using RZUpdate;
using Serilog;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace RZGet
{
    internal class Program
    {
        public static RZScan oScan;
        public static RZUpdater oUpdate;
        public static bool bRunning = true;
        public static bool bRetry = false;
        public static bool bUser = false;
        public static bool bAllUsers = false;
        public static bool bMachine = true;

        private static async Task<int> Main(string[] args)
        {
            List<string> lArgs = args.ToList();

            //Check if AppSettings exists and configure Serilog
            string logLevel = ConfigurationManager.AppSettings["serilog:minimum-level"];
            if (!string.IsNullOrEmpty(logLevel))
            {
                Log.Logger = new LoggerConfiguration()
                    .ReadFrom.AppSettings()
                    .Enrich.FromLogContext()
                    .CreateLogger();
            }
            else
            {
                if (lArgs.Contains("/verbose", StringComparer.InvariantCultureIgnoreCase))
                {
                    Log.Logger = new LoggerConfiguration()
                        .MinimumLevel.Verbose()
                        .WriteTo.Console()
                        .Enrich.FromLogContext()
                        .CreateLogger();

                    lArgs.Remove("/verbose");
                }
                else
                {
                    Log.Logger = new LoggerConfiguration()
                        .MinimumLevel.Information()
                        .WriteTo.Console()
                        .Enrich.FromLogContext()
                        .CreateLogger();
                }

            }


            Trace.Listeners.Add(new global::SerilogTraceListener.SerilogTraceListener());

            bool bError = false;

            //Get Proxy from IE
            WebRequest.DefaultWebProxy = WebRequest.GetSystemWebProxy();

            if (!string.IsNullOrEmpty(Properties.Settings.Default.Customerid))
                RZRestAPIv2.CustomerID = Properties.Settings.Default.Customerid;

            RZRestAPIv2.DisableBroadcast = Properties.Settings.Default.DisableBroadcast;


            if (lArgs.Contains("-?") | lArgs.Contains("/?") | lArgs.Count < 1)
            {
                Console.WriteLine("RuckZuck CommandLine Tool (c) 2022 by Roger Zander");
                Console.WriteLine("Install:");
                Console.WriteLine("Install a Software from Shortname : RZGet.exe install \"<Shortname>\"[;\"<Shortname2>\"] [/cleanup]");
                Console.WriteLine("Install a Software from JSON File : RZGet.exe install \"<JSON full path>\"[;\"<JSON full path>\"]");
                Console.WriteLine("Install a Sepcific Version : RZGet.exe install --name \"<ProductName>\" --vendor \"<Manufacturer>\" --version \"<ProductVersion>\"");
                Console.WriteLine("");
                Console.WriteLine("Update:");
                Console.WriteLine("Update all missing updates : RZGet.exe update --all [--retry] [--user]");
                Console.WriteLine("Update all missing updates : RZGet.exe update --all --exclude \"<Shortname>\"[;\"<Shortname2>\"] [--retry] [--user]");
                Console.WriteLine("Show all missing updates : RZGet.exe update --list --all [--user] [--allusers]");
                Console.WriteLine("check if a Software requires an update : RZGet.exe update --list \"<Shortname>\" [--user]");
                Console.WriteLine("Update a Software from Shortname : RZGet.exe update \"<Shortname>\"[;\"<Shortname2>\"] [--retry] [--user]");
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

            if (lArgs[0].ToLower() == "install")
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

                        Log.ForContext("Param", lArgs[0].ToLower()).Verbose("installing '{productname}' '{productversion}' '{manufacturer}'", ProductName, ProductVersion, Manufacturer);

                        oRZSW.SoftwareUpdate = new SWUpdate(ProductName, ProductVersion, Manufacturer);

                        if (oRZSW.SoftwareUpdate == null || oRZSW.SoftwareUpdate.SW == null || string.IsNullOrEmpty(oRZSW.SoftwareUpdate.SW.ProductName))
                        {
                            Log.ForContext("Param", lArgs[0].ToLower()).Debug("RuckZuck Url: '{url}' customerid: '{customerid}'", RZRestAPIv2.sURL, RZRestAPIv2.CustomerID);
                            Log.ForContext("Param", lArgs[0].ToLower()).Warning("Software NOT available in repository: '{productname}' '{productversion}' '{manufacturer}'", ProductName, ProductVersion, Manufacturer);
                            return 99;
                        }

                        if (string.IsNullOrEmpty(oRZSW.SoftwareUpdate.SW.PSInstall))
                        {
                            oRZSW.SoftwareUpdate.GetInstallType();
                            if (string.IsNullOrEmpty(oRZSW.SoftwareUpdate.SW.PSInstall))
                            {
                                Log.ForContext("Param", lArgs[0].ToLower()).Warning("PreRequisites not valid for '{shortname}'", oRZSW.SoftwareUpdate.SW.ShortName);
                                return 91;
                            }
                        }

                        if (await InstallAsync(oRZSW))
                            return 0;
                    }
                    catch (Exception ex)
                    {
                        Log.ForContext("Param", lArgs[0].ToLower()).Error("E133: {ex}", ex.Message);
                        return 1;
                    }
                }

                if (lArgs.Contains("--retry"))
                    bRetry = true;

                if (lArgs.Contains("--noretry"))
                    bRetry = false;

                foreach (string sArg in lArgs.Skip(1))
                {
                    if (!sArg.StartsWith("--") && !sArg.StartsWith("/"))
                    {
                        if (File.Exists(sArg) || File.Exists(Path.Combine(Environment.ExpandEnvironmentVariables("%TEMP%"), sArg)) || File.Exists(Path.Combine(Environment.ExpandEnvironmentVariables("%TEMP%"), sArg + ".json")))
                        {
                            string sJFile = sArg;
                            if (!File.Exists(sJFile))
                                sJFile = Path.Combine(Environment.ExpandEnvironmentVariables("%TEMP%"), sArg);

                            if (!File.Exists(sJFile))
                                sJFile = Path.Combine(Environment.ExpandEnvironmentVariables("%TEMP%"), sArg + ".json");

                            Log.ForContext("Param", lArgs[0].ToLower()).Verbose("installing '{shortname}' from file: '{file}'", sArg, sJFile);

                            RZUpdater oRZSW = new RZUpdater(sJFile);

                            if (string.IsNullOrEmpty(oRZSW.SoftwareUpdate.SW.ProductName))
                            {
                                Log.ForContext("Param", lArgs[0].ToLower()).Error("Unable to read software from file: '{file}'", sJFile);
                                bError = true;
                                continue;
                            }

                            if (string.IsNullOrEmpty(oRZSW.SoftwareUpdate.SW.PSInstall))
                            {
                                oRZSW.SoftwareUpdate.GetInstallType();
                                if (string.IsNullOrEmpty(oRZSW.SoftwareUpdate.SW.PSInstall))
                                {
                                    Log.ForContext("Param", lArgs[0].ToLower()).Warning("PreRequisites not valid for '{shortname}'", sArg);
                                    bError = false;
                                    continue;
                                }
                            }

                            if (await InstallAsync(oRZSW))
                            {
                                if (lArgs.Contains("/cleanup"))
                                {
                                    try
                                    {
                                        Directory.Delete(oRZSW.SoftwareUpdate.ContentPath, true);
                                    }
                                    catch { }
                                }
                                continue;
                            }
                        }
                        else
                        {
                            try
                            {
                                RZUpdater oRZSW = new RZUpdater();
                                oRZSW.SoftwareUpdate = new SWUpdate(sArg.Trim('"').Trim());

                                Log.ForContext("Param", lArgs[0].ToLower()).Verbose("installing '{shortname}'", sArg);

                                if (oRZSW.SoftwareUpdate == null || oRZSW.SoftwareUpdate.SW == null || string.IsNullOrEmpty(oRZSW.SoftwareUpdate.SW.ProductName))
                                {
                                    Log.ForContext("Param", lArgs[0].ToLower()).Debug("RuckZuck Url: '{url}' customerid: '{customerid}'", RZRestAPIv2.sURL, RZRestAPIv2.CustomerID);
                                    Log.ForContext("Param", lArgs[0].ToLower()).Warning("Shortname NOT available in repository: '{shortname}'", sArg);
                                    bError = true;
                                    continue;
                                }

                                if (string.IsNullOrEmpty(oRZSW.SoftwareUpdate.SW.PSInstall))
                                {
                                    oRZSW.SoftwareUpdate.GetInstallType();
                                    if (string.IsNullOrEmpty(oRZSW.SoftwareUpdate.SW.PSInstall))
                                    {
                                        Log.ForContext("Param", lArgs[0].ToLower()).Warning("PreRequisites not valid for '{shortname}'", sArg);
                                        bError = false;
                                        continue;
                                    }
                                }

                                if (await InstallAsync(oRZSW))
                                {
                                    if (lArgs.Contains("/cleanup"))
                                    {
                                        try
                                        {
                                            Directory.Delete(oRZSW.SoftwareUpdate.ContentPath, true);
                                        }
                                        catch { }
                                    }
                                    continue;
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.ForContext("Param", lArgs[0].ToLower()).Error("E214: {ex}", ex.Message);
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
                bool bUpdateAll = true;
                bool bList = false;
                bool bExclude = false;

                if (lArgs.Contains("--retry", StringComparer.CurrentCultureIgnoreCase))
                    bRetry = true;

                if (lArgs.Contains("--user", StringComparer.CurrentCultureIgnoreCase))
                {
                    bUser = true;
                    bMachine = false;
                }

                if (lArgs.Contains("--allusers", StringComparer.CurrentCultureIgnoreCase))
                    bAllUsers = true;

                if (lArgs.Contains("--noretry", StringComparer.CurrentCultureIgnoreCase))
                    bRetry = false;

                List<string> lExclude = new List<string>();

                foreach (string sArg in lArgs.Skip(1))
                {
                    if (!sArg.StartsWith("--"))
                        bUpdateAll = false;
                }

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
                CancellationToken ct = new CancellationTokenSource(30000).Token;
                await oScan.GetSWRepositoryAsync(ct);

                ct = new CancellationTokenSource(30000).Token;
                await oScan.SWScanAsync(bUser, bMachine, bAllUsers, ct);
                oScan._CheckUpdates(null);

                List<string> lUpdate = new List<string>();
                if (!bUpdateAll)
                {
                    foreach (string sArg in lArgs.Skip(1))
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
                        Log.ForContext("Param", lArgs[0].ToLower()).Verbose("Excludes: {exclude}", scl);
                        lUpdate = oScan.NewSoftwareVersions.Where(r => !lExclude.Contains(r.ShortName.ToLower())).Select(t => t.ShortName.ToLower()).ToList();
                    }
                }

                if(lUpdate.Count == 0)
                {
                    Log.ForContext("Param", lArgs[0].ToLower()).Debug("No updates found.");
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

                        if (oRZSW.SoftwareUpdate == null || oRZSW.SoftwareUpdate.SW == null || string.IsNullOrEmpty(oRZSW.SoftwareUpdate.SW.ProductName))
                        {
                            Log.ForContext("Param", lArgs[0].ToLower()).Debug("RuckZuck Url: '{url}' customerid: '{customerid}'", RZRestAPIv2.sURL, RZRestAPIv2.CustomerID);
                            Log.ForContext("Param", lArgs[0].ToLower()).Warning("Shortname NOT available in repository: '{shortname}'", sArg);
                            bError = true;
                            continue;
                        }

                        if (string.IsNullOrEmpty(oRZSW.SoftwareUpdate.SW.PSInstall))
                        {
                            oRZSW.SoftwareUpdate.GetInstallType();
                            if (string.IsNullOrEmpty(oRZSW.SoftwareUpdate.SW.PSInstall))
                            {
                                Log.ForContext("Param", lArgs[0].ToLower()).Warning("PreRequisites not valid for '{shortname}'", sArg);
                                bError = false;
                                continue;
                            }
                        }

                        if (await InstallAsync(oRZSW))
                            continue;
                    }
                    else
                    {
                        try
                        {
                            RZUpdater oRZSW = new RZUpdater();
                            oRZSW.SoftwareUpdate = new SWUpdate(sArg.Trim('"').Trim());

                            if (oRZSW.SoftwareUpdate == null || oRZSW.SoftwareUpdate.SW == null || string.IsNullOrEmpty(oRZSW.SoftwareUpdate.SW.ProductName))
                            {
                                Log.ForContext("Param", lArgs[0].ToLower()).Debug("RuckZuck Url: '{url}' customerid: '{customerid}'", RZRestAPIv2.sURL, RZRestAPIv2.CustomerID);
                                Log.ForContext("Param", lArgs[0].ToLower()).Warning("Software NOT available in repository: '{productname}' '{productversion}' '{manufacturer}'", oRZSW.SoftwareUpdate.SW.ProductName, oRZSW.SoftwareUpdate.SW.ProductVersion, oRZSW.SoftwareUpdate.SW.Manufacturer);
                                bError = true;
                                continue;
                            }

                            if (string.IsNullOrEmpty(oRZSW.SoftwareUpdate.SW.PSInstall))
                            {
                                oRZSW.SoftwareUpdate.GetInstallType();
                                if (string.IsNullOrEmpty(oRZSW.SoftwareUpdate.SW.PSInstall))
                                {
                                    Log.ForContext("Param", lArgs[0].ToLower()).Warning("PreRequisites not valid for '{shortname}'", sArg);
                                    bError = false;
                                    continue;
                                }
                            }

                            if (await InstallAsync(oRZSW))
                                continue;
                        }
                        catch (Exception ex)
                        {
                            Log.ForContext("Param", lArgs[0].ToLower()).Error("E365: {ex}", ex.Message);
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

                        if (sProp == "shortname")
                            Console.WriteLine(RZRestAPIv2.GetJsonFromObject((await RZRestAPIv2.GetCatalogAsync()).Where(t => t.ShortName.ToLower().Contains(sSearch))).ToString());

                        if (sProp == "manufacturer")
                            Console.WriteLine(RZRestAPIv2.GetJsonFromObject((await RZRestAPIv2.GetCatalogAsync()).Where(t => t.Manufacturer.ToLower().Contains(sSearch))).ToString());

                        if (sProp == "productname" || sProp == "name")
                            Console.WriteLine(RZRestAPIv2.GetJsonFromObject((await RZRestAPIv2.GetCatalogAsync()).Where(t => t.ProductName.ToLower().Contains(sSearch))).ToString());

                        if (sProp == "description")
                            Console.WriteLine(RZRestAPIv2.GetJsonFromObject((await RZRestAPIv2.GetCatalogAsync()).Where(t => t.Description.ToLower().Contains(sSearch))).ToString());

                        if (sProp == "categories" || sProp == "category")
                            Console.WriteLine(RZRestAPIv2.GetJsonFromObject((await RZRestAPIv2.GetCatalogAsync()).Where(t => string.Join(";", t.Categories.ToArray()).ToLower().Contains(sSearch))).ToString());

                        if (sProp == "producturl" || sProp == "url")
                            Console.WriteLine(RZRestAPIv2.GetJsonFromObject((await RZRestAPIv2.GetCatalogAsync()).Where(t => t.ProductURL.ToLower().Contains(sSearch))).ToString());

                        if (sProp == "productversion" || sProp == "version")
                            Console.WriteLine(RZRestAPIv2.GetJsonFromObject((await RZRestAPIv2.GetCatalogAsync()).Where(t => t.ProductVersion.ToLower().Contains(sSearch))).ToString());

                        if (sProp == "isinstalled")
                        {
                            oScan = new RZScan(false, false);
                            await oScan.SWScanAsync();

                            CancellationToken ct = new CancellationTokenSource(30000).Token;
                            await oScan.GetSWRepositoryAsync(ct);

                            foreach (var osw in oScan.InstalledSoftware)
                            {
                                var oItem = oScan.SoftwareRepository.FirstOrDefault(t => t.ProductName == osw.ProductName && t.Manufacturer == osw.Manufacturer && t.ProductVersion == osw.ProductVersion);
                                if (oItem != null)
                                    oItem.isInstalled = true;
                            }

                            Console.WriteLine(RZRestAPIv2.GetJsonFromObject(oScan.SoftwareRepository.Where(t => t.isInstalled.ToString().ToLower().Contains(sSearch))).ToString());
                        }
                    }
                    else
                    {
                        string sSearch = lArgs[1].ToLower();
                        Console.WriteLine(RZRestAPIv2.GetJsonFromObject((await RZRestAPIv2.GetCatalogAsync()).Where(t => t.ProductName.ToLower().Contains(sSearch) || t.Manufacturer.ToLower().Contains(sSearch) || t.ShortName.ToLower().Contains(sSearch))).ToString());
                    }
                }
                else
                {
                    Console.WriteLine(RZRestAPIv2.GetJsonFromObject((await RZRestAPIv2.GetCatalogAsync())).ToString());
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

                        Log.ForContext("Param", lArgs[0].ToLower()).Verbose("showing '{productname}' '{productversion}' '{manufacturer}'", ProductName, ProductVersion, Manufacturer);
                        oRZSW.SoftwareUpdate = new SWUpdate(ProductName, ProductVersion, Manufacturer);

                        if (oRZSW.SoftwareUpdate == null  || oRZSW.SoftwareUpdate.SW == null || string.IsNullOrEmpty(oRZSW.SoftwareUpdate.SW.ProductName))
                        {
                            Log.ForContext("Param", lArgs[0].ToLower()).Debug("RuckZuck Url: '{url}' customerid: '{customerid}'", RZRestAPIv2.sURL, RZRestAPIv2.CustomerID);
                            Log.ForContext("Param", lArgs[0].ToLower()).Warning("Software NOT available in repository: '{productname}' '{productversion}' '{manufacturer}'", ProductName, ProductVersion, Manufacturer);
                            return 99;
                        }

                        Log.ForContext("Param", lArgs[0].ToLower()).Debug("Software from repo: '{ProductName}' '{ProductVersion}'", oRZSW.SoftwareUpdate.SW.ProductName, oRZSW.SoftwareUpdate.SW.ProductVersion);
                        Console.WriteLine(RZRestAPIv2.GetJsonFromObject(oRZSW.SoftwareUpdate.SW).ToString());

                        return 0;
                    }
                    catch (Exception ex)
                    {
                        Log.ForContext("Param", lArgs[0].ToLower()).Error("E468: {ex}", ex.Message);
                        return 1;
                    }
                }

                foreach (string sArg in lArgs.Skip(1))
                {
                    if (File.Exists(sArg))
                    {
                        RZUpdater oRZSW = new RZUpdater(sArg);
                        Console.WriteLine(RZRestAPIv2.GetJsonFromObject(oRZSW.SoftwareUpdate.SW).ToString());
                    }
                    else
                    {
                        try
                        {
                            Log.ForContext("Param", lArgs[0].ToLower()).Verbose("showing '{shortname}'", sArg);
                            RZUpdater oRZSW = new RZUpdater();
                            oRZSW.SoftwareUpdate = new SWUpdate(sArg.Trim('"').Trim());

                            Log.ForContext("Param", lArgs[0].ToLower()).Debug("Software from repo: '{ProductName}' '{ProductVersion}'", oRZSW.SoftwareUpdate.SW.ProductName, oRZSW.SoftwareUpdate.SW.ProductVersion);

                            if (string.IsNullOrEmpty(oRZSW.SoftwareUpdate.SW.ProductName))
                            {
                                Log.ForContext("Param", lArgs[0].ToLower()).Debug("RuckZuck Url: '{url}' customerid: '{customerid}'", RZRestAPIv2.sURL, RZRestAPIv2.CustomerID);
                                Log.ForContext("Param", lArgs[0].ToLower()).Warning("Shortname NOT available in repository: '{shortname}'", sArg);
                                bError = true;
                                continue;
                            }

                            Console.WriteLine(RZRestAPIv2.GetJsonFromObject(oRZSW.SoftwareUpdate.SW).ToString());

                            return 0;
                        }
                        catch (Exception ex)
                        {
                            Log.ForContext("Param", lArgs[0].ToLower()).Error("E500: {ex}", ex.Message);
                            return 1;
                        }
                    }
                }

                return 0;
            }

            return 0;
        }

        private static async Task<bool> InstallAsync(RZUpdater oRZSW)
        {
            if (string.IsNullOrEmpty(oRZSW.SoftwareUpdate.SW.PSInstall))
            {
                oRZSW.SoftwareUpdate.GetInstallType();
                if (string.IsNullOrEmpty(oRZSW.SoftwareUpdate.SW.PSInstall))
                {
                    Log.Information("PreRequisites not valid for: {shortname}", oRZSW.SoftwareUpdate.SW.ShortName);
                    Log.Verbose("Prerequistes: {prereq}", oRZSW.SoftwareUpdate.SW.PreRequisites);
                    return false;
                }
            }

            Log.Information("Processing '{manufacturer}' '{productname}' '{productversion}'", oRZSW.SoftwareUpdate.SW.Manufacturer, oRZSW.SoftwareUpdate.SW.ProductName, oRZSW.SoftwareUpdate.SW.ProductVersion);

            foreach (string sPreReq in oRZSW.SoftwareUpdate.SW.PreRequisites)
            {
                if (!string.IsNullOrEmpty(sPreReq))
                {
                    RZUpdater oRZSWPreReq = new RZUpdater();
                    oRZSWPreReq.SoftwareUpdate = new SWUpdate(sPreReq);
                    //Console.WriteLine();
                    Log.Information("Dependency download started: {Shortname}", sPreReq);
                    if (oRZSWPreReq.SoftwareUpdate.DownloadAsync().Result)
                    {
                        Log.Information("Dependency download finished: {Shortname}", sPreReq);
                        Log.Information("Dependency installation started: {Shortname}", sPreReq);
                        if (await oRZSWPreReq.SoftwareUpdate.InstallAsync(false, bRetry))
                        {
                            Log.Information("Dependency installation finished: {Shortname}", sPreReq);
                        }
                        else
                        {
                            Log.Error("Dependency installation failed: {Shortname}  error: {error}", sPreReq, oRZSWPreReq.SoftwareUpdate.downloadTask.ErrorMessage);
                            return false;
                        }
                    }
                    else
                    {
                        Log.Error("Dependency download failed: '{Shortname}' error: {error}  downloaded: {MB}MB", oRZSWPreReq.SoftwareUpdate.SW.ShortName, oRZSWPreReq.SoftwareUpdate.downloadTask.ErrorMessage, oRZSWPreReq.SoftwareUpdate.downloadTask.DownloadedBytes / 1024 / 1024);
                    }
                }
            }
            Log.Information("Download started: {Shortname} (all file size:{MB}MB)", oRZSW.SoftwareUpdate.SW.ShortName, oRZSW.SoftwareUpdate.SW.Files.Sum(t=>t.FileSize)/1024/1024);
            if (await oRZSW.SoftwareUpdate.DownloadAsync())
            {
                Log.Information("Download finished: '{Shortname}'  downloaded: {MB}MB", oRZSW.SoftwareUpdate.SW.ShortName, oRZSW.SoftwareUpdate.downloadTask.DownloadedBytes / 1024 / 1024);

                Log.Information("Installation started: {Shortname}", oRZSW.SoftwareUpdate.SW.ShortName);

                if (await oRZSW.SoftwareUpdate.InstallAsync(false, bRetry))
                {
                    Log.Information("Installation finished: {Shortname}", oRZSW.SoftwareUpdate.SW.ShortName);
                    return true;
                }
                else
                {
                    Log.Error("Installation failed: {Shortname} error: {error}", oRZSW.SoftwareUpdate.SW.ShortName, oRZSW.SoftwareUpdate.downloadTask.ErrorMessage);
                    return false;
                }
            }
            else
            {
                Log.Error("Download failed: '{Shortname}' error: {error}  downloaded: {MB}MB", oRZSW.SoftwareUpdate.SW.ShortName, oRZSW.SoftwareUpdate.downloadTask.ErrorMessage, oRZSW.SoftwareUpdate.downloadTask.DownloadedBytes / 1024 / 1024);
            }

            return false;
        }

        private static void OScan_OnUpdScanCompleted(object sender, EventArgs e)
        {
            Log.Information("Update-Scan completed. {count} updates found.", oScan.NewSoftwareVersions.Count);

            foreach (AddSoftware oSW in oScan.NewSoftwareVersions)
            {
                try
                {
                    if (Properties.Settings.Default.Excludes.Cast<string>().ToList().FirstOrDefault(t => t.ToLower() == oSW.ShortName.ToLower()) != null)
                    {
                        Log.Information("Skipping: '{shortname}' (excluded).", oSW.ShortName);
                        continue;
                    }
                    Console.WriteLine(oSW.Manufacturer + " " + oSW.ProductName + " new Version: " + oSW.ProductVersion);
                    Log.Information("Updating {manufacturer} {productname} to new version: {productversion}", oSW.Manufacturer, oSW.ProductName, oSW.ProductVersion);

                    oUpdate.SoftwareUpdate.SW = oSW;
                    oUpdate.SoftwareUpdate.GetInstallType();
                    Log.Information("Download started: {Shortname} (all file size:{MB}MB)", oSW.ShortName, oUpdate.SoftwareUpdate.downloadTask.Files.Sum(t => t.FileSize) / 1024 / 1024);
                    if (oUpdate.SoftwareUpdate.DownloadAsync().Result)
                    {
                        Log.Information("Download finished: {Shortname}  downloaded: {MB}MB", oSW.ShortName, oUpdate.SoftwareUpdate.downloadTask.DownloadedBytes / 1024 / 1024);
                        Log.Information("Installation started: {Shortname}", oSW.ShortName);
                        if (oUpdate.SoftwareUpdate.InstallAsync(false, bRetry).Result)
                        {
                            Log.Information("Installation finished: {Shortname}", oSW.ShortName);
                        }
                        else
                        {
                            Log.Error("Installation failed: {Shortname} error: {error}", oUpdate.SoftwareUpdate.SW.ShortName, oUpdate.SoftwareUpdate.downloadTask.ErrorMessage);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.ForContext("Param", "OScan_OnUpdScanCompleted").Error("E630: {ex}", ex.Message);
                }
            }
            bRunning = false;
        }
    }
}