using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel.Web;
using System.Net;
using System.Web;
using Microsoft.ServiceBus.Messaging;
using StackExchange.Redis;
using System.Threading.Tasks;
using System.Text;
using System.IO;
using System.ServiceModel;
using System.ServiceModel.Channels;
using Newtonsoft.Json;

namespace RuckZuck_WCF
{
    public class RZService : IRZService
    {
        SWEntitiesApi oSW = new SWEntitiesApi();
        MessagingFactorySettings settings = new MessagingFactorySettings();
        TopicClient tcRuckZuck = TopicClient.CreateFromConnectionString(Properties.Settings.Default.ServiceBusConnection, "RuckZuck");

        private static Lazy<ConnectionMultiplexer> lazyConnection = new Lazy<ConnectionMultiplexer>(() =>
        {
            return ConnectionMultiplexer.Connect("ruckzuck2.redis.cache.windows.net ,abortConnect=false,ssl=true,password=hXptN4dMrrAaXcDrEY0RSZUjL6/X7WWol/fMZlSgbO0=");
        });

        public static ConnectionMultiplexer Connection
        {
            get
            {
                return lazyConnection.Value;
            }
        }

        public string AuthenticateUser()
        {
            IncomingWebRequestContext request = WebOperationContext.Current.IncomingRequest;
            WebHeaderCollection headers = request.Headers;
            IDatabase cache = Connection.GetDatabase();

            string Username = headers["Username"] ?? "";
            string Password = headers["Password"] ?? "";


            if (Username == "FreeRZ")
            {
                try
                {
                    byte[] data = Convert.FromBase64String(Password);
                    DateTime when = DateTime.FromBinary(BitConverter.ToInt64(data, 0));
                    if ((DateTime.UtcNow - when) <= new TimeSpan(2, 0, 10))
                    {
                        // Create and store the AuthenticatedToken before returning it
                        string token = Guid.NewGuid().ToString();

                        try
                        {
                            cache.StringSetAsync(token, Username, new TimeSpan(1, 0, 0));
                        }
                        catch { }
                        return token; // "deecdc6b-ad08-42ab-a743-a3c0f9033c80";
                    }
                }
                catch {}

                return "";
            }
            else
            {
                if (System.Web.Security.Membership.ValidateUser(Username, Password))
                {
                    try
                    {
                        tcRuckZuck.SendAsync(new BrokeredMessage() { Label = "RuckZuck/WCF/Authentication/" + Username, TimeToLive = new TimeSpan(8, 0, 0) });
                    }
                    catch { }

                    // Create and store the AuthenticatedToken before returning it
                    string token = Guid.NewGuid().ToString();

                    try
                    {
                        cache.StringSetAsync(token, Username, new TimeSpan(1,0,0));
                    }
                    catch { }

                    /*HttpRuntime.Cache.Add(
                        token,
                        Username,
                        null,
                        System.Web.Caching.Cache.NoAbsoluteExpiration,
                        TimeSpan.FromMinutes(45),
                        System.Web.Caching.CacheItemPriority.Normal,
                        null);*/
                    return token;
                }
            }

            return "";
        }

        private bool IsUserValid()
        {
            try
            {
                IncomingWebRequestContext request = WebOperationContext.Current.IncomingRequest;
                WebHeaderCollection headers = request.Headers;
                IDatabase cache = Connection.GetDatabase();

                string sToken = headers["AuthenticatedToken"] ?? "";
                string sUsername = headers["Username"] ?? "";
                string sPassword = headers["Password"] ?? "";

                //Do we have a Token?
                if (!string.IsNullOrEmpty(sToken))
                {
                    //FreeRZ user token...
                    if (sToken == "deecdc6b-ad08-42ab-a743-a3c0f9033c80")
                        return true;

                    //Do we have a cached token... ?
                    if(cache.StringGet(sToken) != RedisValue.Null)
                    {
                        return true;
                    }

                    /*if (HttpRuntime.Cache[sToken] != null)
                    {
                        return true;
                    }*/

                }
                //No token; use Username and password
                else
                {
                    return System.Web.Security.Membership.ValidateUser(sUsername, sPassword);
                }
            }
            catch { }

            return false;
        }

