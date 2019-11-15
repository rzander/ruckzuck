using Microsoft.Identity.Client;
using Newtonsoft.Json.Linq;
using RuckZuck.Base;
using RuckZuck_Tool;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RuckZuck_Tool
{
    class Intune_API
    {
        public static AuthenticationResult authResult; 

        public Intune_API()
        {

        }

        public void RuckZuckSync(AddSoftware oRZ, DLTask downloadTask, bool Bootstrap = false)
        {
            string PkgName = oRZ.ProductName;
            string PkgVersion = oRZ.ProductVersion;
            string Manufacturer = oRZ.Manufacturer.TrimEnd('.'); //Temp Fix
            Bootstrap = false;
            downloadTask.Installing = true;

            var lSW = GetRZSoftware(PkgName, PkgVersion, Manufacturer);

            foreach (var SW in lSW)
            {
                string SWID = "RZID" + SW.SWId.ToString();
                Boolean bDownloadStatus = true; 
                bool bPreReq = false;

                foreach (var oIT in RZRestAPIv2.GetSoftwares(SW.ProductName, SW.ProductVersion, SW.Manufacturer, "RZ4Intune"))
                {
                    try
                    {
                        if (oIT.Architecture.StartsWith("_prereq_"))
                        {
                            bPreReq = true;
                            continue;
                        }


                        RZUpdate.SWUpdate oUpd = new RZUpdate.SWUpdate(oIT);

                        downloadTask.Status = "Downloading File(s)...";
                        //Listener.WriteLine(SW.Shortname, "Creating DeploymentType: " + oIT.Architecture);
                        DirectoryInfo oDir = new DirectoryInfo(Path.Combine(Environment.GetEnvironmentVariable("TEMP"), oIT.ContentID.ToString()));

                        if (!Directory.Exists(oDir.FullName))
                        {
                            oDir = Directory.CreateDirectory(Path.Combine(Environment.GetEnvironmentVariable("TEMP"), oIT.ContentID.ToString()));
                        }
                        downloadTask.Status = "Downloading File(s)...";
                        //Listener.WriteLine(SW.Shortname, "Downloading File(s)...");
                        if (!Bootstrap)
                        {
                            bDownloadStatus = oUpd.Download(true, oDir.FullName).Result;

                            //DL Failed!
                            if (!bDownloadStatus)
                            {
                                downloadTask.Error = true;
                                downloadTask.ErrorMessage = "content download failed.";
                                Thread.Sleep(3000);
                            }
                        }
                    }
                    catch {

                        downloadTask.Status = "";
                        downloadTask.Installing = false;
                        downloadTask.Installed = true;
                        downloadTask.Error = true;
                    }

                    downloadTask.Status = "Creating Application...";
                    if (!Directory.Exists(Environment.ExpandEnvironmentVariables("%TEMP%\\intunewin")))
                        Directory.CreateDirectory(Environment.ExpandEnvironmentVariables("%TEMP%\\intunewin"));

                    File.WriteAllText(Environment.ExpandEnvironmentVariables("%TEMP%\\intunewin\\RZ4Intune.ps1"), Properties.Settings.Default.RZCreateAppPS);

                    Process.Start("powershell.exe", "-executionpolicy bypass -file " + Environment.ExpandEnvironmentVariables("%TEMP%\\intunewin\\RZ4Intune.ps1") + " \"" + oIT.ShortName + "\" \"" + authResult.AccessToken + "\" \"" + authResult.ExpiresOn.ToString() + "\" \"" + authResult.Account.Username + "\"").WaitForExit();

                    downloadTask.Status = "";
                    downloadTask.Installing = false;
                    downloadTask.Installed = true;
                    downloadTask.Error = false;
                }
            }
        }

        private List<GetSoftware> GetRZSoftware(string ProdName, string ProdVersion, string Manufacturer)
        {
            List<GetSoftware> oResult = new List<GetSoftware>();
            oResult.AddRange(RZRestAPIv2.GetCatalog().Where(t => t.ProductName == ProdName && t.Manufacturer == Manufacturer && t.ProductVersion == ProdVersion));

            return oResult;
        }

        public class GraphRZ
        {
            public long RZID { get; set; }
            public string Shortname { get; set; }
            public bool Bootstrap { get; set; }
            public string Version { get; set; }
        }

        public List<GraphRZ> getRZIDs()
        {
            List<GraphRZ> lResult = new List<GraphRZ>();
            try
            {
                var jres = GetHttpContentWithToken(Properties.Settings.Default.RZGetExistingAppsURL, authResult.AccessToken);
                var grapResult = JObject.Parse(jres.Result);
                foreach (var oRes in grapResult["value"].Children())
                {
                    GraphRZ oRZ = new GraphRZ();
                    try
                    {
                        string sNotes =oRes["notes"].Value<string>();
                        foreach(string sLine in sNotes.Split('\n'))
                        {
                            switch(sLine.Split(':')[0].ToUpper())
                            {
                                case ("RZID"):
                                    oRZ.RZID = long.Parse(sLine.Split(':')[1]);
                                    break;
                                case ("SHORTNAME"):
                                    oRZ.Shortname = sLine.Split(':')[1];
                                    break;
                                case ("VERSION"):
                                    oRZ.Version = sLine.Split(':')[1];
                                    break;
                            }
                        }

                        lResult.Add(oRZ);
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message, "getRZIDs");
            }
            return lResult;
        }

        /// <summary>
        /// Perform an HTTP GET request to a URL using an HTTP Authorization header
        /// </summary>
        /// <param name="url">The URL</param>
        /// <param name="token">The token</param>
        /// <returns>String containing the results of the GET operation</returns>
        public async Task<string> GetHttpContentWithToken(string url, string token)
        {
            var httpClient = new System.Net.Http.HttpClient();
            System.Net.Http.HttpResponseMessage response;
            try
            {
                var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, url);
                //Add the token in Authorization header
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                response = await httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();
                return content;
            }
            catch (Exception ex)
            {
                return ex.ToString();
            }
        }
    }
}

