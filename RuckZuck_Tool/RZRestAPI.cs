﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web.Script.Serialization;
using Microsoft.Win32;
using System.Net.Sockets;
using System.IO;
using System.Diagnostics;

namespace RuckZuck_WCF
{
    public static class RZRestAPI
    {
        private static string _sURL = "UDP";
        public static bool DisableBroadcast = false;
        public static string sURL
        {
            get
            {
                if (DisableBroadcast)
                    _sURL = "";

                string sWebSVC = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\RuckZuck", "WebService", "") as string;
                if (!string.IsNullOrEmpty(sWebSVC))
                {
                    if (sWebSVC.StartsWith("http", StringComparison.CurrentCultureIgnoreCase))
                    {
                        RZRestAPI._sURL = sWebSVC;
                    }
                }

                if (_sURL == "UDP" & !DisableBroadcast)
                {
                    try
                    {
                        using (var Client = new UdpClient())
                        {
                            Client.Client.SendTimeout = 1000;
                            Client.Client.ReceiveTimeout = 1000;
                            var RequestData = Encoding.ASCII.GetBytes(Environment.MachineName);
                            var ServerEp = new IPEndPoint(IPAddress.Any, 0);

                            Client.EnableBroadcast = true;
                            Client.Send(RequestData, RequestData.Length, new IPEndPoint(IPAddress.Broadcast, 5001));

                            var ServerResponseData = Client.Receive(ref ServerEp);
                            var ServerResponse = Encoding.ASCII.GetString(ServerResponseData);
                            Console.WriteLine("Recived {0} from {1}", ServerResponse, ServerEp.Address.ToString());
                            if (ServerResponse.StartsWith("http"))
                                _sURL = ServerResponse;
                            Client.Close();
                        }
                    }
                    catch { _sURL = ""; }
                }

                if (string.IsNullOrEmpty(_sURL))
                {
                    return "https://ruckzuck.azurewebsites.net/wcf/RZService.svc";
                }
                else
                    return _sURL;
            }
            set
            {
                _sURL = value;
            }
        }

        public static string contentType = "application/json";

        public static string Token;

        public static string GetAuthToken(string Username, string Password)
        {
            try
            {
                using (var oClient = new HttpClient())
                {

                    oClient.DefaultRequestHeaders.Add("Username", Username);
                    oClient.DefaultRequestHeaders.Add("Password", Password);
                    oClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    var response = oClient.GetStringAsync(sURL + "/rest/AuthenticateUser");
                    response.Wait(20000);
                    if (response.IsCompleted)
                    {
                        Token = response.Result.Replace("\"", "");
                        return Token;
                    }
                }
            }
            catch { }

            return "";

        }

        public static List<GetSoftware> SWGet(string Shortname)
        {
            List<GetSoftware> lResult = new List<GetSoftware>();

            try
            {
                lResult = SWResults("").Where(t => t.Shortname == Shortname).ToList();
            }
            catch { }

            if (lResult.Count() == 0)
            {
                using (var oClient = new HttpClient())
                {
                    try
                    {
                        oClient.DefaultRequestHeaders.Add("AuthenticatedToken", Token);
                        oClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(contentType));
                        var response = oClient.GetStringAsync(sURL + "/rest/SWGetShort?name=" + WebUtility.UrlEncode(Shortname));
                        response.Wait(5000);
                        if (response.IsCompleted)
                        {
                            JavaScriptSerializer ser = new JavaScriptSerializer();
                            List<GetSoftware> lRes = ser.Deserialize<List<GetSoftware>>(response.Result);
                            return lRes;
                        }
                    }
                    catch { }
                }
            }


            return lResult;
        }