        public List<GetSoftware> SWResults(string SearchPattern)
        {
            IDatabase cache = Connection.GetDatabase();

            if (SearchPattern == "*")
                SearchPattern = "";

            /*if (!IsUserValid())
               return new List<GetSoftware>();*/

            List<GetSoftware> oResult = new List<GetSoftware>();

            IDatabase cache4 = Connection.GetDatabase(4);
            if (string.IsNullOrEmpty(SearchPattern))
            {
                string sAll = cache4.StringGet("ALL");
                if (!string.IsNullOrEmpty(sAll))
                {
                    try
                    {
                        oResult = JsonConvert.DeserializeObject<List<GetSoftware>>(sAll);
                        return oResult;
                    }
                    catch { }

                }
            }

            List<vAllSWDetails> oFoundItems = new List<vAllSWDetails>();
            bool bTemplate = false;
            if (SearchPattern == "--OLD--")
            {
                oFoundItems.AddRange(oSW.vAllSWDetails.OrderBy(t => t.LastStatus).Take(30));

                oFoundItems = oFoundItems.GroupBy(x => new { x.ProductName, x.Version, x.Manufacturer }).Select(cl => new vAllSWDetails
                {
                    Categories = cl.First().Categories,
                    DownloadURL = cl.First().DownloadURL,
                    Failed = cl.Sum(c => c.Failed),
                    FileHash = cl.First().FileHash,
                    Filename = cl.First().Filename,
                    //Icon = cl.First().Icon,
                    IconId = cl.First().IconId,
                    IsLatest = cl.First().IsLatest,
                    LastStatus = cl.First().LastStatus,
                    Manufacturer = cl.First().Manufacturer,
                    ProductName = cl.First().ProductName,
                    ProductDescription = cl.First().ProductDescription,
                    ProjectURL = cl.First().ProjectURL,
                    PSDetection = cl.First().PSDetection,
                    PSInstall = cl.First().PSInstall,
                    PSPostInstall = cl.First().PSPostInstall,
                    PSPreInstall = cl.First().PSPreInstall,
                    PSPreReq = cl.First().PSPreReq,
                    PSUninstall = cl.First().PSUninstall,
                    ShortName = cl.First().ShortName,
                    Success = cl.Sum(c => c.Success),
                    SuccessRatio = 100 - ((cl.Sum(c => c.Failed ?? 1) * 100) / (cl.Sum(c => c.Success ?? 1) > 0 ? (cl.Sum(c => c.Success ?? 1)) : 1)),  //Convert.ToInt32(cl.Average(c => c.SuccessRatio) ?? 0),
                    Type = cl.First().Type,
                    Version = cl.First().Version,
                    Downloads = cl.Sum(c => c.Downloads)
                }).ToList();

                oFoundItems = oFoundItems.Take(20).ToList();
                bTemplate = true;
            }

            if (SearchPattern == "--BAD--")
            {
                oFoundItems.AddRange(oSW.vAllSWDetails.Where(t => t.SuccessRatio < 90).OrderBy(t => t.SuccessRatio).Take(20));
                oFoundItems = oFoundItems.GroupBy(x => new { x.ProductName, x.Version, x.Manufacturer }).Select(cl => new vAllSWDetails
                {
                    Categories = cl.First().Categories,
                    DownloadURL = cl.First().DownloadURL,
                    Failed = cl.Sum(c => c.Failed),
                    FileHash = cl.First().FileHash,
                    Filename = cl.First().Filename,
                    //Icon = cl.First().Icon,
                    IconId = cl.First().IconId,
                    IsLatest = cl.First().IsLatest,
                    LastStatus = cl.First().LastStatus,
                    Manufacturer = cl.First().Manufacturer,
                    ProductName = cl.First().ProductName,
                    ProductDescription = cl.First().ProductDescription,
                    ProjectURL = cl.First().ProjectURL,
                    PSDetection = cl.First().PSDetection,
                    PSInstall = cl.First().PSInstall,
                    PSPostInstall = cl.First().PSPostInstall,
                    PSPreInstall = cl.First().PSPreInstall,
                    PSPreReq = cl.First().PSPreReq,
                    PSUninstall = cl.First().PSUninstall,
                    ShortName = cl.First().ShortName,
                    Success = cl.Sum(c => c.Success),
                    SuccessRatio = 100 - ((cl.Sum(c => c.Failed ?? 1) * 100) / (cl.Sum(c => c.Success ?? 1) > 0 ? (cl.Sum(c => c.Success ?? 1)) : 1)),  //Convert.ToInt32(cl.Average(c => c.SuccessRatio) ?? 0),
                    Type = cl.First().Type,
                    Version = cl.First().Version,
                    Downloads = cl.Sum(c => c.Downloads)
                }).ToList();
                oFoundItems = oFoundItems.Take(20).ToList();
                bTemplate = true;
            }

            if (SearchPattern == "--ISSUE--")
            {
                var lIssue = oSW.vProductsWithIssues.Select(t=>t.ShortName).Distinct().ToList();
                oFoundItems.AddRange(oSW.vAllSWDetails.Where(t => lIssue.Contains(t.ShortName)).Take(20));
                oFoundItems = oFoundItems.GroupBy(x => new { x.ProductName, x.Version, x.Manufacturer }).Select(cl => new vAllSWDetails
                {
                    Categories = cl.First().Categories,
                    DownloadURL = cl.First().DownloadURL,
                    Failed = cl.Sum(c => c.Failed),
                    FileHash = cl.First().FileHash,
                    Filename = cl.First().Filename,
                    //Icon = cl.First().Icon,
                    IconId = cl.First().IconId,
                    IsLatest = cl.First().IsLatest,
                    LastStatus = cl.First().LastStatus,
                    Manufacturer = cl.First().Manufacturer,
                    ProductName = cl.First().ProductName,
                    ProductDescription = cl.First().ProductDescription,
                    ProjectURL = cl.First().ProjectURL,
                    PSDetection = cl.First().PSDetection,
                    PSInstall = cl.First().PSInstall,
                    PSPostInstall = cl.First().PSPostInstall,
                    PSPreInstall = cl.First().PSPreInstall,
                    PSPreReq = cl.First().PSPreReq,
                    PSUninstall = cl.First().PSUninstall,
                    ShortName = cl.First().ShortName,
                    Success = cl.Sum(c => c.Success),
                    SuccessRatio = 100 - ((cl.Sum(c => c.Failed ?? 1) * 100) / (cl.Sum(c => c.Success ?? 1) > 0 ? (cl.Sum(c => c.Success ?? 1)) : 1)),  //Convert.ToInt32(cl.Average(c => c.SuccessRatio) ?? 0),
                    Type = cl.First().Type,
                    Version = cl.First().Version,
                    Downloads = cl.Sum(c => c.Downloads)
                }).ToList();
                oFoundItems = oFoundItems.Take(20).ToList();
                bTemplate = true;
            }

            if (SearchPattern == "--NEW--")
            {
                oFoundItems.AddRange(oSW.vAllSWDetails.OrderByDescending(t => t.LastModified).Take(30));
                oFoundItems = oFoundItems.GroupBy(x => new { x.ProductName, x.Version, x.Manufacturer }).Select(cl => new vAllSWDetails
                {
                    Categories = cl.First().Categories,
                    DownloadURL = cl.First().DownloadURL,
                    Failed = cl.Sum(c => c.Failed),
                    FileHash = cl.First().FileHash,
                    Filename = cl.First().Filename,
                    //Icon = cl.First().Icon,
                    IconId = cl.First().IconId,
                    IsLatest = cl.First().IsLatest,
                    LastStatus = cl.First().LastStatus,
                    Manufacturer = cl.First().Manufacturer,
                    ProductName = cl.First().ProductName,
                    ProductDescription = cl.First().ProductDescription,
                    ProjectURL = cl.First().ProjectURL,
                    PSDetection = cl.First().PSDetection,
                    PSInstall = cl.First().PSInstall,
                    PSPostInstall = cl.First().PSPostInstall,
                    PSPreInstall = cl.First().PSPreInstall,
                    PSPreReq = cl.First().PSPreReq,
                    PSUninstall = cl.First().PSUninstall,
                    ShortName = cl.First().ShortName,
                    Success = cl.Sum(c => c.Success),
                    SuccessRatio = 100 - ((cl.Sum(c => c.Failed ?? 1) * 100) / (cl.Sum(c => c.Success ?? 1) > 0 ? (cl.Sum(c => c.Success ?? 1)) : 1)),  //Convert.ToInt32(cl.Average(c => c.SuccessRatio) ?? 0),
                    Type = cl.First().Type,
                    Version = cl.First().Version,
                    Downloads = cl.Sum(c => c.Downloads)
                }).ToList();
                oFoundItems = oFoundItems.Take(20).ToList();
                bTemplate = true;
            }

            if (SearchPattern == "--APPROVE--")
            {
                var oRes = oSW.vPendingSoftwareRequests.Take(30).ToList(); 

                oFoundItems = oRes.GroupBy(x => new { x.ProductName, x.Version, x.Manufacturer }).Select(cl => new vAllSWDetails
                {
                    Categories = "",
                    DownloadURL = cl.First().DownloadURL,
                    Failed = 0,
                    FileHash = cl.First().FileHash,
                    Filename = cl.First().Filename,
                    //Icon = cl.First().Icon,
                    IconId = cl.First().ProductVersionId,
                    IsLatest = cl.First().IsLatest,
                    LastStatus = DateTime.Now,
                    Manufacturer = cl.First().Manufacturer,
                    ProductName = cl.First().ProductName,
                    ProductDescription = cl.First().ProductDescription,
                    ProjectURL = cl.First().ProjectURL,
                    PSDetection = cl.First().PSDetection,
                    PSInstall = cl.First().PSInstall,
                    PSPostInstall = cl.First().PSPostInstall,
                    PSPreInstall = cl.First().PSPreInstall,
                    PSPreReq = cl.First().PSPreReq,
                    PSUninstall = cl.First().PSUnInstall,
                    ShortName = cl.First().ShortName,
                    Success = 0,
                    SuccessRatio = 0,  //Convert.ToInt32(cl.Average(c => c.SuccessRatio) ?? 0),
                    Type = cl.First().Type,
                    Version = cl.First().Version,
                    Downloads = 0
                }).ToList();
                oFoundItems = oFoundItems.Take(20).ToList();
                bTemplate = true;
            }

            if (!bTemplate)
            {
                int iCount = 1;
                oFoundItems.AddRange(oSW.vAllSWDetails.Where(t => t.ShortName.Contains(SearchPattern)));
                if (!string.IsNullOrEmpty(SearchPattern))
                {
                    oFoundItems.AddRange(oSW.vAllSWDetails.Where(t => t.ProductName.Contains(SearchPattern)));
                    iCount++;
                }

                if (oFoundItems.Count == 0)
                {
                    iCount = 1;
                    oFoundItems.AddRange(oSW.vAllSWDetails.Where(t => t.ProductDescription.Contains(SearchPattern)));
                }
                oFoundItems = oFoundItems.GroupBy(x => new { x.ProductName, x.Version, x.Manufacturer }).Select(cl => new vAllSWDetails
                {
                    Categories = cl.First().Categories,
                    DownloadURL = cl.First().DownloadURL,
                    Failed = cl.Sum(c => c.Failed),
                    FileHash = cl.First().FileHash,
                    Filename = cl.First().Filename,
                    //Icon = cl.First().Icon,
                    IconId = cl.First().IconId,
                    IsLatest = cl.First().IsLatest,
                    LastStatus = cl.First().LastStatus,
                    Manufacturer = cl.First().Manufacturer,
                    ProductName = cl.First().ProductName,
                    ProductDescription = cl.First().ProductDescription,
                    ProjectURL = cl.First().ProjectURL,
                    PSDetection = cl.First().PSDetection,
                    PSInstall = cl.First().PSInstall,
                    PSPostInstall = cl.First().PSPostInstall,
                    PSPreInstall = cl.First().PSPreInstall,
                    PSPreReq = cl.First().PSPreReq,
                    PSUninstall = cl.First().PSUninstall,
                    ShortName = cl.First().ShortName,
                    Success = cl.Sum(c => c.Success),
                    SuccessRatio = 100 - ((cl.Sum(c => c.Failed ?? 1) * 100) / (cl.Sum(c => c.Success ?? 1) > 0 ? (cl.Sum(c => c.Success ?? 1)) : 1)),  //Convert.ToInt32(cl.Average(c => c.SuccessRatio) ?? 0),
                    Type = cl.First().Type,
                    Version = cl.First().Version,
                    Downloads = cl.Sum(c => c.Downloads) / iCount
                }).ToList();
            }

            foreach (vAllSWDetails oItem in oFoundItems)
            {
                try
                {
                    GetSoftware oRItem = new GetSoftware()
                    {
                        Description = oItem.ProductDescription,
                        //Image = oItem.Icon,
                        Manufacturer = oItem.Manufacturer,
                        ProductName = oItem.ProductName,
                        ProductURL = oItem.ProjectURL,
                        ProductVersion = oItem.Version,
                        Shortname = oItem.ShortName,
                        Quality = oItem.SuccessRatio,
                        Downloads = oItem.Downloads,
                        IconId = oItem.IconId
                    };

                    try
                    {
                        if (oItem.Categories != null)
                        {
                            oRItem.Categories = oItem.Categories.Split(',').ToList();
                            if (oRItem.Categories.Count == 0)
                                oRItem.Categories.Add("Other");
                        }
                        else
                        {
                            oRItem.Categories = new List<string>();
                            oRItem.Categories.Add("Other");
                        }
                    }
                    catch { }

                    oResult.Add(oRItem);
                }
                catch { }
            }

            try
            {
                BrokeredMessage bMSG = new BrokeredMessage() { Label = "RuckZuck/WCF/SWResults/" + SearchPattern, TimeToLive = new TimeSpan(4, 0, 0) };
                bMSG.Properties.Add("Count", oResult.Count);
                tcRuckZuck.SendAsync(bMSG);
            }
            catch { }

            if (string.IsNullOrEmpty(SearchPattern))
            {
                cache4.StringSetAsync("ALL", JsonConvert.SerializeObject(oResult), new TimeSpan(1, 0, 0));
            }

            return oResult;
        }