namespace RZUpdate
{
    public partial class SWUpdate
    {
        public async Task<bool> InstallCM(bool Force = false, bool Retry = false)
        {
            try
            {
                downloadTask.Downloading = false;
                downloadTask.Installing = true;
                ProgressDetails(downloadTask, EventArgs.Empty);

                //Check if RuckZuckis running...
                try
                {
                    using (var mutex = Mutex.OpenExisting(@"Global\RuckZuckCM"))
                    {
                        if (Retry)
                        {
                            Thread.Sleep(new TimeSpan(0, 0, 2));
                        }
                        else
                            return false;
                    }
                    GC.Collect();
                }
                catch
                {
                }

                bool bMutexCreated = false;
                using (Mutex mutex = new Mutex(false, "Global\\RuckZuckCM", out bMutexCreated))
                {
                    Intune_API oAPI = new Intune_API();
                    ProgressDetails(downloadTask, EventArgs.Empty);
                    bool bBootStrap = false;
                    if (!string.IsNullOrEmpty(SW.Author))
                    {
                        if (SW.Author == "BootstrapTrue")
                            bBootStrap = true;
                    }

                    oAPI.RuckZuckSync(SW, this.downloadTask, bBootStrap);
                    downloadTask.Installed = true;
                    downloadTask.Installing = false;
                    ProgressDetails(downloadTask, EventArgs.Empty);

                    if (bMutexCreated)
                        mutex.Close();
                }
                GC.Collect();

            }
            catch (Exception ex)
            {
                downloadTask.Status = "";
                downloadTask.Installing = false;
                downloadTask.Downloading = false;
                downloadTask.Error = true;
                downloadTask.ErrorMessage = ex.Message;
                ProgressDetails(downloadTask, EventArgs.Empty);
                return false;
            }
            return true;
        }

        //internal bool InstallCM_cmd(CMAPI oAPI, bool Force = false, bool Retry = false)
        //{
        //    try
        //    {
        //        downloadTask.Downloading = false;
        //        downloadTask.Installing = true;
        //        ProgressDetails(downloadTask, EventArgs.Empty);

        //        //Check if RuckZuckis running...
        //        try
        //        {
        //            using (var mutex = Mutex.OpenExisting(@"Global\RuckZuckCM"))
        //            {
        //                if (Retry)
        //                {
        //                    Thread.Sleep(new TimeSpan(0, 0, 2));
        //                }
        //                else
        //                    return false;
        //            }
        //            GC.Collect();
        //        }
        //        catch
        //        {
        //        }

        //        bool bMutexCreated = false;
        //        using (Mutex mutex = new Mutex(false, "Global\\RuckZuckCM", out bMutexCreated))
        //        {
        //            bool bBootStrap = false;
        //            if (!string.IsNullOrEmpty(SW.Author))
        //            {
        //                if (SW.Author == "BootstrapTrue")
        //                    bBootStrap = true;
        //            }

        //            oAPI.RuckZuckSync(SW, this.downloadTask, bBootStrap);
        //            downloadTask.Installed = true;
        //            downloadTask.Installing = false;
        //            ProgressDetails(downloadTask, EventArgs.Empty);

        //            if (bMutexCreated)
        //                mutex.Close();
        //        }
        //        GC.Collect();

        //    }
        //    catch (Exception ex)
        //    {
        //        downloadTask.Status = "";
        //        downloadTask.Installing = false;
        //        downloadTask.Downloading = false;
        //        downloadTask.Error = true;
        //        downloadTask.ErrorMessage = ex.Message;
        //        ProgressDetails(downloadTask, EventArgs.Empty);
        //        return false;
        //    }
        //    return true;
        //}

        public async Task<bool> Download(bool Enforce)
        {
            downloadTask.AutoInstall = true;
            downloadTask.SWUpd = this;
            downloadTask.PercentDownloaded = 100;
            downloadTask.Downloading = false;
            downloadTask.Installing = false;
            downloadTask.Status = "Connecting...";
            //Downloaded(downloadTask, EventArgs.Empty);
            ProgressDetails(downloadTask, EventArgs.Empty);
            //OnSWUpdated(this, new EventArgs());
            return true;
        }
    }


}