        public static List<GetSoftware> SWGet(string PackageName, string PackageVersion)
        {
            /*using (var oClient = new HttpClient())
            {
                try
                {
                    oClient.DefaultRequestHeaders.Add("AuthenticatedToken", Token);
                    oClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(contentType));
                    var response = oClient.GetStringAsync(sURL + "/rest/SWGet?name=" + WebUtility.UrlEncode(PackageName) + "&ver=" + PackageVersion);
                    response.Wait(5000);
                    if (response.IsCompleted)
                    {
                        JavaScriptSerializer ser = new JavaScriptSerializer();
                        List<GetSoftware> lRes = ser.Deserialize<List<GetSoftware>>(response.Result);
                        return lRes;
                    }
                }
                catch { }
            }*/

            try
            {
                return SWResults("").Where(t => t.ProductName == PackageName & t.ProductVersion == PackageVersion).ToList();
            }
            catch { }

            return new List<GetSoftware>();
        }

        public static List<GetSoftware> SWGet(string PackageName, string Manufacturer, string PackageVersion)
        {
            /*using (var oClient = new HttpClient())
            {
                try
                {
                    oClient.DefaultRequestHeaders.Add("AuthenticatedToken", Token);
                    oClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(contentType));
                    var response = oClient.GetStringAsync(sURL + "/rest/SWGetPkg?name=" + WebUtility.UrlEncode(PackageName) + "&manuf=" + WebUtility.UrlEncode(Manufacturer) + "&ver=" + PackageVersion);
                    response.Wait(5000);
                    if (response.IsCompleted)
                    {
                        JavaScriptSerializer ser = new JavaScriptSerializer();
                        List<GetSoftware> lRes = ser.Deserialize<List<GetSoftware>>(response.Result);
                        return lRes;
                    }
                }
                catch { }
            }*/

            try
            {
                return SWResults("").Where(t => t.ProductName == PackageName & t.ProductVersion == PackageVersion & t.Manufacturer == Manufacturer).ToList();
            }
            catch { }

            return new List<GetSoftware>();
        }

        public static List<GetSoftware> SWResults(string Searchstring)
        {

            using (var oClient = new HttpClient())
            {
                try
                {
                    oClient.DefaultRequestHeaders.Add("AuthenticatedToken", Token);
                    oClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(contentType));

                    if (string.IsNullOrEmpty(Searchstring)) //FullCatalog?
                    {
                        if (File.Exists(Path.Combine(Environment.ExpandEnvironmentVariables("%TEMP%"), "rzcat.json"))) //Cached content exists
                        {
                            try
                            {
                                DateTime dCreationDate = File.GetCreationTime(Path.Combine(Environment.ExpandEnvironmentVariables("%TEMP%"), "rzcat.json"));
                                if ((DateTime.Now - dCreationDate) < new TimeSpan(0, 30, 0))
                                {
                                    //return cached Content
                                    string jRes = File.ReadAllText(Path.Combine(Environment.ExpandEnvironmentVariables("%TEMP%"), "rzcat.json"));
                                    JavaScriptSerializer ser = new JavaScriptSerializer();
                                    List<GetSoftware> lRes = ser.Deserialize<List<GetSoftware>>(jRes);
                                    return lRes;
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine(ex.Message);
                            }
                        }
                    }

                    var response = oClient.GetStringAsync(sURL + "/rest/SWResults?search=" + Searchstring);
                    response.Wait(20000);
                    if (response.IsCompleted)
                    {
                        JavaScriptSerializer ser = new JavaScriptSerializer();
                        List<GetSoftware> lRes = ser.Deserialize<List<GetSoftware>>(response.Result);

                        if (string.IsNullOrEmpty(Searchstring))
                        {
                            if (lRes.Count > 400)
                            {
                                File.WriteAllText(Path.Combine(Environment.ExpandEnvironmentVariables("%TEMP%"), "rzcat.json"), response.Result);
                            }
                        }

                        return lRes;
                    }
                }
                catch { }
            }


            return new List<GetSoftware>();
        }

        public static async Task<string> Feedback(string productName, string productVersion, string manufacturer, string working, string userKey, string feedback)
        {
            if (!string.IsNullOrEmpty(feedback))
            {

                using (var oClient = new HttpClient())
                {
                    try
                    {
                        oClient.DefaultRequestHeaders.Add("AuthenticatedToken", Token);
                        oClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(contentType));
                        var oRes = await oClient.GetStringAsync(sURL + "/rest/Feedback?name=" + WebUtility.UrlEncode(productName) + "&ver=" + WebUtility.UrlEncode(productVersion) + "&man=" + WebUtility.UrlEncode(manufacturer) + "&ok=" + working + "&user=" + WebUtility.UrlEncode(userKey) + "&text=" + WebUtility.UrlEncode(feedback));
                        return oRes;
                    }
                    catch { }
                }

            }