        public List<GetSoftware> SWGetByShortname(string PkgName)
        {
            return SWGetByPkgNameAndVersion(PkgName, "");
        }

        public List<GetSoftware> SWGetByPkgNameAndVersion(string PkgName, string PkgVersion = "")
        {
            if (!IsUserValid())
                return new List<GetSoftware>();

            //PkgName = WebUtility.UrlDecode(PkgName);
            //PkgVersion = WebUtility.UrlDecode(PkgVersion);

            List<GetSoftware> oResult = new List<GetSoftware>();
            List<vAllSWDetails> oFoundItems = new List<vAllSWDetails>();
            oFoundItems.AddRange(oSW.vAllSWDetails.Where(t => t.ProductName == PkgName & t.Version == PkgVersion));

            if (oFoundItems.Count() == 0)
            {
                oFoundItems.AddRange(oSW.vAllSWDetails.Where(t => t.ShortName == PkgName & t.IsLatest ?? false));
            }

            //remove duplicates
            var distinctList = oFoundItems.GroupBy(x => new { x.ProductName, x.Version, x.Manufacturer })
                         .Select(g => g.First())
                         .ToList();

            foreach (vAllSWDetails oItem in distinctList)
            {
                try
                {
                    GetSoftware oRItem = new GetSoftware()
                    {
                        Description = oItem.ProductDescription,
                        //Image = oItem.Icon,
                        Manufacturer = oItem.Manufacturer,
                        ProductName = oItem.ProductName,
                        ProductURL = oItem.ProjectURL,
                        ProductVersion = oItem.Version,
                        Shortname = oItem.ShortName,
                        Quality = oItem.SuccessRatio,
                        Downloads = oItem.Downloads,
                        IconId = oItem.IconId
                    };

                    try
                    {
                        oRItem.Categories = oItem.Categories.Split(',').ToList();
                        if (oRItem.Categories.Count == 0)
                            oRItem.Categories.Add("Other");
                    }
                    catch { }

                    if (oRItem.Categories == null)
                        oRItem.Categories = new List<string>() { "Other" };


                    oResult.Add(oRItem);
                }
                catch { }
            }
            try
            {
                BrokeredMessage bMSG = new BrokeredMessage() { Label = "RuckZuck/WCF/SWGet/" + PkgName + ";" + PkgVersion , TimeToLive = new TimeSpan(4, 0, 0) };
                bMSG.Properties.Add("Count", oResult.Count);
                tcRuckZuck.SendAsync(bMSG);
            }
            catch { }

            return oResult;
        }