namespace RuckZuck.Base
{
    public partial class RZScan
    {
        //public static string AADToken;
        public Task SWScan()
        {
            Intune_API oAPI = new Intune_API();
            var tSWScan = Task.Run(() =>
            {
                scan(oAPI);
                OnUpdScanCompleted(this, new EventArgs());
                OnSWScanCompleted(this, new EventArgs());
            });

            return tSWScan;
        }

        internal void scan(Intune_API oAPI)
        {

            try
            {
                var lIDs = oAPI.getRZIDs();
                //File.AppendAllLines(Environment.ExpandEnvironmentVariables("%TEMP%\\dbg.txt"), new string[] { "Repository-Items:" + SoftwareRepository.Count.ToString() });

#if DEBUG
                System.IO.File.AppendAllLines(Environment.ExpandEnvironmentVariables("%TEMP%\\RZDebug.txt"), new string[] { DateTime.Now.ToString() + ";S0;" + "RZItems detected: ", lIDs.Count.ToString() });
#endif
#if DEBUG
                System.IO.File.AppendAllLines(Environment.ExpandEnvironmentVariables("%TEMP%\\RZDebug.txt"), new string[] { DateTime.Now.ToString() + ";S0;" + "Repository Items: ", SoftwareRepository.Count().ToString() });
#endif

                foreach (Intune_API.GraphRZ RZSW in lIDs)
                {
                    try
                    {
                        if (SoftwareRepository.Count(t => t.SWId == RZSW.RZID) == 0)
                        {
                            //File.AppendAllLines(Environment.ExpandEnvironmentVariables("%TEMP%\\dbg.txt"), new string[] { "not match, IconId:" + SQLRZ.RZID.ToString() });

                            var oSW = SoftwareRepository.FirstOrDefault(t => t.ShortName == RZSW.Shortname);

                            if (oSW != null)
                            {
                                AddSoftware oNew = new AddSoftware()
                                {
                                    ProductName = oSW.ProductName,
                                    ProductVersion = oSW.ProductVersion,
                                    Manufacturer = oSW.Manufacturer,
                                    ShortName = oSW.ShortName,
                                    Description = oSW.Description,
                                    SWId = oSW.SWId,
                                    IconHash = oSW.IconHash,
                                    MSIProductID = RZSW.Version
                            };

                            //if (RZSW.Bootstrap)
                            //    oNew.Author = "BootstrapTrue";
                            //else
                            //    oNew.Author = "BootstrapFalse";

#if DEBUG
                            System.IO.File.AppendAllLines(Environment.ExpandEnvironmentVariables("%TEMP%\\RZDebug.txt"), new string[] { DateTime.Now.ToString() + ";S1;" + "New SWVersion: ", oNew.ProductName, oNew.ProductVersion, oNew.MSIProductID });
#endif
                            NewSoftwareVersions.Add(oNew);
                        }
                    }
                        else
                    {
                        try
                        {
                            var oSW = SoftwareRepository.FirstOrDefault(t => t.SWId == RZSW.RZID);
                            if (oSW != null)
                            {
                                try
                                {
                                    AddSoftware oExisting = new AddSoftware()
                                    {
                                        ProductName = oSW.ProductName,
                                        ProductVersion = oSW.ProductVersion,
                                        Manufacturer = oSW.Manufacturer,
                                        ShortName = oSW.ShortName,
                                        Description = oSW.Description,
                                        IconHash = oSW.IconHash,
                                        SWId = oSW.SWId
                                    };

                                    //if (RZSW.Bootstrap)
                                    //    oExisting.Author = "BootstrapTrue";
                                    //else
                                    //    oExisting.Author = "BootstrapFalse";

#if DEBUG
                                    System.IO.File.AppendAllLines(Environment.ExpandEnvironmentVariables("%TEMP%\\RZDebug.txt"), new string[] { DateTime.Now.ToString() + ";S2;" + "Installed SWVersion: ", oExisting.ProductName, oExisting.ProductVersion });
#endif
                                    InstalledSoftware.Add(oExisting);
                                }
                                catch { }
                            }

                        }
                        catch { }
                    }
                }
                    catch (Exception ex)
            {
                System.IO.File.AppendAllLines(Environment.ExpandEnvironmentVariables("%TEMP%\\RZError.txt"), new string[] { DateTime.Now.ToString() + ";F1814E1" + ex.Message });
            }
        }

                //Cleanup SW where new Version already exists
                foreach (var oSW in InstalledSoftware)
                {
                    NewSoftwareVersions.RemoveAll(t => t.ShortName == oSW.ShortName);
                }
}
            catch (Exception ex)
            {
                System.IO.File.AppendAllLines(Environment.ExpandEnvironmentVariables("%TEMP%\\RZError.txt"), new string[] { DateTime.Now.ToString() + ";F1845E1" + ex.Message });
            }
            OnUpdScanCompleted(this, new EventArgs());
            OnSWScanCompleted(this, new EventArgs());

        }
    }
}
