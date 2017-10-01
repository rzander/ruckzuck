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
using System.Data.SqlClient;
using System.Diagnostics;
using System.Runtime.Caching;
using System.Threading;

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
            string ipAddress = "";
            try
            {
                OperationContext context = OperationContext.Current;
                MessageProperties prop = context.IncomingMessageProperties;
                RemoteEndpointMessageProperty endpoint = prop[RemoteEndpointMessageProperty.Name] as RemoteEndpointMessageProperty;
                ipAddress = endpoint.Address.ToString();
            }
            catch { }

            if (Username == "FreeRZ")
            {
                try
                {
                    /*if (ipAddress == "193.5.178.34")
                        return "";*/
                    /*if (ipAddress == "5.145.85.35")
                        return "";
                    if (ipAddress == "212.51.142.146")
                        return "";
                    if (ipAddress == "195.191.241.14")
                        return "";*/


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
                catch { }

                return "";
            }
            else
            {
                if (System.Web.Security.Membership.ValidateUser(Username, Password))
                {
                    try
                    {
                        var oMSG = new BrokeredMessage() { Label = "RuckZuck/WCF/Authentication/" + Username, TimeToLive = new TimeSpan(8, 0, 0) };
                        oMSG.Properties.Add("IP", ipAddress);
                        tcRuckZuck.SendAsync(oMSG);
                    }
                    catch { }

                    // Create and store the AuthenticatedToken before returning it
                    string token = Guid.NewGuid().ToString();

                    try
                    {
                        cache.StringSetAsync(token, Username, new TimeSpan(1, 0, 0));
                    }
                    catch { }


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
                    if (cache.StringGet(sToken) != RedisValue.Null)
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

        public List<GetSoftware> GetCatalog()
        {
            DateTime dStart = DateTime.Now;
            List<GetSoftware> oResult = new List<GetSoftware>();
            //IDatabase cache4 = Connection.GetDatabase(4);
            ObjectCache cache = MemoryCache.Default;
            string ipAddress = "";

            try
            {
                OperationContext context = OperationContext.Current;
                MessageProperties prop = context.IncomingMessageProperties;
                RemoteEndpointMessageProperty endpoint = prop[RemoteEndpointMessageProperty.Name] as RemoteEndpointMessageProperty;
                ipAddress = endpoint.Address.ToString();
            }
            catch { }

            try
            {
                //string jCatalog = cache["CATALOG"] as string;
                List<GetSoftware> oCache = cache["ALL"] as List<GetSoftware>;

                if (oCache != null)
                    return oCache;

                /*if (!string.IsNullOrEmpty(jCatalog))
                {
                    oResult = JsonConvert.DeserializeObject<List<GetSoftware>>(jCatalog);
                    return oResult;
                }*/
            }
            catch(Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }

            /*if (!IsUserValid())
                return new List<GetSoftware>();*/

            /*string sAll = cache4.StringGet("ALL");
            if (!string.IsNullOrEmpty(sAll))
            {
                try
                {
                    oResult = JsonConvert.DeserializeObject<List<GetSoftware>>(sAll);
                    return oResult;
                }
                catch { }
            }*/

            //Get full Catalog
            foreach (var oItem in oSW.v_SWVersionsLatest.Where(t => t.IsLatest == true).ToList())
            {
                try
                {
                    List<string> lCategory = new List<string>() { "Other" };
                    if (!string.IsNullOrEmpty(oItem.Category))
                    {
                        lCategory = oItem.Category.Split(';').ToList();
                    }

                    GetSoftware oRes = new GetSoftware()
                    {
                        ProductName = oItem.ProductName,
                        ProductVersion = oItem.Version,
                        Manufacturer = oItem.Manufacturer,
                        Shortname = oItem.ShortName,
                        Description = oItem.ProductDescription,
                        IconId = oItem.Id,
                        ProductURL = oItem.ProjectURL,
                        Categories = lCategory,
                        Downloads = int.Parse((oItem.Downloads ?? 0).ToString()),
                        Quality = int.Parse((100 - (((oItem.Failures ?? 0) * 100) / ((oItem.Success ?? 1) > 0 ? oItem.Success : 1))).ToString()),
                        isLatest = oItem.IsLatest ?? false,
                    };
                    oResult.Add(oRes);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }
            }

            //cache.Add("CATALOG", JsonConvert.SerializeObject(oResult), new DateTimeOffset(DateTime.Now.AddMinutes(90)));
            cache.Add("ALL", oResult, new DateTimeOffset(DateTime.Now.AddMinutes(90)));

            //cache4.StringSetAsync("ALL", JsonConvert.SerializeObject(oResult), new TimeSpan(1, 0, 0));

            try
            {
                IncomingWebRequestContext request = WebOperationContext.Current.IncomingRequest;
                WebHeaderCollection headers = request.Headers;
                BrokeredMessage bMSG = new BrokeredMessage() { Label = "RuckZuck/WCF/GetCatalog", TimeToLive = new TimeSpan(4, 0, 0) };
                bMSG.Properties.Add("Duration", (DateTime.Now - dStart).TotalMilliseconds);
                bMSG.Properties.Add("IP", ipAddress);
                bMSG.Properties.Add("User", headers["Username"] ?? "");

                tcRuckZuck.SendAsync(bMSG);
            }
            catch { }

            return oResult;
        }
        public List<GetSoftware> SWResults(string SearchPattern)
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

            List<GetSoftware> oCat = GetCatalog();

            IDatabase cache = Connection.GetDatabase();

            if (SearchPattern == "*")
                SearchPattern = "";

            if (string.IsNullOrEmpty(SearchPattern))
                return oCat;

            List<GetSoftware> oResult = new List<GetSoftware>();

            bool bTemplate = false;
            if (SearchPattern == "--OLD--")
            {
                foreach (var oOld in oSW.v_SWVersionsOLD.Where(t => t.IsLatest == true).OrderBy(t => t.LastModified).Take(30))
                {
                    try
                    {
                        GetSoftware oTemp = new GetSoftware();
                        oTemp.ProductVersion = oOld.Version;
                        oTemp.ProductName = oOld.ProductName;
                        oTemp.Manufacturer = oOld.Manufacturer;
                        oTemp.IconId = oOld.Id;
                        oTemp.Description = oOld.ProductDescription;
                        oTemp.ProductURL = oOld.ProjectURL;
                        oTemp.Shortname = oOld.ShortName;
                        oTemp.isLatest = true;
                        oTemp.Image = null;

                        if (oOld.Downloads != null)
                            oTemp.Downloads = int.Parse((oOld.Downloads).ToString());

                        if (oOld.Category != null)
                            oTemp.Categories = oOld.Category.Split(';').ToList();

                        oResult.Add(oTemp);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.Message);
                    }
                    bTemplate = true;
                }
            }

            if (SearchPattern == "--BAD--")
            {
                bTemplate = true;
            }

            if (SearchPattern == "--ISSUE--")
            {
                bTemplate = true;
            }

            if (SearchPattern == "--NEW--")
            {
                foreach (var oOld in oSW.v_SWVersions.Where(t=>t.IsLatest == true).OrderByDescending(t => t.LastModified).Take(30))
                {
                    try
                    {
                        GetSoftware oTemp = new GetSoftware();
                        oTemp.ProductVersion = oOld.Version;
                        oTemp.ProductName = oOld.ProductName;
                        oTemp.Manufacturer = oOld.Manufacturer;
                        oTemp.IconId = oOld.Id;
                        oTemp.Description = oOld.ProductDescription;
                        oTemp.ProductURL = oOld.ProjectURL;
                        oTemp.Shortname = oOld.ShortName;
                        oTemp.isLatest = true;
                        oTemp.Image = null;

                        if (oOld.Downloads != null)
                            oTemp.Downloads = int.Parse((oOld.Downloads).ToString());

                        if (oOld.Category != null)
                            oTemp.Categories = oOld.Category.Split(';').ToList();

                        oResult.Add(oTemp);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.Message);
                    }
                }
                bTemplate = true;
            }

            if (SearchPattern == "--APPROVE--")
            {
                bTemplate = true;
            }

            if (!bTemplate)
            {
                int iCount = 1;

                if (string.IsNullOrEmpty(SearchPattern))
                {
                }
                else
                {

                    if (SearchPattern.Contains("*"))
                    {
                        SearchPattern = SearchPattern.Replace("*", "");
                        oResult = oCat.Where(t => t.Shortname == SearchPattern).ToList();
                        //oFoundItems.AddRange(oSW.vAllSWDetails.Where(t => t.ShortName == SearchPattern));
                    }

                    if (oResult.Count == 0)
                    {
                        oResult = oCat.Where(t => t.Shortname.Contains(SearchPattern)).ToList();
                        //oFoundItems.AddRange(oSW.vAllSWDetails.Where(t => t.ShortName.Contains(SearchPattern)));
                    }

                    if (oResult.Count == 0)
                    {
                        if (!string.IsNullOrEmpty(SearchPattern))
                        {
                            oResult = oCat.Where(t => t.ProductName.Contains(SearchPattern)).ToList();
                            //oFoundItems.AddRange(oSW.vAllSWDetails.Where(t => t.ProductName.Contains(SearchPattern)));
                            iCount++;
                        }
                    }

                    if (oResult.Count == 0)
                    {
                        iCount = 1;
                        oResult = oCat.Where(t => t.Description.Contains(SearchPattern)).ToList();
                        //oFoundItems.AddRange(oSW.vAllSWDetails.Where(t => t.ProductDescription.Contains(SearchPattern)));
                    }
                }
            }

            try
            {
                IncomingWebRequestContext request = WebOperationContext.Current.IncomingRequest;
                WebHeaderCollection headers = request.Headers;
                BrokeredMessage bMSG = new BrokeredMessage() { Label = "RuckZuck/WCF/SWResults/" + SearchPattern, TimeToLive = new TimeSpan(4, 0, 0) };
                bMSG.Properties.Add("Count", oResult.Count);
                bMSG.Properties.Add("IP", ipAddress);
                bMSG.Properties.Add("User", headers["Username"] ?? "");

                tcRuckZuck.SendAsync(bMSG);
            }
            catch { }

            return oResult;
        }

        public List<GetSoftware> SWGetByShortname(string PkgName)
        {
            return SWGetByPkgNameAndVersion(PkgName, "");
        }

        public List<GetSoftware> SWGetByPkgNameAndVersion(string PkgName, string PkgVersion = "")
        {
            DateTime dStart = DateTime.Now;
            string ipAddress = "";
            try
            {
                OperationContext context = OperationContext.Current;
                MessageProperties prop = context.IncomingMessageProperties;
                RemoteEndpointMessageProperty endpoint = prop[RemoteEndpointMessageProperty.Name] as RemoteEndpointMessageProperty;
                ipAddress = endpoint.Address.ToString();
            }
            catch { }

            var oCat = GetCatalog();



            List<GetSoftware> lResult = new List<GetSoftware>();

            if (string.IsNullOrEmpty(PkgVersion))
            {
                lResult = oCat.Where(t => t.ProductName == PkgName).ToList();
            }
            else
            {
                lResult = oCat.Where(t => t.ProductName == PkgName & t.ProductVersion == PkgVersion).ToList();
            }

            if (lResult.Count() == 0)
            {
                lResult = oCat.Where(t => t.Shortname == PkgName & t.isLatest).ToList();
            }

            try
            {
                BrokeredMessage bMSG = new BrokeredMessage() { Label = "RuckZuck/WCF/SWGet/" + PkgName + ";" + PkgVersion, TimeToLive = new TimeSpan(4, 0, 0) };
                bMSG.Properties.Add("Duration", (DateTime.Now - dStart).TotalMilliseconds);
                bMSG.Properties.Add("IP", ipAddress);
                tcRuckZuck.SendAsync(bMSG);
            }
            catch { }

            return lResult;
            /*
            string ipAddress = "";
            try
            {
                OperationContext context = OperationContext.Current;
                MessageProperties prop = context.IncomingMessageProperties;
                RemoteEndpointMessageProperty endpoint = prop[RemoteEndpointMessageProperty.Name] as RemoteEndpointMessageProperty;
                ipAddress = endpoint.Address.ToString();
            }
            catch { }

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
                BrokeredMessage bMSG = new BrokeredMessage() { Label = "RuckZuck/WCF/SWGet/" + PkgName + ";" + PkgVersion, TimeToLive = new TimeSpan(4, 0, 0) };
                bMSG.Properties.Add("Count", oResult.Count);
                bMSG.Properties.Add("IP", ipAddress);
                tcRuckZuck.SendAsync(bMSG);
            }
            catch { }

            return oResult;*/
        }

        public List<GetSoftware> SWGetByPkg(string PkgName, string Manufacturer, string PkgVersion = "")
        {
            DateTime dStart = DateTime.Now;
            string ipAddress = "";
            try
            {
                OperationContext context = OperationContext.Current;
                MessageProperties prop = context.IncomingMessageProperties;
                RemoteEndpointMessageProperty endpoint = prop[RemoteEndpointMessageProperty.Name] as RemoteEndpointMessageProperty;
                ipAddress = endpoint.Address.ToString();
            }
            catch { }

            var oCat = GetCatalog();



            List<GetSoftware> lResult = new List<GetSoftware>();

            if (string.IsNullOrEmpty(PkgVersion))
            {
                lResult = oCat.Where(t => t.ProductName == PkgName & t.Manufacturer == Manufacturer).ToList();
            }
            else
            {
                lResult = oCat.Where(t => t.ProductName == PkgName & t.ProductVersion == PkgVersion & t.Manufacturer == Manufacturer).ToList();
            }

            if(lResult.Count() == 0)
            {
                oCat.Where(t => t.Shortname == PkgName & t.isLatest).ToList();
            }

            try
            {
                BrokeredMessage bMSG = new BrokeredMessage() { Label = "RuckZuck/WCF/SWGet/" + PkgName + ";" + PkgVersion, TimeToLive = new TimeSpan(4, 0, 0) };
                bMSG.Properties.Add("Duration", (DateTime.Now - dStart).TotalMilliseconds);
                bMSG.Properties.Add("IP", ipAddress);
                tcRuckZuck.SendAsync(bMSG);
            }
            catch { }

            return lResult;

            /*
            if (!IsUserValid())
                return new List<GetSoftware>();

            //PkgName = WebUtility.UrlDecode(PkgName);
            //PkgVersion = WebUtility.UrlDecode(PkgVersion);

            List<GetSoftware> oResult = new List<GetSoftware>();
            List<vAllSWDetails> oFoundItems = new List<vAllSWDetails>();
            oFoundItems.AddRange(oSW.vAllSWDetails.Where(t => t.ProductName == PkgName & t.Version == PkgVersion & t.Manufacturer == Manufacturer));

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
                BrokeredMessage bMSG = new BrokeredMessage() { Label = "RuckZuck/WCF/SWGet/" + PkgName + ";" + PkgVersion, TimeToLive = new TimeSpan(4, 0, 0) };
                bMSG.Properties.Add("Count", oResult.Count);
                tcRuckZuck.SendAsync(bMSG);
            }
            catch { }

            return oResult;*/
        }

        public bool UploadSWEntry(AddSoftware SoftwareItem)
        {
            /*if (!IsUserValid())
                return false;*/
            try
            {
                SWVersions oProd = null;

                try
                {
                    oProd = oSW.SWVersions.FirstOrDefault(t => t.ProductName == SoftwareItem.ProductName & t.Version == SoftwareItem.ProductVersion & t.Manufacturer == SoftwareItem.Manufacturer);
                    if (oProd == null)
                    {
                        oProd = oSW.SWVersions.Add(new SWVersions() { ProductName = SoftwareItem.ProductName, Version = SoftwareItem.ProductVersion, Manufacturer = SoftwareItem.Manufacturer, LastModified = DateTime.Now.ToUniversalTime(), ProductDescription = SoftwareItem.Description, ProjectURL = SoftwareItem.ProductURL, Category = SoftwareItem.Category, ShortName = SoftwareItem.Shortname ?? "" });
                        oSW.SaveChanges();

                        try
                        {
                            BrokeredMessage bMSG = new BrokeredMessage() { Label = "RuckZuck/WCF/UploadSWEntry/" + SoftwareItem.ProductName + ";" + SoftwareItem.ProductVersion, TimeToLive = new TimeSpan(24, 0, 0) };
                            bMSG.Properties.Add("User", SoftwareItem.Author);
                            tcRuckZuck.SendAsync(bMSG);
                        }
                        catch { }
                    }
                    else
                    {
                        if(oProd.IsLatest != true )
                        {
                            if (string.IsNullOrEmpty(oProd.ProductDescription) & !string.IsNullOrEmpty(SoftwareItem.Description))
                                oProd.ProductDescription = SoftwareItem.Description;
                            if (string.IsNullOrEmpty(oProd.ProjectURL) & !string.IsNullOrEmpty(SoftwareItem.ProductURL))
                                oProd.ProjectURL = SoftwareItem.ProductURL;
                            if(string.IsNullOrEmpty(oProd.Category) & !string.IsNullOrEmpty(SoftwareItem.Category))
                                oProd.Category = SoftwareItem.Category;
                            if (string.IsNullOrEmpty(oProd.ShortName) & !string.IsNullOrEmpty(SoftwareItem.Shortname))
                                oProd.ShortName = SoftwareItem.Shortname;
                            
                            oSW.SaveChanges();
                        }
                    }

                    SoftwareItem.IconId = oProd.Id;
                }
                catch(Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }

                if (string.IsNullOrEmpty(oProd.ShortName))
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

                    //14.4.2017
                    if (SoftwareItem.ProductName.StartsWith("Wireshark "))
                        sShortName = "Wireshark";
                    if (SoftwareItem.ProductName.StartsWith("Postman-win64-"))
                        sShortName = "Postman";
                    if (SoftwareItem.ProductName.StartsWith("Sandboxie "))
                        sShortName = "Sandboxie";

                    //8.9.2017
                    if (SoftwareItem.ProductName.StartsWith("Git Extensions "))
                        sShortName = "Git Extensions";
                    if (SoftwareItem.ProductName == "RuckZuck")
                        sShortName = "RuckZuck";
                    if (SoftwareItem.ProductName == "Google Chrome ")
                        sShortName = "Google Chrome ";
                    if (SoftwareItem.ProductName == "CDBurnerXP")
                        sShortName = "CDBurnerXP";

                    if (SoftwareItem.ProductName == "Sublime Text Build ")
                        sShortName = "SublimeText";
                    if (SoftwareItem.ProductName == "Microsoft Visio Viewer ")
                        sShortName = "Visio Viewer";
                    if (SoftwareItem.ProductName == "Adobe Flash Player 27 NPAPI")
                        sShortName = "FlashPlayerPlugin";

                    if (SoftwareItem.ProductName == "SafeInCloud Password Manager")
                        sShortName = "SafeInCloud Password Manager";
                    if (SoftwareItem.ProductName == "BDAntiRansomware")
                        sShortName = "BDAntiRansomware";
                    if (SoftwareItem.ProductName == "Arduino")
                        sShortName = "Arduino";
                    if (SoftwareItem.ProductName == "mRemoteNG")
                        sShortName = "mRemoteNG";
                    if (SoftwareItem.ProductName == "Driver Booster ")
                        sShortName = "Driver Booster";

                    if (!string.IsNullOrEmpty(sShortName))
                    {
                        SoftwareItem.Shortname = sShortName;

                        if (oProd != null)
                            oProd.ShortName = sShortName;

                    }
                }
                else
                {
                }

                try
                {
                    if (SoftwareItem.Files.Where(t => !string.IsNullOrEmpty(t.URL)).Count() == 0)
                    {
                        SoftwareItem.Architecture = "NEW";
                    }
                }
                catch { SoftwareItem.Architecture = "NEW"; }


                //oSW.SaveChanges();

                if (SoftwareItem.Architecture != "NEW")
                {
                    try
                    {
                        string jItem = JsonConvert.SerializeObject(SoftwareItem);
                        File.WriteAllText(System.IO.Path.Combine(HttpContext.Current.Server.MapPath("Data"), "Imp_" + SoftwareItem.ProductName + SoftwareItem.ProductVersion + SoftwareItem.Architecture.Trim() + DateTime.Now.ToUniversalTime().Ticks.ToString() + ".json"), jItem);

                        if (oProd.IsLatest != true) //Do not overwrite Latest Versions
                        {
                            if (SoftwareItem.IconId > 0)
                            {
                                try
                                {
                                    SWDetails oDetail = oSW.SWDetails.FirstOrDefault(t => t.SWId == oProd.Id & t.Architecture == SoftwareItem.Architecture.Trim());
                                    if (oDetail == null)
                                    {
                                        oSW.SWDetails.Add(new SWDetails() { SWId = SoftwareItem.IconId, ShortName = SoftwareItem.Shortname ?? "", Architecture = SoftwareItem.Architecture.Trim(), CreationDate = DateTime.Now.ToUniversalTime(), Definition = jItem, Downloads = 0, Failures = 0, Success = 0 });
                                        oSW.SaveChanges();
                                    }
                                    else
                                    {
                                        oDetail.ShortName = SoftwareItem.Shortname;
                                        //oDetail.Architecture = SoftwareItem.Architecture;
                                        oDetail.Definition = jItem;
                                        oSW.SaveChanges();
                                    }

                                }
                                catch { }
                            }

                            PushBullet(SoftwareItem.ProductName + " " + SoftwareItem.ProductVersion + " is waiting for approval.", SoftwareItem.ProductURL);

                            if (SoftwareItem.Author != "rzander")
                            {
                                oSW.SWPending.Add(new SWPending() { Architecture = SoftwareItem.Architecture, ProductName = SoftwareItem.ProductName, Username = SoftwareItem.Author, SWId = SoftwareItem.IconId });
                                oSW.SaveChangesAsync();
                            }
                            else
                            {
                                ApproveSW(SoftwareItem.IconId, SoftwareItem.Architecture);
                            }
                        }

                        try
                        {
                            var oUsr = System.Web.Security.Membership.GetUser(SoftwareItem.Author.Trim());
                            
                            BrokeredMessage bMSG = new BrokeredMessage() { Label = "RuckZuck/WCF/PendingApproval/" + SoftwareItem.ProductName + ";" + SoftwareItem.ProductVersion, TimeToLive = new TimeSpan(24, 0, 0) };
                            bMSG.Properties.Add("User", SoftwareItem.Author);

                            if(oUsr != null)
                                bMSG.Properties.Add("Mail", oUsr.Email.Trim() ?? "");

                            bMSG.Properties.Add("SWId", SoftwareItem.IconId.ToString());
                            bMSG.Properties.Add("Shortname", SoftwareItem.Shortname ?? "");
                            bMSG.Properties.Add("Version", SoftwareItem.ProductVersion ?? "");
                            bMSG.Properties.Add("Architecture", SoftwareItem.Architecture ?? "");
                            bMSG.Properties.Add("Body", jItem ?? "");
                            tcRuckZuck.SendAsync(bMSG);
                        }
                        catch { }
                    }
                    catch { }
                }

                return true;
            }
            catch(Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return false;
            }

        }

        public List<AddSoftware> GetSWDefinitions(string productName, string productVersion, string manufacturer)
        {
            if (!IsUserValid())
                return new List<AddSoftware>();

            List<AddSoftware> lResult = new List<AddSoftware>();
            IDatabase cache5 = Connection.GetDatabase(5);

            string sResult = cache5.StringGet(manufacturer + ";" + productName + ";" + productVersion);
            if (!string.IsNullOrEmpty(sResult))
            {
                try
                {
                    lResult = JsonConvert.DeserializeObject<List<AddSoftware>>(sResult);

                    //Send Status
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

                    return lResult.OrderBy(t => t.Architecture).ToList();
                }
                catch { }

            }

            var oSWVer =  oSW.v_SWVersions.FirstOrDefault(t => t.ProductName == productName & t.Version == productVersion & t.Manufacturer == manufacturer);
            if(oSWVer != null)
            {
                lResult = GetSWDetails(oSWVer.Id);
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
            Feedback(productName, productVersion, manufacturer, "", working, userKey, feedback);
        }
        public void Feedback(string productName, string productVersion, string manufacturer, string Architecture, string working, string userKey, string feedback)
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

            /*if (ipAddress == "193.5.178.34") //Skip because of failure loops from this address
                return;*/

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

                    if (feedback == "ok")
                        bWriteToDB = false;
                    if (feedback == "ok..")
                        bWriteToDB = false;


                    if (bWriteToDB)
                    {
                        var oCat = GetCatalog();

                        var oProd = oCat.FirstOrDefault(t => t.ProductName == productName & t.ProductVersion == productVersion & t.Manufacturer == manufacturer);
                        if (oProd != null)
                        {
                            string sArch = Architecture;
                            if (string.IsNullOrEmpty(sArch))
                            {
                                var oDet = GetSWDetail(oProd.IconId); //Get first Architecture
                                if (oDet != null)
                                {
                                    sArch = oDet.Architecture;
                                }
                            }

                            if (bWorking)
                                FeedbackSuccess(oProd.IconId, sArch);
                            else
                                FeedbackFailure(oProd.IconId, sArch);

                            oSW.SWFeedback.Add(new SWFeedback() { SWId = oProd.IconId, ReceivedDate = DateTime.Now.ToUniversalTime(), Success = bWorking, Feedback = feedback, Username = userKey });
                            oSW.SaveChanges();
                        }


                    }

                }
                catch { }
            });
            task.Start();
        }

        public List<AddSoftware> CheckForUpdateJ(List<AddSoftware> lSoftware)
        {
            return CheckForUpdate2(lSoftware);
        }

        public List<AddSoftware> CheckForUpdate(List<AddSoftware> lSoftware)
        {
            return CheckForUpdate2(lSoftware);
        }

        public List<AddSoftware> CheckForUpdate2(List<AddSoftware> lSoftware)
        {
            DateTime tStart = DateTime.Now;

            IDatabase cache6 = Connection.GetDatabase(6); // Latest Versions
            IDatabase cache7 = Connection.GetDatabase(7); // NoUpdates

            

            List<AddSoftware> lResult = new List<AddSoftware>();

            foreach (AddSoftware oCheckSW in lSoftware)
            {
                //fix
                if (oCheckSW.ProductName == "Client Center for Configuration Manager")
                    oCheckSW.Manufacturer = "Zander Tools";
                if (oCheckSW.ProductName == "Client Center for Configuration Manager 2012")
                    oCheckSW.Manufacturer = "Zander Tools";

                if (!string.IsNullOrEmpty(oCheckSW.Manufacturer))
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(cache7.StringGet(oCheckSW.ProductName + oCheckSW.ProductVersion + oCheckSW.Manufacturer))) //is SW marked for no Updates in Redis?
                        {
                            continue; //Skip
                        }

                        var oRes = oSW.v_SWVersions.FirstOrDefault(t => t.ProductName == oCheckSW.ProductName & t.Version == oCheckSW.ProductVersion & t.Manufacturer == oCheckSW.Manufacturer); //Check in DB
                        if (oRes != null)
                        {
                            if (oRes.IsLatest == true) //Is it the Latest?
                            {
                                cache7.StringSetAsync(oCheckSW.ProductName + oCheckSW.ProductVersion + oCheckSW.Manufacturer, "noUpdate", new TimeSpan(0, 30, 0));
                                continue; //It's alreday the latest
                            }

                            string sShortname = oRes.ShortName;


                            if (string.IsNullOrEmpty(sShortname)) //is it a known Product ?
                            {
                                cache7.StringSetAsync(oCheckSW.ProductName + oCheckSW.ProductVersion + oCheckSW.Manufacturer, "noUpdate", new TimeSpan(0, 30, 0));
                                continue; //No -> skip
                            }

                            //Check if it's a known product in Redis
                            string jLatest = cache6.StringGet(sShortname);


                            v_SWVersions oLatest;

                            if (!string.IsNullOrEmpty(jLatest)) //SW was found in Redis
                            {
                                oLatest = JsonConvert.DeserializeObject<v_SWVersions>(jLatest, new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore });
                            }
                            else
                            {
                                oLatest = oSW.v_SWVersions.FirstOrDefault(t => t.IsLatest == true & t.ShortName == sShortname);
                                if (oLatest != null)
                                {
                                    cache6.StringSetAsync(sShortname, JsonConvert.SerializeObject(oLatest), new TimeSpan(1, 0, 0));
                                }
                            }

                            if (oLatest == null) //no latest Version available?
                            {
                                cache7.StringSetAsync(oCheckSW.ProductName + oCheckSW.ProductVersion + oCheckSW.Manufacturer, "noUpdate", new TimeSpan(0, 30, 0));
                                continue; //no latest version -> skip
                            }

                            if (oLatest.Version == oCheckSW.ProductVersion) //There are cases where ProductNames are different but Version is latest.
                            {
                                cache7.StringSetAsync(oCheckSW.ProductName + oCheckSW.ProductVersion + oCheckSW.Manufacturer, "noUpdate", new TimeSpan(0, 30, 0));
                                continue; //no latest version -> skip
                            }

                            try
                            {
                                if (Version.Parse(oLatest.Version) <= Version.Parse(oCheckSW.ProductVersion)) //Try to compare Version
                                {
                                    if (oRes.ProductName != "SCCM Client Center")
                                    {
                                        //Installed Version is newer...
                                        cache7.StringSetAsync(oCheckSW.ProductName + oCheckSW.ProductVersion + oCheckSW.Manufacturer, "noUpdate", new TimeSpan(0, 30, 0));

                                        try
                                        {
                                            BrokeredMessage bMSG = new BrokeredMessage() { Label = "RuckZuck/WCF/NEW/" + oCheckSW.ProductName + "/" + oCheckSW.ProductVersion, TimeToLive = new TimeSpan(4, 0, 0) };
                                            bMSG.Properties.Add("ProductName", oCheckSW.ProductName);
                                            bMSG.Properties.Add("NewVersion", oCheckSW.ProductVersion);
                                            tcRuckZuck.SendAsync(bMSG);
                                        }
                                        catch { }

                                        continue; //no latest version -> skip
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                ex.Message.ToString();
                            }

                            AddSoftware oReturn = new AddSoftware()
                            {
                                ProductName = oLatest.ProductName,
                                ProductVersion = oLatest.Version,
                                Manufacturer = oLatest.Manufacturer,
                                Shortname = oLatest.ShortName,
                                Description = oLatest.ProductDescription,
                                Image = convertStream(GetIcon(oLatest.Id.ToString())), //can be removed after 16.0.0.2
                                IconId = oLatest.Id,
                                MSIProductID = oCheckSW.ProductVersion
                            };

                            lResult.Add(oReturn);
                        }
                        else //add SW to the DB
                        {
                            //Upload SW

                            UploadSWEntry(new AddSoftware() { Manufacturer = oCheckSW.Manufacturer, ProductName = oCheckSW.ProductName, ProductVersion = oCheckSW.ProductVersion });

                            cache7.StringSetAsync(oCheckSW.ProductName + oCheckSW.ProductVersion + oCheckSW.Manufacturer, "noUpdate", new TimeSpan(0, 30, 0));
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.Message);
                    }
                }
                else
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(cache7.StringGet(oCheckSW.ProductName + oCheckSW.ProductVersion))) //is SW marked for no Updates in Redis?
                        {
                            continue; //Skip
                        }

                        var oRes = oSW.v_SWVersions.FirstOrDefault(t => t.ProductName == oCheckSW.ProductName & t.Version == oCheckSW.ProductVersion); //Check in DB
                        if (oRes != null)
                        {
                            if (oRes.IsLatest == true) //Is it the Latest?
                            {
                                cache7.StringSetAsync(oCheckSW.ProductName + oCheckSW.ProductVersion, "noUpdate", new TimeSpan(0, 30, 0));
                                continue; //It's alreday the latest
                            }

                            string sShortname = oRes.ShortName;


                            if (string.IsNullOrEmpty(sShortname)) //is it a known Product ?
                            {
                                cache7.StringSetAsync(oCheckSW.ProductName + oCheckSW.ProductVersion, "noUpdate", new TimeSpan(0, 30, 0));
                                continue; //No -> skip
                            }

                            //Check if it's a known product in Redis
                            string jLatest = cache6.StringGet(sShortname);


                            v_SWVersions oLatest;

                            if (!string.IsNullOrEmpty(jLatest)) //SW was found in Redis
                            {
                                oLatest = JsonConvert.DeserializeObject<v_SWVersions>(jLatest, new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore });
                            }
                            else
                            {
                                oLatest = oSW.v_SWVersions.FirstOrDefault(t => t.IsLatest == true & t.ShortName == sShortname);
                                if (oLatest != null)
                                {
                                    cache6.StringSetAsync(sShortname, JsonConvert.SerializeObject(oLatest), new TimeSpan(1, 0, 0));
                                }
                            }

                            if (oLatest == null) //no latest Version available?
                            {
                                cache7.StringSetAsync(oCheckSW.ProductName + oCheckSW.ProductVersion, "noUpdate", new TimeSpan(0, 30, 0));
                                continue; //no latest version -> skip
                            }

                            if (oLatest.Version == oCheckSW.ProductVersion) //There are cases where ProductNames are different but Version is latest.
                            {
                                cache7.StringSetAsync(oCheckSW.ProductName + oCheckSW.ProductVersion, "noUpdate", new TimeSpan(0, 30, 0));
                                continue; //no latest version -> skip
                            }

                            try
                            {
                                if (Version.Parse(oLatest.Version) <= Version.Parse(oCheckSW.ProductVersion)) //Try to compare Version
                                {
                                    if (oRes.ProductName != "SCCM Client Center")
                                    {
                                        //Installed Version is newer...
                                        cache7.StringSetAsync(oCheckSW.ProductName + oCheckSW.ProductVersion + oCheckSW.Manufacturer, "noUpdate", new TimeSpan(0, 30, 0));

                                        try
                                        {
                                            BrokeredMessage bMSG = new BrokeredMessage() { Label = "RuckZuck/WCF/NEW/" + oCheckSW.ProductName + "/" + oCheckSW.ProductVersion, TimeToLive = new TimeSpan(4, 0, 0) };
                                            bMSG.Properties.Add("ProductName", oCheckSW.ProductName);
                                            bMSG.Properties.Add("NewVersion", oCheckSW.ProductVersion);
                                            tcRuckZuck.SendAsync(bMSG);
                                        }
                                        catch { }

                                        continue; //no latest version -> skip
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                ex.Message.ToString();
                            }

                            AddSoftware oReturn = new AddSoftware()
                            {
                                ProductName = oLatest.ProductName,
                                ProductVersion = oLatest.Version,
                                Manufacturer = oLatest.Manufacturer,
                                Shortname = oLatest.ShortName,
                                Description = oLatest.ProductDescription,
                                Image = convertStream(GetIcon(oLatest.Id.ToString())), //can be removed after 16.0.0.2
                                IconId = oLatest.Id,
                                MSIProductID = oCheckSW.ProductVersion
                            };

                            lResult.Add(oReturn);
                        }
                        else //add SW to the DB
                        {
                            //Upload SW

                            UploadSWEntry(new AddSoftware() { Manufacturer = oCheckSW.Manufacturer, ProductName = oCheckSW.ProductName, ProductVersion = oCheckSW.ProductVersion });

                            cache7.StringSetAsync(oCheckSW.ProductName + oCheckSW.ProductVersion + oCheckSW.Manufacturer, "noUpdate", new TimeSpan(0, 30, 0));
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.Message);
                    }
                }
            }

            TimeSpan tDuration = DateTime.Now - tStart;
            return lResult;
        }

        public byte[] convertStream(Stream input)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                input.CopyTo(ms);
                return ms.ToArray();
            }
        }

        public void TrackDownloads(string contentId)
        {
            //depreciated
        }

        public void TrackDownloads2(string sSWId, string Architecture)
        {
            Task task = new Task(() =>
            {
                //Track download..
                try
                {
                    long SWId = long.Parse(sSWId);
                    var oSWDetail = oSW.SWDetails.FirstOrDefault(t => t.SWId == SWId & t.Architecture == Architecture);
                    oSWDetail.Downloads++;
                    oSW.SaveChangesAsync();

                    BrokeredMessage bMSG;
                    bMSG = new BrokeredMessage() { Label = "RuckZuck/WCF/downloaded/" + sSWId, TimeToLive = new TimeSpan(24, 0, 0) };

                    bMSG.Properties.Add("SWId", sSWId);
                    bMSG.Properties.Add("Architecture", Architecture);

                    tcRuckZuck.SendAsync(bMSG);
                }
                catch { }
            });
            task.Start();
        }

        public void FeedbackSuccess(long SWId, string Architecture)
        {

            //Track download..
            try
            {
                var oSWDetail = oSW.SWDetails.FirstOrDefault(t => t.SWId == SWId & t.Architecture == Architecture);
                oSWDetail.Success++;
                oSW.SaveChanges();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }


        public void FeedbackFailure(long SWId, string Architecture)
        {

            //Track download..
            try
            {
                var oSWDetail = oSW.SWDetails.FirstOrDefault(t => t.SWId == SWId & t.Architecture == Architecture);
                oSWDetail.Failures++;
                oSW.SaveChanges();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }

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

        public Stream GetIcon(string iconid)
        {
            ObjectCache cache = MemoryCache.Default;

            try
            {
                byte[] oCache = cache["ICO" + iconid] as byte[];

                if (oCache != null)
                {
                    return new MemoryStream(oCache);
                }

                try
                {
                    if (File.Exists(HttpContext.Current.Server.MapPath("~") + @".\Data\Icons\" + iconid.ToString() + ".jpg"))
                    {
                        FileStream oRes = File.Open(HttpContext.Current.Server.MapPath("~") + @"\Data\Icons\" + iconid.ToString() + ".jpg", FileMode.Open, FileAccess.Read, FileShare.Read);

                        /*using (MemoryStream ms = new MemoryStream())
                        {
                            oRes.CopyTo(ms);
                            cache.Add("ICO" + iconid, ms.ToArray(), new DateTimeOffset(DateTime.Now.AddMinutes(5)));
                        }*/

                        return oRes;
                    }
                }
                catch(Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }

                int id = Convert.ToInt32(iconid);

                var oSW = GetSWDetail(id);
                if (oSW.Image != null)
                {
                    byte[] image = oSW.Image;

                    MemoryStream ms = new MemoryStream(image);
                    try
                    {
                        using (var sIcon = new System.IO.FileStream(HttpContext.Current.Server.MapPath("~") + @"\Data\Icons\" + iconid.ToString() + ".jpg", FileMode.Create))
                        {
                            ms.CopyTo(sIcon);
                            sIcon.Flush();
                            sIcon.Close();
                        }
                    }
                    catch { }
                    ms.Position = 0;
                    cache.Add("ICO" + iconid, ms.ToArray(), new DateTimeOffset(DateTime.Now.AddMinutes(5)));
                    ms.Position = 0;
                    return ms;
                }


            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }

            return null;
        }

        public AddSoftware GetSWDetail(long SWId)
        {
            var oDet = oSW.SWDetails.FirstOrDefault(t => t.SWId == SWId);
            if (oDet != null)
            {
                JsonSerializerSettings oJSet = new JsonSerializerSettings();
                oJSet.NullValueHandling = NullValueHandling.Ignore;

                string jSW = oDet.Definition;
                if (!string.IsNullOrEmpty(jSW))
                {
                    var oItem = JsonConvert.DeserializeObject<AddSoftware>(jSW, oJSet);
                    if (oItem.PreRequisites == null)
                        oItem.PreRequisites = new string[0];
                    return oItem;
                }
            }

            return new AddSoftware();
        }

        public List<AddSoftware> GetSWDetails(long SWId)
        {
            List<AddSoftware> lResult = new List<AddSoftware>();
            var oDets = oSW.SWDetails.Where(t => t.SWId == SWId);

            JsonSerializerSettings oJSet = new JsonSerializerSettings();
            oJSet.NullValueHandling = NullValueHandling.Ignore;

            foreach (var oDet in oDets)
            {
                try
                {
                    string jSW = oDet.Definition;
                    if (!string.IsNullOrEmpty(jSW))
                    {
                        var oItem = JsonConvert.DeserializeObject<AddSoftware>(jSW, oJSet);
                        if (oItem.PreRequisites == null)
                            oItem.PreRequisites = new string[0];
                        lResult.Add(oItem);
                    }
                }
                catch(Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }
            }

            return lResult;
        }

        public void SyncSW(string s1)
        {
            foreach(var oLatest in oSW.v_SWVersionsLatest.ToList())
            {
               foreach(var oDet in GetSWDetails(oLatest.Id))
               {
                    if (oDet.IconId != oLatest.Id)
                    {
                        oDet.IconId = oLatest.Id;
                        oDet.Category = oLatest.Category;

                        

                        var oDBItem = oSW.SWDetails.FirstOrDefault(t => t.SWId == oLatest.Id & t.Architecture == oDet.Architecture);
                        if(oDBItem != null)
                        {
                            string jItem = JsonConvert.SerializeObject(oDet);
                            oDBItem.Definition = jItem;
                            oSW.SaveChanges();
                        }
                        
                    }
               }

            }

        }

        public string UpdateCatalog()
        {
            try
            {
                ObjectCache cache = MemoryCache.Default;
                cache.Remove("ALL");
                cache.Remove("CATALOG");
                GetCatalog();

                return "Done.";
            }
            catch { }

            return "Failure..";
        }

        public bool ApproveSW(long SWId, string Architecture)
        {
            try
            {
                IncomingWebRequestContext request = WebOperationContext.Current.IncomingRequest;
                WebHeaderCollection headers = request.Headers;
                IDatabase cache = Connection.GetDatabase();

                string sToken = headers["AuthenticatedToken"] ?? "";
                string sUsername = headers["Username"] ?? "rzander";
                string sPassword = headers["Password"] ?? "kerb7eros";

                if (System.Web.Security.Membership.ValidateUser(sUsername, sPassword))
                {
                    if (System.Web.Security.Roles.IsUserInRole(sUsername, "Admin"))
                    {
                        var oNewVersion = oSW.SWVersions.FirstOrDefault(t => t.Id == SWId);
                        if (oNewVersion != null)
                        {
                            oNewVersion.IsLatest = true;
                            oSW.SaveChanges();
                        }

                        var oOldVersion = oSW.SWVersions.FirstOrDefault(t => t.ShortName == oNewVersion.ShortName & t.IsLatest == true);
                        if(oOldVersion != null)
                        {
                            if(oNewVersion != null)
                            {
                                if (oNewVersion.Id != oOldVersion.Id) //in case of a resubmit
                                {
                                    oOldVersion.IsLatest = false;
                                    oSW.SaveChanges();
                                }

                                UpdateCatalog();

                                oSW.SWPending.Remove(oSW.SWPending.FirstOrDefault(t => t.SWId == SWId));
                                oSW.SaveChangesAsync();
                                return true;
                            }
                        }
                    }
                }
            }
            catch { }

            return false;

        }
    }
}