        public bool UploadSWEntry(AddSoftware SoftwareItem)
        {
            if (!IsUserValid())
                return false;
            try
            {
                Product oProduct;
                ProductVersion oProductVersion;
                WindowsInstallerIDs oMSI;
                DeploymentType oDT = new DeploymentType();

                try
                {
                    BrokeredMessage bMSG = new BrokeredMessage() { Label = "RuckZuck/WCF/UploadSWEntry/" + SoftwareItem.ProductName + ";" + SoftwareItem.ProductVersion, TimeToLive = new TimeSpan(24, 0, 0) };
                    bMSG.Properties.Add("User", SoftwareItem.Author);
                    tcRuckZuck.SendAsync(bMSG);
                }
                catch { }

                //Check if Product already exists
                if (string.IsNullOrEmpty(SoftwareItem.Manufacturer))
                    oProduct = oSW.Product.FirstOrDefault(t => t.ProductName == SoftwareItem.ProductName);
                else
                    oProduct = oSW.Product.FirstOrDefault(t => t.ProductName == SoftwareItem.ProductName & t.Manufacturer == SoftwareItem.Manufacturer);

                if (oProduct == null)
                {
                    string sShortName = "";

                    if (SoftwareItem.ProductName.StartsWith("Mp3tag "))
                        sShortName = "Mp3tag";

                    if (SoftwareItem.ProductName.StartsWith("K-Lite Codec Pack "))
                        sShortName = "K-LiteCodecPack";

                    if (SoftwareItem.ProductName.StartsWith("FileZilla Client "))
                        sShortName = "FileZilla";

                    if (SoftwareItem.ProductName.StartsWith("GIMP "))
                        sShortName = "GIMP";

                    if (SoftwareItem.ProductName.StartsWith("Greenshot "))
                        sShortName = "Greenshot";

                    if (SoftwareItem.ProductName.StartsWith("Inkscape "))
                        sShortName = "Inkscape";

                    if (SoftwareItem.ProductName.StartsWith("Malwarebytes Anti-Malware version "))
                        sShortName = "Malwarebytes";

                    if (SoftwareItem.ProductName.StartsWith("KeePass Password Safe "))
                        sShortName = "KeePass";

                    if (SoftwareItem.ProductName.StartsWith("MediaInfo 0."))
                        sShortName = "MediaInfo";

                    if (SoftwareItem.ProductName.StartsWith("Mozilla Thunderbird "))
                        sShortName = "Thunderbird";

                    if (SoftwareItem.ProductName.StartsWith("OpenVPN 2."))
                        sShortName = "OpenVPN";

                    if (SoftwareItem.ProductName.StartsWith("PuTTY version 0."))
                        sShortName = "PuTTY";

                    if (SoftwareItem.ProductName.StartsWith("R for Windows "))
                        sShortName = "R-Project";

                    if (SoftwareItem.ProductName.StartsWith("TreeSize Free V"))
                        sShortName = "TreeSizeFree";

                    if (SoftwareItem.ProductName.StartsWith("VidCoder 1."))
                        sShortName = "VidCoder";

                    if (SoftwareItem.ProductName.StartsWith("VLC media player "))
                        sShortName = "VLC";

                    if (SoftwareItem.ProductName.StartsWith("WinRAR "))
                        sShortName = "WinRAR";

                    if (SoftwareItem.ProductName.StartsWith("WinSCP "))
                        sShortName = "WinSCP";

                    if (SoftwareItem.ProductName.StartsWith("Wireshark 1."))
                        sShortName = "WireShark";

                    if (SoftwareItem.ProductName.StartsWith("XMind "))
                        sShortName = "XMind";

                    //11.Jan.2015
                    if (SoftwareItem.ProductName.StartsWith("Secunia PSI "))
                        sShortName = "Secunia PSI";
                    //17.1.2015
                    if (SoftwareItem.ProductName.StartsWith("TeraCopy  "))
                        sShortName = "TeraCopy";

                    //7.2.2015
                    if (SoftwareItem.ProductName.StartsWith("7-Zip 9."))
                        sShortName = "7-Zip";
                    if (SoftwareItem.ProductName.StartsWith("Adobe Reader XI (11."))
                        sShortName = "AdobeReader";
                    if (SoftwareItem.ProductName.StartsWith("Advanced Installer "))
                        sShortName = "AdvancedInstaller";
                    if (SoftwareItem.ProductName.StartsWith("AutoHotkey 1."))
                        sShortName = "AutoHotkey";
                    if (SoftwareItem.ProductName.StartsWith("AutoIt v3"))
                        sShortName = "AutoIt";
                    if (SoftwareItem.ProductName.StartsWith("Beyond Compare "))
                        sShortName = "BeyondCompare";
                    if (SoftwareItem.ProductName.StartsWith("Bluefish "))
                        sShortName = "Bluefish";
                    if (SoftwareItem.ProductName.StartsWith("CutePDF Writer "))
                        sShortName = "CutePDF Writer";
                    if (SoftwareItem.ProductName.StartsWith("Edraw Max "))
                        sShortName = "Edraw Max";
                    if (SoftwareItem.ProductName.StartsWith("Evernote v"))
                        sShortName = "Evernote";
                    if (SoftwareItem.ProductName.StartsWith("FastStone Capture "))
                        sShortName = "FastStone Capture";
                    if (SoftwareItem.ProductName.StartsWith("FastStone Image Viewer "))
                        sShortName = "FastStone Image Viewer";
                    if (SoftwareItem.ProductName.StartsWith("foobar2000 v"))
                        sShortName = "foobar2000";
                    if (SoftwareItem.ProductName.StartsWith("Java 7 Update"))
                        sShortName = "JavaRuntime8";
                    if (SoftwareItem.ProductName.StartsWith("Java 7 Update") & SoftwareItem.ProductName.EndsWith(" (64-bit)"))
                        sShortName = "JavaRuntime8x64";
                    if (SoftwareItem.ProductName.StartsWith("Java 8 Update"))
                        sShortName = "JavaRuntime8";
                    if (SoftwareItem.ProductName.StartsWith("Java 8 Update") & SoftwareItem.ProductName.EndsWith(" (64-bit)"))
                        sShortName = "JavaRuntime8x64";
                    if (SoftwareItem.ProductName.StartsWith("Java 6 Update"))
                        sShortName = "JavaRuntime8";
                    if (SoftwareItem.ProductName.StartsWith("Java 6 Update") & SoftwareItem.ProductName.EndsWith(" (64-bit)"))
                        sShortName = "JavaRuntime8x64";
                    if (SoftwareItem.ProductName.StartsWith("Opera Stable "))
                        sShortName = "Opera";
                    if (SoftwareItem.ProductName.StartsWith("Paint.NET v"))
                        sShortName = "Paint.Net";
                    if (SoftwareItem.ProductName.StartsWith("PDF24 Creator "))
                        sShortName = "PDF24";
                    if (SoftwareItem.ProductName.StartsWith("PostgreSQL "))
                        sShortName = "PostgreSQL";
                    if (SoftwareItem.ProductName.StartsWith("Royal TS "))
                        sShortName = "RoyalTS";
                    if (SoftwareItem.ProductName.StartsWith("Skype™ "))
                        sShortName = "Skype";
                    if (SoftwareItem.ProductName.StartsWith("Stellarium "))
                        sShortName = "Stellarium";
                    if (SoftwareItem.ProductName.StartsWith("Sweet Home 3D version "))
                        sShortName = "SweetHome3D";
                    if (SoftwareItem.ProductName.StartsWith("UltraISO Premium V"))
                        sShortName = "UltraISO";
                    if (SoftwareItem.ProductName.StartsWith("VMware vSphere Client "))
                        sShortName = "VMware vSphere Client";
                    if (SoftwareItem.ProductName.StartsWith("WinPcap "))
                        sShortName = "WinPcap";
                    if (SoftwareItem.ProductName.StartsWith("WinMerge "))
                        sShortName = "WinMerge";

                    //7.3.2015
                    if (SoftwareItem.ProductName.StartsWith("DVDStyler v"))
                        sShortName = "DVDStyler";
                    if (SoftwareItem.ProductName.StartsWith("LMMS "))
                        sShortName = "LMMS";
                    if (SoftwareItem.ProductName.StartsWith("scilab-5"))
                        sShortName = "Scilab";
                    if (SoftwareItem.ProductName.StartsWith("qBittorrent "))
                        sShortName = "qBittorrent";
                    if (SoftwareItem.ProductName.StartsWith("Audacity "))
                        sShortName = "Audacity";

                    //23.3.2015
                    if (SoftwareItem.ProductName.StartsWith("Snagit 1"))
                        sShortName = "Snagit";
                    if (SoftwareItem.ProductName.StartsWith("WinX DVD Ripper 5"))
                        sShortName = "WinX DVD Ripper";
                    if (SoftwareItem.ProductName.StartsWith("WinX DVD Ripper Platinum "))
                        sShortName = "WinX DVD Ripper Platinum";
                    if (SoftwareItem.ProductName.StartsWith("Microsoft Azure PowerShell -"))
                        sShortName = "Microsoft Azure PowerShell";
                    if (SoftwareItem.ProductName.StartsWith("Any Video Converter Ultimate "))
                        sShortName = "Any Video Converter Ultimate";
                    if (SoftwareItem.ProductName.StartsWith("Any Video Converter 5"))
                        sShortName = "Any Video Converter";
                    if (SoftwareItem.ProductName.StartsWith("MySQL Workbench "))
                        sShortName = "MySQL Workbench";
                    if (SoftwareItem.ProductName.StartsWith("Oracle VM VirtualBox 4"))
                        sShortName = "VirtualBox";
                    if (SoftwareItem.ProductName.StartsWith("Sandboxie 4."))
                        sShortName = "Sandboxie";
                    if (SoftwareItem.ProductName.StartsWith("ShareX 9"))
                        sShortName = "ShareX";
                    if (SoftwareItem.ProductName.StartsWith("SoapUI 5."))
                        sShortName = "SoapUI";
                    if (SoftwareItem.ProductName.StartsWith("Solve Elec "))
                        sShortName = "Solve Elec";
                    if (SoftwareItem.ProductName.StartsWith("Icecream Screen Recorder version "))
                        sShortName = "Icecream Screen Recorder";

                    //21.4.2015
                    if (SoftwareItem.ProductName.StartsWith("SMPlayer "))
                        sShortName = "SMPlayer";

                    //18.5.15
                    if (SoftwareItem.ProductName.StartsWith("Nmap "))
                        sShortName = "Nmap";
                    if (SoftwareItem.ProductName.StartsWith("Xamarin Studio "))
                        sShortName = "Xamarin Studio";

                    //25.5.2015
                    if (SoftwareItem.ProductName.StartsWith("WinZip "))
                        sShortName = "WinZip";
                    if (SoftwareItem.ProductName.StartsWith("1Password "))
                        sShortName = "1Password";

                    //19.6.2015
                    if (SoftwareItem.ProductName.StartsWith("LibreOffice "))
                        sShortName = "LibreOffice";

                    //21.12.2015
                    if (SoftwareItem.ProductName.StartsWith("MakeMKV v"))
                        sShortName = "MakeMKV";
                    if (SoftwareItem.ProductName.StartsWith("Krita Desktop (x64) "))
                        sShortName = "Krita";
                    if (SoftwareItem.ProductName.StartsWith("IsoBuster "))
                        sShortName = "IsoBuster";
                    if (SoftwareItem.ProductName.StartsWith("Media Player Codec Pack "))
                        sShortName = "Media Player Codec Pack";
                    if (SoftwareItem.ProductName.StartsWith("TortoiseHg "))
                        sShortName = "TortoiseHg";
                    if (SoftwareItem.ProductName.StartsWith("Git version "))
                        sShortName = "Git";

                    //25.2.2016
                    if (SoftwareItem.ProductName.StartsWith("XnView "))
                        sShortName = "XnView";
                    if (SoftwareItem.ProductName.StartsWith("XnViewMP "))
                        sShortName = "XnViewMP";

                    //5.3.2016
                    if (SoftwareItem.ProductName.StartsWith("Oracle VM VirtualBox 5."))
                        sShortName = "VirtualBox";
                    if (SoftwareItem.ProductName.StartsWith("Plane9 v2."))
                        sShortName = "Plane9";
                    if (SoftwareItem.ProductName.StartsWith("Mumble 1."))
                        sShortName = "Mumble";

                    //8.3.2016
                    if (SoftwareItem.ProductName.StartsWith("WSCC 2."))
                        sShortName = "WSCC";
                    if (SoftwareItem.ProductName.StartsWith("Seafile 5."))
                        sShortName = "Seafile";

                    //27.3.2016
                    if (SoftwareItem.ProductName.StartsWith("MKVToolNix "))
                        sShortName = "MKVToolNix";
                    if (SoftwareItem.ProductName.StartsWith("XMedia Recode version 3."))
                        sShortName = "XMedia Recode";
                    if (SoftwareItem.ProductName.StartsWith("LAV Filters "))
                        sShortName = "LAV Filters";
                    if (SoftwareItem.ProductName.StartsWith("Java(TM) 6 Update "))
                        sShortName = "JavaRuntime8";
                    if (SoftwareItem.ProductName.StartsWith("Java(TM) 6 Update ") & SoftwareItem.ProductName.EndsWith(" (64-bit)"))
                        sShortName = "JavaRuntime8x64";
                    if (SoftwareItem.ProductName.StartsWith("PeaZip "))
                        sShortName = "PeaZip";

                    //29.4.2016
                    if (SoftwareItem.ProductName.StartsWith("K-Lite Mega Codec Pack "))
                        sShortName = "K-LiteCodecPack";
                    if (SoftwareItem.ProductName.StartsWith("FormatFactory "))
                        sShortName = "FormatFactory";
                    if (SoftwareItem.ProductName.StartsWith("Free Download Manager "))
                        sShortName = "Free Download Manager";

                    //2.11.2016
                    if (SoftwareItem.ProductName.StartsWith("Pale Moon "))
                        sShortName = "Pale Moon";

                    //23.1.2017
                    if (SoftwareItem.ProductName.StartsWith("Microsoft SQL Server Management Studio - 16."))
                        sShortName = "SQL Management Studio 2016";

                    if (SoftwareItem.ProductName.StartsWith("Mozilla Firefox "))
                    {
                        if (SoftwareItem.ProductName.Contains("ESR"))
                            sShortName = "Firefox ESR";
                        else
                            sShortName = "Firefox";
                    }

                    if (SoftwareItem.ProductName.StartsWith("Notepad++ "))
                    {
                        sShortName = "Notepad++";

                        if (SoftwareItem.ProductName.Contains("x64"))
                            sShortName = "Notepad++(x64)";

                    }

                    if (SoftwareItem.ProductName.StartsWith("Puran Utilities "))
                        sShortName = "Puran Utilities";
                    if (SoftwareItem.ProductName.StartsWith("Glary Utilities "))
                        sShortName = "Glary Utilities PRO";
                    if (SoftwareItem.ProductName.StartsWith("Docker Toolbox version "))
                        sShortName = "Docker Toolbox";
                    if (SoftwareItem.ProductName.StartsWith("Cyberduck "))
                        sShortName = "Cyberduck";
                    if (SoftwareItem.ProductName.StartsWith("Clover "))
                        sShortName = "Clover";

                    //31.1.2017
                    if (SoftwareItem.ProductName.StartsWith("Logitech Gaming Software "))
                        sShortName = "Logitech Gaming Software";
                    if (SoftwareItem.ProductName.StartsWith("Waterfox "))
                        sShortName = "Waterfox";

                    if (SoftwareItem.ProductName.StartsWith("VidCoder "))
                        sShortName = "VidCoder";
                    if (SoftwareItem.ProductName.StartsWith("ImageMagick 7."))
                        sShortName = "ImageMagick";

                    //2.4.2017
                    if (SoftwareItem.ProductName.StartsWith("fxCalc version "))
                        sShortName = "fxCalc";
                    if (SoftwareItem.ProductName.StartsWith("ConEmu "))
                        sShortName = "ConEmu";
                    if (SoftwareItem.ProductName.StartsWith("AVStoDVD "))
                        sShortName = "AVStoDVD";

                    if (string.IsNullOrEmpty(sShortName))
                        oProduct = oSW.Product.Add(new Product() { ProductName = SoftwareItem.ProductName, Manufacturer = SoftwareItem.Manufacturer, ProductDescription = SoftwareItem.Description, ProjectURL = SoftwareItem.ProductURL });
                    else
                        oProduct = oSW.Product.Add(new Product() { ProductName = SoftwareItem.ProductName, Manufacturer = SoftwareItem.Manufacturer, ProductDescription = SoftwareItem.Description, ProjectURL = SoftwareItem.ProductURL, ShortName = sShortName });
                }
                else
                {
                    try
                    {
                        //if (SoftwareItem.Architecture == "X86" | SoftwareItem.Architecture == "X64" | string.IsNullOrEmpty(oProduct.ShortName))

                        bool bChange = false;
                        if (!string.IsNullOrEmpty(SoftwareItem.ProductURL))
                        {
                            oProduct.ProjectURL = SoftwareItem.ProductURL;
                            bChange = true;
                        }
                        if (!string.IsNullOrEmpty(SoftwareItem.Description))
                        {
                            oProduct.ProductDescription = SoftwareItem.Description;
                            bChange = true;
                        }

                        if (bChange)
                            oSW.SaveChanges();

                    }
                    catch { }
                }

                //Check if version already exists
                oProductVersion = oSW.ProductVersion.FirstOrDefault(t => t.Version == SoftwareItem.ProductVersion & t.ProductId == oProduct.Id);
                if (oProductVersion == null)
                {
                    oProductVersion = oSW.ProductVersion.Add(new ProductVersion() { ProductId = oProduct.Id, Version = SoftwareItem.ProductVersion, Icon = SoftwareItem.Image, LastModified = DateTime.Now });
                    oSW.SaveChanges();
                }
                else
                {
                    //Do not update published Versions
                    if (oProductVersion.IsLatest != true)
                    {
                        if (SoftwareItem.Image != null)
                            oProductVersion.Icon = SoftwareItem.Image;

                        oProductVersion.LastModified = DateTime.Now;

                        oSW.SaveChanges();
                    }
                }

                //Check if MSI ID exists
                if (!string.IsNullOrEmpty(SoftwareItem.MSIProductID))
                {
                    try
                    {
                        Guid oMSIID = Guid.Parse(SoftwareItem.MSIProductID);
                        oMSI = oSW.WindowsInstallerIDs.FirstOrDefault(t => t.ProductVersionId == oProductVersion.Id & t.MSIProductID == oMSIID);
                        if (oMSI == null)
                        {
                            oMSI = oSW.WindowsInstallerIDs.Add(new WindowsInstallerIDs() { ProductVersionId = oProductVersion.Id, MSIProductID = oMSIID, MSIVersion = SoftwareItem.ProductVersion, ARPDisplayName = SoftwareItem.ProductName, MSIProductName = SoftwareItem.ProductName });
                        }
                    }
                    catch { }
                }

                try
                {
                    if (SoftwareItem.Files.Where(t => !string.IsNullOrEmpty(t.URL)).Count() == 0)
                    {
                        SoftwareItem.Architecture = "NEW";
                    }
                }
                catch { SoftwareItem.Architecture = "NEW"; }

                //Check InstallType
                try
                {
                    if (SoftwareItem.Files.ToList().Count > 0)
                    {
                        if (SoftwareItem.Files.Where(t => !string.IsNullOrEmpty(t.URL)).Count() > 0)
                        {
                            oDT = oSW.DeploymentType.FirstOrDefault(t => t.ProductVersionId == oProductVersion.Id & t.Type == SoftwareItem.Architecture);
                            if (oDT == null)
                            {
                                Guid gContet = Guid.NewGuid();
                                try
                                {
                                    gContet = Guid.Parse(SoftwareItem.ContentID);
                                }
                                catch { }
                                oDT = oSW.DeploymentType.Add(new DeploymentType() { ProductVersionId = oProductVersion.Id, PSInstall = SoftwareItem.PSInstall, PSUninstall = SoftwareItem.PSUninstall, Type = SoftwareItem.Architecture, PSDetection = SoftwareItem.PSDetection, ContentID = gContet, PSPreReq = SoftwareItem.PSPreReq, LastModified = DateTime.Now, IsSealed = false, Author = SoftwareItem.Author, PSPreInstall = SoftwareItem.PSPreInstall, PSPostInstall = SoftwareItem.PSPostInstall });
                            }
                            else
                            {
                                if (oDT.IsSealed != true)
                                {
                                    if (oDT.Type == "NEW")
                                    {
                                        //Allow full update if Item is 'NEW'
                                        oDT.PSDetection = SoftwareItem.PSDetection;
                                        oDT.PSInstall = SoftwareItem.PSInstall;
                                        oDT.PSPreReq = SoftwareItem.PSPreReq;
                                        oDT.PSUninstall = SoftwareItem.PSUninstall;
                                        oDT.Type = SoftwareItem.Architecture;
                                        oDT.Author = SoftwareItem.Author;
                                        oDT.PSPreInstall = SoftwareItem.PSPreInstall;
                                        oDT.PSPostInstall = SoftwareItem.PSPostInstall;

                                        if (SoftwareItem.ContentID != null)
                                        {
                                            try
                                            {
                                                oDT.ContentID = Guid.Parse(SoftwareItem.ContentID);
                                            }
                                            catch { }
                                        }

                                        //oSW.SaveChanges();
                                    }
                                    else
                                    {
                                        //Only update missing Items
                                        //??? Does this make sense ?
                                        if (!string.IsNullOrEmpty(SoftwareItem.PSDetection))
                                            oDT.PSDetection = SoftwareItem.PSDetection;
                                        if (!string.IsNullOrEmpty(SoftwareItem.PSInstall))
                                            oDT.PSInstall = SoftwareItem.PSInstall;
                                        if (!string.IsNullOrEmpty(SoftwareItem.PSPreReq))
                                            oDT.PSPreReq = SoftwareItem.PSPreReq;
                                        if (!string.IsNullOrEmpty(SoftwareItem.PSUninstall))
                                            oDT.PSUninstall = SoftwareItem.PSUninstall;
                                        if (string.IsNullOrEmpty(oDT.Author) & !string.IsNullOrEmpty(SoftwareItem.Author))
                                            oDT.Author = SoftwareItem.Author;

                                        oDT.PSPreInstall = SoftwareItem.PSPreInstall;
                                        oDT.PSPostInstall = SoftwareItem.PSPostInstall;
                                    }
                                    oDT.LastModified = DateTime.Now;
                                    oSW.SaveChanges();
                                }
                            }
                        }
                    }
                    else
                    { }

                }
                catch { }

                if (oDT.IsSealed == null)
                    oDT.IsSealed = false;

                if (oDT.IsSealed == false)
                {
                    foreach (contentFiles fs in SoftwareItem.Files)
                    {
                        try
                        {
                            if (oSW.Content.Count(t => t.DownloadURL == fs.URL & t.DeploymentTypeID == oDT.Id) == 0)
                            {
                                oSW.Content.Add(new Content() { DeploymentTypeID = oDT.Id, DownloadURL = fs.URL, Filename = fs.FileName, FileHash = fs.FileHash, HashType = fs.HashType });
                            }
                            else
                            {
                                try
                                {
                                    Content oContent = oSW.Content.First(t => t.DownloadURL == fs.URL & t.DeploymentTypeID == oDT.Id);
                                    oContent.Filename = fs.FileName;
                                    oContent.FileHash = fs.FileHash;
                                    oContent.HashType = fs.HashType;

                                    //oSW.SaveChanges();
                                }
                                catch { }
                            }
                        }
                        catch { }
                    }
                }

                //only add if no prereq exists
                if (oSW.PreRequisite.FirstOrDefault(t => t.ProductId == oProduct.Id) == null && oDT.IsSealed == false)
                {
                    //PreReq
                    if (SoftwareItem.PreRequisites != null)
                    {
                        if (SoftwareItem.PreRequisites.Length > 0)
                        {
                            int i = 0;
                            foreach (string sName in SoftwareItem.PreRequisites)
                            {
                                if (!string.IsNullOrEmpty(sName))
                                {
                                    try
                                    {
                                        PreRequisite oPreReq = new PreRequisite() { ProductName = sName.Trim(), ProductId = oProduct.Id, Enabled = true, ProductVersion = "", Position = i * 10 };
                                        oSW.PreRequisite.Add(oPreReq);
                                        oSW.SaveChanges();
                                        i++;
                                    }
                                    catch { }
                                }
                            }
                        }
                    }
                }

                oSW.SaveChanges();

                if (SoftwareItem.Architecture != "NEW")
                {
                    try
                    {
                        System.Xml.Serialization.XmlSerializer x = new System.Xml.Serialization.XmlSerializer(SoftwareItem.GetType());
                        System.IO.StreamWriter file = new System.IO.StreamWriter(System.IO.Path.Combine(HttpContext.Current.Server.MapPath("Data"), "Imp_" + SoftwareItem.ProductName + SoftwareItem.ProductVersion + SoftwareItem.Architecture + DateTime.Now.ToUniversalTime().Ticks.ToString() + ".xml"));
                        x.Serialize(file, SoftwareItem);
                        file.Close();

                        PushBullet(SoftwareItem.ProductName + " " + SoftwareItem.ProductVersion + " is waiting for approval.", SoftwareItem.ProductURL);

                        try
                        {
                            BrokeredMessage bMSG = new BrokeredMessage() { Label = "RuckZuck/WCF/PendingApproval/" + SoftwareItem.ProductName + ";" + SoftwareItem.ProductVersion, TimeToLive = new TimeSpan(24, 0, 0) };
                            bMSG.Properties.Add("User", SoftwareItem.Author);
                            tcRuckZuck.SendAsync(bMSG);
                        }
                        catch { }
                    }
                    catch { }
                }

                return true;
            }
            catch
            {
                return false;
            }

        }