            return "";
        }

        public static List<AddSoftware> GetSWDefinitions(string productName, string productVersion, string manufacturer)
        {
            using (var oClient = new HttpClient())
            {
                try
                {
                    oClient.DefaultRequestHeaders.Add("AuthenticatedToken", Token);
                    oClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(contentType));
                    var response = oClient.GetStringAsync(sURL + "/rest/GetSWDefinition?name=" + WebUtility.UrlEncode(productName) + "&ver=" + WebUtility.UrlEncode(productVersion) + "&man=" + WebUtility.UrlEncode(manufacturer));
                    response.Wait(15000);
                    if (response.IsCompleted)
                    {
                        JavaScriptSerializer ser = new JavaScriptSerializer();
                        List<AddSoftware> lRes = ser.Deserialize<List<AddSoftware>>(response.Result);
                        return lRes;
                    }
                }
                catch { }
            }

            return new List<AddSoftware>();

        }

        public static List<AddSoftware> CheckForUpdate(List<AddSoftware> lSoftware)
        {

            using (var oClient = new HttpClient())
            {
                try
                {
                    JavaScriptSerializer ser = new JavaScriptSerializer();

                    oClient.DefaultRequestHeaders.Add("AuthenticatedToken", Token);
                    oClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(contentType));
                    HttpContent oCont = new StringContent(ser.Serialize(lSoftware), Encoding.UTF8, contentType);

                    if (contentType == "application/json")
                    {
                        var response = oClient.PostAsync(sURL + "/rest/CheckForUpdate", oCont);
                        response.Wait(15000);
                        if (response.IsCompleted)
                        {
                            List<AddSoftware> lRes = ser.Deserialize<List<AddSoftware>>(response.Result.Content.ReadAsStringAsync().Result);
                            return lRes;
                        }
                    }

                }
                catch { }
            }

            return new List<AddSoftware>();
        }

        public static bool UploadSWEntry(AddSoftware lSoftware)
        {
            try
            {
                var oClient = new HttpClient();
                JavaScriptSerializer ser = new JavaScriptSerializer();
                oClient.DefaultRequestHeaders.Add("AuthenticatedToken", Token);
                oClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(contentType));
                HttpContent oCont = new StringContent(ser.Serialize(lSoftware), Encoding.UTF8, contentType);

                var response = oClient.PostAsync(sURL + "/rest/UploadSWEntry", oCont);
                response.Wait(10000);

                if (response.Result.StatusCode == HttpStatusCode.OK)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch { }

            return false;
        }

        public static async void TrackDownloads(string contentID)
        {
            using (var oClient = new HttpClient())
            {
                try
                {
                    oClient.DefaultRequestHeaders.Add("AuthenticatedToken", Token);
                    oClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(contentType));
                    await oClient.GetStringAsync(sURL + "/rest/TrackDownloads/" + WebUtility.UrlEncode(contentID));
                }
                catch { }
            }
        }


        //vNext 5.9.2017
        public static async void TrackDownloads2(long SWId, string Architecture)
        {
            using (var oClient = new HttpClient())
            {
                try
                {
                    oClient.DefaultRequestHeaders.Add("AuthenticatedToken", Token);
                    oClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(contentType));
                    await oClient.GetStringAsync(sURL + "/rest/TrackDownloadsNew?SWId=" + SWId.ToString() +"&arch=" + WebUtility.UrlEncode(Architecture));
                }
                catch { }
            }
        }

        public static List<string> GetCategories(List<GetSoftware> oSWList)
        {
            List<string> lResult = new List<string>();

            foreach (GetSoftware oSW in oSWList)
            {
                lResult.AddRange((oSW.Categories ?? new List<string>()).ToArray());
            }

            return lResult.Distinct().OrderBy(t => t).ToList();
        }
    }

    public class GetSoftware
    {
        public string ProductName { get; set; }

        public string Manufacturer { get; set; }

        public string Description { get; set; }

        public string Shortname { get; set; }

        public string ProductURL { get; set; }

        public string ProductVersion { get; set; }

        public byte[] Image { get; set; }

        public Int32? Quality { get; set; }

        public Int32? Downloads { get; set; }

        public List<string> Categories { get; set; }

        public long IconId { get; set; }

        public bool isInstalled { get; set; }

        public string XMLFile { get; set; }

        public string IconFile { get; set; }

        public string IconURL
        {
            get
            {
                if (IconId > 0)
                {
                    return RZRestAPI.sURL + "/rest/GetIcon?id=" + IconId.ToString();
                }
                else
                {
                    return "File://" + IconFile;
                }
                //return "https://ruckzuck.azurewebsites.net/wcf/RZService.svc/rest/GetIcon?id=" + IconId.ToString();
            }
        }

    }

    public class AddSoftware
    {
        public string ProductName { get; set; }

        public string Manufacturer { get; set; }

        public string Description { get; set; }

        public string Shortname { get; set; }

        public string ProductURL { get; set; }

        public string ProductVersion { get; set; }

        public byte[] Image { get; set; }

        public string MSIProductID { get; set; }

        public string Architecture { get; set; }

        public string PSUninstall { get; set; }

        public string PSDetection { get; set; }

        public string PSInstall { get; set; }

        public string PSPreReq { get; set; }

        public string PSPreInstall { get; set; }

        public string PSPostInstall { get; set; }

        public string ContentID { get; set; }

        public List<contentFiles> Files { get; set; }

        public string Author { get; set; }

        public string Category { get; set; }

        public string[] PreRequisites { get; set; }

        //vNext 5.9.2017
        public long SWId { get { return IconId; } set { IconId = value; } }

        //remove if SWId is in place 5.9.2017
        public long IconId { get; set; }

        public string IconURL
        {
            get
            {
                if (SWId > 0)
                {
                    return RZRestAPI.sURL + "/rest/GetIcon?id=" + SWId.ToString();
                }
                return "";
            }
        }
    }

    public class contentFiles
    {
        public string URL { get; set; }
        public string FileName { get; set; }
        public string FileHash { get; set; }
        public string HashType { get; set; }
    }

    public class DLTask
    {
        public string ProductName { get; set; }

        public string ProductVersion { get; set; }

        public string Manufacturer { get; set; }

        public string Shortname { get; set; }

        public byte[] Image { get; set; }

        public bool AutoInstall { get; set; }

        public bool Installed { get; set; }

        public bool UnInstalled { get; set; }

        public bool Downloading { get; set; }

        public bool Installing { get; set; }

        public bool Error { get; set; }

        public bool WaitingForDependency { get; set; }

        public string ErrorMessage { get; set; }

        internal string _status = "";
        public string Status
        {
            get
            {
                if (string.IsNullOrEmpty(_status))
                {
                    if (Installing & !Error)
                        return "Installing";
                    if (Downloading & !Error)
                        return "Downloading";
                    if (Installed & !Error)
                        return "Installed";
                    if (UnInstalled & !Error)
                        return "Uninstalled";
                    if (WaitingForDependency)
                        return "Installing dependencies";
                    if (PercentDownloaded == 100 & !Error)
                        return "Downloaded";
                    if (Error)
                        return ErrorMessage;

                    return "Waiting";
                }
                else
                    return _status;
            }
            set
            {
                _status = value;
            }
        }

        public long DownloadedBytes { get; set; }

        public long TotalBytes { get; set; }

        public int PercentDownloaded { get; set; }

        public List<contentFiles> Files { get; set; }

        //public Task task { get; set; }

        public RZUpdate.SWUpdate SWUpd { get; set; }
    }

    public class DLStatus
    {
        public string Filename { get; set; }

        public string URL { get; set; }

        public int PercentDownloaded { get; set; }

        public long DownloadedBytes { get; set; }

        public long TotalBytes { get; set; }
    }
}