        public List<AddSoftware> GetSWDefinitions(string productName, string productVersion, string manufacturer)
        {
            if (!IsUserValid())
                return new List<AddSoftware>();

            List<AddSoftware> lResult = new List<AddSoftware>();
            IDatabase cache5 = Connection.GetDatabase(5);

            //productName = WebUtility.UrlDecode(productName);
            //productVersion = WebUtility.UrlDecode(productVersion);
            //manufacturer = WebUtility.UrlDecode(manufacturer);

            string sResult = cache5.StringGet(manufacturer + ";" + productName + ";" + productVersion);
            if (!string.IsNullOrEmpty(sResult))
            {
                try
                {
                    lResult = JsonConvert.DeserializeObject<List<AddSoftware>>(sResult);
                    return lResult.OrderBy(t => t.Architecture).ToList();
                }
                catch { }

            }

            //ProductVersion oProdVer = oSW.ProductVersion.First(t => t.Version == productVersion & t.Product.ProductName == productName & t.Product.Manufacturer == manufacturer);
            foreach (ProductVersion oProdVer in oSW.ProductVersion.Where(t => t.Version == productVersion & t.Product.ProductName == productName & t.Product.Manufacturer == manufacturer))
            {
                try
                {
                    foreach (DeploymentType oDT in oProdVer.DeploymentType)
                    {
                        try
                        {
                            AddSoftware SoftwareItem = new AddSoftware()
                            {
                                Architecture = oDT.Type,
                                ContentID = oDT.ContentID.ToString(),
                                Description = oProdVer.Product.ProductDescription,
                                Manufacturer = oProdVer.Product.Manufacturer,
                                ProductName = oProdVer.Product.ProductName,
                                ProductURL = oProdVer.Product.ProjectURL,
                                ProductVersion = oProdVer.Version,
                                Shortname = oProdVer.Product.ShortName,
                                Image = oProdVer.Icon,
                                PSDetection = oDT.PSDetection,
                                PSInstall = oDT.PSInstall,
                                PSPreReq = oDT.PSPreReq,
                                PSUninstall = oDT.PSUninstall,
                                PSPreInstall = oDT.PSPreInstall,
                                PSPostInstall = oDT.PSPostInstall,
                                Author = oDT.Author
                            };

                            List<string> lPreReqs = new List<string>();

                            foreach (var oPreReq in oSW.PreRequisite.Where(t => t.ProductId == oProdVer.ProductId & t.Enabled).OrderBy(t => t.Position))
                            {
                                try
                                {
                                    string sShortname = oPreReq.ProductName;
                                    if (!string.IsNullOrEmpty(oPreReq.ProductVersion))
                                    {
                                        var oLatest = oSW.vActiveLatestProducts.FirstOrDefault(t => t.ProductName == oPreReq.ProductName & t.Version == oPreReq.ProductVersion);
                                        if (oLatest != null)
                                            sShortname = oLatest.ShortName;
                                    }

                                    lPreReqs.Add(sShortname);
                                }
                                catch { }
                            }

                            SoftwareItem.PreRequisites = lPreReqs.ToArray();

                            List<contentFiles> oFile = new List<contentFiles>();
                            foreach (Content oCont in oDT.Content)
                            {
                                oFile.Add(new contentFiles() { URL = oCont.DownloadURL, FileHash = oCont.FileHash, FileName = oCont.Filename, HashType = oCont.HashType });
                            }

                            SoftwareItem.Files = oFile;

                            WindowsInstallerIDs oWin = oProdVer.WindowsInstallerIDs.FirstOrDefault();
                            if (oWin != null)
                            {
                                if (oWin.MSIProductID != null)
                                {
                                    SoftwareItem.MSIProductID = oWin.MSIProductID.ToString();
                                }
                            }

                            lResult.Add(SoftwareItem);
                        }
                        catch (Exception ex)
                        {
                            ex.Message.ToString();
                        }
                    }
                }
                catch { }
            }

            try
            {
                BrokeredMessage bMSG = new BrokeredMessage() { Label = "RuckZuck/WCF/GetSWDefinitions/" + productName, TimeToLive = new TimeSpan(4, 0, 0) };
                bMSG.Properties.Add("Count", lResult.Count);
                bMSG.Properties.Add("ProductName", productName);
                bMSG.Properties.Add("ProductVersion", productVersion);
                bMSG.Properties.Add("Manufacturer", manufacturer);
                tcRuckZuck.SendAsync(bMSG);
            }
            catch { }

            //Only cache if we have a result
            if (lResult.Count > 0)
            {
                cache5.StringSetAsync(manufacturer + ";" + productName + ";" + productVersion, JsonConvert.SerializeObject(lResult), new TimeSpan(0, 15, 0));
            }

            return lResult.OrderBy(t => t.Architecture).ToList();
        }

        public void Feedback(string productName, string productVersion, string manufacturer, string working, string userKey, string feedback)
        {
            string ipAddress = "";
            try
            {
                OperationContext context = OperationContext.Current;
                MessageProperties prop = context.IncomingMessageProperties;
                RemoteEndpointMessageProperty endpoint = prop[RemoteEndpointMessageProperty.Name] as RemoteEndpointMessageProperty;
                ipAddress = endpoint.Address.ToString();
            }
            catch { }

            Task task = new Task(() =>
            {
                try
                {
                    bool bWorking = false;
                    try
                    {
                        if (string.IsNullOrEmpty(working))
                            working = "false";

                        bool.TryParse(working, out bWorking);




                        BrokeredMessage bMSG;
                        if (bWorking)
                            bMSG = new BrokeredMessage() { Label = "RuckZuck/WCF/Feedback/success/" + productName + ";" + productVersion, TimeToLive = new TimeSpan(24, 0, 0) };
                        else
                            bMSG = new BrokeredMessage() { Label = "RuckZuck/WCF/Feedback/failure/" + productName + ";" + productVersion, TimeToLive = new TimeSpan(24, 0, 0) };


                        bMSG.Properties.Add("User", userKey);
                        bMSG.Properties.Add("feedback", feedback);
                        bMSG.Properties.Add("ProductName", productName);
                        bMSG.Properties.Add("ProductVersion", productVersion);
                        bMSG.Properties.Add("Manufacturer", manufacturer);
                        bMSG.Properties.Add("IP", ipAddress);
                        tcRuckZuck.SendAsync(bMSG);

                    }
                    catch { }

                    bool bWriteToDB = true;
                    if (productName == "RuckZuck" & productVersion.StartsWith("1.5.") & feedback == "WARNING: Product not detected after installation.")
                    {
                        //We do not save status as it's from the upgrade process
                        bWriteToDB = false;
                    }
                    
                    if (feedback.Contains("Product not detected after installation."))
                    {
                        //We do not save status as it's from the upgrade process
                        bWriteToDB = false;
                    }

                    if (productName.StartsWith("Windows Management Framework 5") & !bWorking)
                    {
                        //We do not save status as it's from the upgrade process
                        bWriteToDB = false;
                    }

                    if (feedback == "Requirements not valid.Installation will not start.")
                    {
                        //We do not save status as it's from the upgrade process
                        bWriteToDB = false;
                    }


                    if (bWriteToDB)
                    {
                        ProductVersion PV = oSW.ProductVersion.First(t => t.Version == productVersion & t.Product.ProductName == productName & t.IsLatest == true);

                        oSW.ProductVersionFeedback.Add(new ProductVersionFeedback() { CreationDateTime = DateTime.Now, ProductVersionId = PV.Id, Working = bWorking, UserKey = userKey, Feedback = feedback });
                        oSW.SaveChanges();
                    }
                    
                }
                catch { }
            });
            task.Start();
        }

        public List<AddSoftware> CheckForUpdateJ(List<AddSoftware> lSoftware)
        {
            return CheckForUpdate(lSoftware);
        }

        public List<AddSoftware> CheckForUpdate(List<AddSoftware> lSoftware)
        {
            IDatabase cache = Connection.GetDatabase(1);
            IDatabase cache2 = Connection.GetDatabase(2);
            IDatabase cache3 = Connection.GetDatabase(3);

            if (!IsUserValid())
                return new List<AddSoftware>();

            List<AddSoftware> oResult = new List<AddSoftware>();
            DateTime dStart = DateTime.Now;
            //List<vActiveLatestProducts> oActive = oSW.vActiveLatestProducts.ToList();
            
            //Check if SW Exists
            foreach (AddSoftware oCheckSW in lSoftware)
            {
                string sShortName = "";
                vActiveLatestProducts oLatest = null;
                string sLatestVersion = "";

                //Check if it's a known product
                if (cache.StringGet(oCheckSW.ProductName + ";" + oCheckSW.ProductVersion) != RedisValue.Null)
                {
                    continue;
                }

                //Check if Product is already the latest Version
                var oActiveLatest = oSW.vActiveLatestProducts.FirstOrDefault(t => t.ProductName == oCheckSW.ProductName & t.Version == oCheckSW.ProductVersion);
                if (oActiveLatest != null)
                {
                    if (!string.IsNullOrEmpty(oActiveLatest.ShortName))
                    {
                        //Cache latest ProductName and Version
                        cache.StringSetAsync(oActiveLatest.ProductName + ";" + oActiveLatest.Version, oActiveLatest.ShortName, new TimeSpan(1, 0, 0));
                        continue;
                    }
                }

                /*
                if (oActive.FirstOrDefault(t => t.ProductName == oCheckSW.ProductName & t.Version == oCheckSW.ProductVersion) != null)
                    continue;
                    */
                try
                {
                    var rShortName = cache2.StringGet(oCheckSW.Manufacturer + ";" + oCheckSW.ProductName + ";" + oCheckSW.ProductVersion);
                    if (string.IsNullOrEmpty(rShortName))
                    {
                        var oProd = oSW.vListProducts.FirstOrDefault(t => t.ProductName == oCheckSW.ProductName & t.Version == oCheckSW.ProductVersion & t.Manufacturer == oCheckSW.Manufacturer);
                        if (oProd == null)
                        {
                            //Only upload if Productname and Version do not exists...
                            oProd = oSW.vListProducts.FirstOrDefault(t => t.ProductName == oCheckSW.ProductName & t.Version == oCheckSW.ProductVersion);
                            if (oProd == null)
                            {
                                //Add SW to DB...
                                UploadSWEntry(new AddSoftware() { Manufacturer = oCheckSW.Manufacturer, ProductName = oCheckSW.ProductName, ProductVersion = oCheckSW.ProductVersion });
                            }

                            //Reload the uploaded Version (as UploadSWEntry can automatically assign Shortnames)
                            try
                            {
                                oProd = oSW.vListProducts.FirstOrDefault(t => t.ProductName == oCheckSW.ProductName & t.Version == oCheckSW.ProductVersion);
                                //Get Shortname
                                sShortName = oProd.ShortName;

                                if (!string.IsNullOrEmpty(sShortName))
                                {
                                    cache2.StringSetAsync(oCheckSW.Manufacturer + ";" + oCheckSW.ProductName + ";" + oCheckSW.ProductVersion, sShortName, new TimeSpan(4, 0, 0));
                                }

                            }
                            catch { }
                        }
                        else
                        {
                            //Get Shortname
                            sShortName = oProd.ShortName;
                            if (!string.IsNullOrEmpty(sShortName))
                            {
                                cache2.StringSetAsync(oCheckSW.Manufacturer + ";" + oCheckSW.ProductName + ";" + oCheckSW.ProductVersion, sShortName, new TimeSpan(4, 0, 0));
                            }
                        }
                    }
                    else
                    {
                        sShortName = rShortName;
                    }

                    if (!string.IsNullOrEmpty(sShortName))
                    {
                        string sLatest = cache3.StringGet(sShortName);
                        try
                        {
                            if (string.IsNullOrEmpty(sLatest))
                            {
                                oLatest = oSW.vActiveLatestProducts.FirstOrDefault(t => t.ShortName == sShortName);

                                if (oLatest != null)
                                {
                                    sLatestVersion = oLatest.Version;
                                    //cache3.StringSet(sShortName, sLatestVersion);
                                    cache3.StringSetAsync(sShortName, JsonConvert.SerializeObject(oLatest), new TimeSpan(1,0,0));
                                }
                                else
                                    continue;
                            }
                            else
                            {
                                oLatest = JsonConvert.DeserializeObject<vActiveLatestProducts>(sLatest);
                                sLatestVersion = oLatest.Version;
                            }
                        }
                        catch(Exception ex)
                        {
                            //cache.StringSet(sShortName, ex.Message);
                            //oLatest = oSW.vActiveLatestProducts.FirstOrDefault(t => t.ShortName == sShortName);
                            //sLatestVersion = oLatest.Version;
                        }

                        //Compare Versions...
                        try
                        {
                            if (Version.Parse(oCheckSW.ProductVersion) < Version.Parse(sLatestVersion))
                            {
                                //We need the full value;
                                if (oLatest == null)
                                    oLatest = oSW.vActiveLatestProducts.FirstOrDefault(t => t.ShortName == sShortName); 
                                oResult.Add(new AddSoftware() { Shortname = sShortName, ProductName = oLatest.ProductName, ProductVersion = oLatest.Version, Manufacturer = oLatest.Manufacturer, Image = oSW.ProductVersion.First(t => t.Id == oLatest.Id).Icon, Description = oLatest.ProductDescription, MSIProductID = oCheckSW.ProductVersion });
                            }
                        }
                        catch
                        {
                            //String compare if version compare is not possible
                            try
                            {
                                List<string> LVer = new List<string>() { sLatestVersion, oCheckSW.ProductVersion };
                                if (oCheckSW.ProductVersion != LVer.OrderByDescending(t => t).First())
                                {
                                    //We need the full value;
                                    if (oLatest == null)
                                        oLatest = oSW.vActiveLatestProducts.FirstOrDefault(t => t.ShortName == sShortName);
                                    oResult.Add(new AddSoftware() { Shortname = sShortName, ProductName = oLatest.ProductName, ProductVersion = oLatest.Version, Manufacturer = oLatest.Manufacturer, Image = oSW.ProductVersion.First(t => t.Id == oLatest.Id).Icon, Description = oLatest.ProductDescription, MSIProductID = oCheckSW.ProductVersion });
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch { }
            }
            TimeSpan tDuration = DateTime.Now - dStart;

            try
            {
                BrokeredMessage bMSG = new BrokeredMessage() { Label = "RuckZuck/WCF/CheckForUpdate", TimeToLive = new TimeSpan(4, 0, 0) };
                bMSG.Properties.Add("ResultCount", oResult.Count);
                bMSG.Properties.Add("InputCount", lSoftware.Count);
                bMSG.Properties.Add("Duration", tDuration.TotalMilliseconds);
                tcRuckZuck.SendAsync(bMSG);
            }
            catch { }

            return oResult;

        }

        public void TrackDownloads(string contentId)
        {
            Task task = new Task(() => 
            {
                /*if (!IsUserValid())
                    return;*/

                //Track download..
                try
                {
                    Guid gID = Guid.Parse(contentId);
                    var oDT = oSW.DeploymentType.FirstOrDefault(t => t.ContentID == gID);

                    oSW.ProductVersionDownloads.Add(new ProductVersionDownloads() { DownloadDateTime = DateTime.Now, ProductVersionId = oDT.ProductVersionId, InstallTypeId = oDT.Id });
                    oSW.SaveChanges();

                    //oMQTT.Publish("RuckZuck/API/getContentFiles/" + sMQName, System.Text.Encoding.UTF8.GetBytes(oCont.DownloadURL), 0, false);
                    BrokeredMessage bMSG;
                    bMSG = new BrokeredMessage() { Label = "RuckZuck/WCF/downloaded/" + contentId, TimeToLive = new TimeSpan(24, 0, 0) };

                    bMSG.Properties.Add("contentId", contentId);
                    bMSG.Properties.Add("ProductVersionID", oDT.ProductVersionId);

                    tcRuckZuck.SendAsync(bMSG);
                }
                catch { }
            });
            task.Start();
        }

        public void PushBullet(string Message, string Body)
        {
            try
            {
                WebRequest request = WebRequest.Create("https://api.pushbullet.com/v2/pushes");
                request.Method = "POST";
                request.Headers.Add("Authorization", "Bearer o.WP944MGzspDyIxmiT61zv7S7Lrxnnx5c");
                request.ContentType = "application/json; charset=UTF-8";
                string postData =
                    "{\"channel_tag\": \"ruckzuck\",  \"type\": \"note\", \"title\": \"" + Message + "\", \"body\": \"" + Body + "\"}";
                byte[] byteArray = Encoding.UTF8.GetBytes(postData);
                request.ContentLength = byteArray.Length;
                Stream dataStream = request.GetRequestStream();
                dataStream.Write(byteArray, 0, byteArray.Length);
                dataStream.Close();
            }
            catch { }
        }
    }
}
