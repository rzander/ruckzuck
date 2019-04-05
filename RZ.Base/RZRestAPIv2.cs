using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace RuckZuck.Base
{
    class RZRestAPIv2
    {
        private static string _sURL = "UDP";
        public static bool DisableBroadcast = false;
        public static string CustomerID = "";

        private static HttpClient oClient = new HttpClient(); //thx https://aspnetmonsters.com/2016/08/2016-08-27-httpclientwrong/

        public static string sURL
        {
            get
            {
                if (_sURL != "UDP" && ! string.IsNullOrEmpty(_sURL))
                    return _sURL;

                try
                {
                    var vBroadcast = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\RuckZuck", "Broadcast", 1);
                    if ((int)vBroadcast == 0) //only disable if set to 0
                        DisableBroadcast = true;
                }
                catch { }


                if (DisableBroadcast)
                    _sURL = "";

                try
                {
                    string sCustID = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\RuckZuck", "CustomerID", "") as string;
                    if (!string.IsNullOrEmpty(sCustID))
                    {
                        CustomerID = sCustID; //Override CustomerID
                    }
                }
                catch { }

                try
                {
                    string sWebSVC = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\RuckZuck", "WebService", "") as string;
                    if (!string.IsNullOrEmpty(sWebSVC))
                    {
                        if (sWebSVC.StartsWith("http", StringComparison.CurrentCultureIgnoreCase))
                        {
                            RZRestAPIv2._sURL = sWebSVC.TrimEnd('/');
                        }
                    }
                }
                catch { }

                if (_sURL == "UDP" && !DisableBroadcast)
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
                    return GetURL(CustomerID);
                }
                else
                    return _sURL;
            }
            set
            {
                _sURL = value;
            }
        }

        public static string GetURL(string customerid)
        {
            using (HttpClient hClient = new HttpClient())
            {
                Task<string> tReq;
                if (string.IsNullOrEmpty(customerid))
                    tReq = hClient.GetStringAsync("https://ruckzuck.tools/rest/v2/geturl");
                else
                    tReq = hClient.GetStringAsync("https://ruckzuck.tools/rest/v2/geturl?customerid=" + customerid);

                tReq.Wait(5000); //wait max 5s

                if (tReq.IsCompleted)
                {
                    _sURL = tReq.Result;
                    return _sURL;
                }
                else
                {
                    _sURL = "https://ruckzuck.azurewebsites.net";
                    return _sURL;
                }
            }
        }

        public static List<GetSoftware> GetCatalog(string customerid = "")
        {
            if (string.IsNullOrEmpty(customerid))
            {
                if (File.Exists(Path.Combine(Environment.ExpandEnvironmentVariables("%TEMP%"), "rzcat.json"))) //Cached content exists
                {
                    try
                    {
                        DateTime dCreationDate = File.GetLastWriteTime(Path.Combine(Environment.ExpandEnvironmentVariables("%TEMP%"), "rzcat.json"));
                        if ((DateTime.Now - dCreationDate) < new TimeSpan(0, 30, 0)) //Cache for 30min
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
                        Debug.WriteLine("E1" + ex.Message, "GetCatalog");
                    }
                }
            }

            try
            {
                Task<string> response;
                if (string.IsNullOrEmpty(customerid))
                    response = oClient.GetStringAsync(sURL + "/rest/v2/GetCatalog");
                else
                    response = oClient.GetStringAsync(sURL + "/rest/v2/GetCatalog?customerid=" + customerid);

                response.Wait(60000); //60s max

                if (response.IsCompleted)
                {
                    JavaScriptSerializer ser = new JavaScriptSerializer();
                    List<GetSoftware> lRes = ser.Deserialize<List<GetSoftware>>(response.Result);


                    if (string.IsNullOrEmpty(customerid) && lRes.Count > 400)
                    {
                        File.WriteAllText(Path.Combine(Environment.ExpandEnvironmentVariables("%TEMP%"), "rzcat.json"), response.Result);
                    }

                    return lRes;
                }
            }
            catch(Exception ex)
            {
                Debug.WriteLine("E2" + ex.Message, "GetCatalog");
                _sURL = ""; //enforce reload endpoint URL
            }

            return new List<GetSoftware>();
        }

        public static List<AddSoftware> GetSoftwares(string productName, string productVersion, string manufacturer, string customerid = "")
        {

            try
            {
                Task<string> response;

                if (string.IsNullOrEmpty(customerid))
                    response = oClient.GetStringAsync(sURL + "/rest/v2/GetSoftwares?name=" + WebUtility.UrlEncode(productName) + "&ver=" + WebUtility.UrlEncode(productVersion) + "&man=" + WebUtility.UrlEncode(manufacturer));
                else
                    response = oClient.GetStringAsync(sURL + "/rest/v2/GetSoftwares?name=" + WebUtility.UrlEncode(productName) + "&ver=" + WebUtility.UrlEncode(productVersion) + "&man=" + WebUtility.UrlEncode(manufacturer) + "&customerid=" + WebUtility.UrlEncode(customerid));

                response.Wait(20000);
                if (response.IsCompleted)
                {
                    JavaScriptSerializer ser = new JavaScriptSerializer();
                    List<AddSoftware> lRes = ser.Deserialize<List<AddSoftware>>(response.Result);
                    return lRes;
                }

            }
            catch(Exception ex)
            {
                Debug.WriteLine("E1" + ex.Message, "GetSoftwares");
                _sURL = ""; //enforce reload endpoint URL
            }

            return new List<AddSoftware>();

        }

        public static byte[] GetIcon(string iconhash, string customerid = "")
        {
            Task<Stream> response;

            response = oClient.GetStreamAsync(sURL + "/rest/v2/GetIcon?iconhash=" + iconhash);

            //if (string.IsNullOrEmpty(iconhash))
            //    response = oClient.GetStreamAsync(sURL + "rest/v2/GetIcon?iconid=" + iconid);
            //else
            //    response = oClient.GetStreamAsync(sURL + "rest/v2/GetIcon?iconhash=" + iconhash);

            response.Wait(10000);

            if (response.IsCompleted)
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    response.Result.CopyTo(ms);
                    byte[] bRes = ms.ToArray();
                    return bRes;
                }
            }

            return null;
        }

        public static async void IncCounter(string shortname = "", string counter = "DL", string customerid = "")
        {
            try
            {
               await oClient.GetStringAsync(sURL + "/rest/v2/IncCounter?shortname=" + WebUtility.UrlEncode(shortname));
            }
            catch { }
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

        public static async Task<string> Feedback(string productName, string productVersion, string manufacturer, string working, string userKey, string feedback, string customerid = "")
        {
            if (!string.IsNullOrEmpty(feedback))
            {
                try
                {
                    var oRes = await oClient.GetStringAsync(sURL + "/rest/v2/feedback?name=" + WebUtility.UrlEncode(productName) + "&ver=" + WebUtility.UrlEncode(productVersion) + "&man=" + WebUtility.UrlEncode(manufacturer) + "&ok=" + working + "&user=" + WebUtility.UrlEncode(userKey) + "&text=" + WebUtility.UrlEncode(feedback));
                    return oRes;
                }
                catch { }
            }

            return "";
        }

        public static bool UploadSWEntry(AddSoftware lSoftware, string customerid = "")
        {
            try
            {
                JavaScriptSerializer ser = new JavaScriptSerializer();
                HttpContent oCont = new StringContent(ser.Serialize(lSoftware), Encoding.UTF8, "application/json");

                var response = oClient.PostAsync(sURL + "/rest/v2/uploadswentry", oCont);
                response.Wait(30000); //30s max

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

        public static List<AddSoftware> CheckForUpdate(List<AddSoftware> lSoftware, string customerid = "")
        {
            try
            {
                JavaScriptSerializer ser = new JavaScriptSerializer();
                HttpContent oCont = new StringContent(ser.Serialize(lSoftware), Encoding.UTF8, "application/json");

                var response = oClient.PostAsync(sURL + "/rest/v2/checkforupdate", oCont);
                response.Wait(120000); //2min max
                if (response.IsCompleted)
                {
                    List<AddSoftware> lRes = ser.Deserialize<List<AddSoftware>>(response.Result.Content.ReadAsStringAsync().Result);
                    return lRes;
                }

            }
            catch
            {
                _sURL = ""; //enforce reload endpoint URL
            }

            return new List<AddSoftware>();
        }

    }

    public class GetSoftware
    {
        public string ProductName { get; set; }

        public string Manufacturer { get; set; }

        public string Description { get; set; }

        public string ShortName { get; set; }

        public string ProductURL { get; set; }

        public string ProductVersion { get; set; }

        //public byte[] Image { get; set; }

        //public Int32? Quality { get; set; }

        public Int32? Downloads { get; set; }

        public List<string> Categories { get; set; }

        //public long IconId { get; set; }

        public long SWId { get; set; }

        public string IconHash { get; set; }

        public bool isInstalled { get; set; }

        //public string XMLFile { get; set; }

        //public string IconFile { get; set; }

        public string IconURL
        {
            get
            {
                //Support new V2 REST API
                if (!string.IsNullOrEmpty(IconHash))
                {
                    return RZRestAPIv2.sURL + "/rest/v2/GetIcon?iconhash=" + IconHash;
                }

                if (SWId > 0)
                {
                    return RZRestAPIv2.sURL + "/rest/GetIcon?id=" + SWId.ToString();
                }

                //if (IconId > 0)
                //{
                //    SWId = IconId;
                //    return RZRestAPI.sURL + "/rest/GetIcon?id=" + SWId.ToString();
                //}

                return "";
            }
        }

    }

    public class AddSoftware
    {
        public string ProductName { get; set; }

        public string Manufacturer { get; set; }

        public string Description { get; set; }

        public string ShortName { get; set; }

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
        //public long SWId { get { return IconId; } set { IconId = value; } }
        public long SWId { get; set; }

        //public long IconId { get; set; }

        public string IconHash { get; set; }
        //remove if SWId is in place 5.9.2017
        //public long IconId { get; set; }

        public string IconURL
        {
            get
            {
                //Support new V2 REST API
                if (!string.IsNullOrEmpty(IconHash))
                {
                    return RZRestAPIv2.sURL + "/rest/v2/GetIcon?iconhash=" + IconHash;
                }

                if (SWId > 0)
                {
                    //IconId = SWId;
                    string sURL = RZRestAPIv2.sURL + "/rest/v2/GetIcon?iconid=" + SWId.ToString();
                    return sURL;
                }

                //if (IconId > 0)
                //{
                //    SWId = IconId;
                //    string sURL = RZRestAPI.sURL + "/rest/v2/GetIcon?iconid=" + SWId.ToString();
                //    return sURL;
                //}
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

        public string ShortName { get; set; }

        //public byte[] Image { get; set; }

        public string IconURL { get; set; }

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
                    if (Installing && !Error)
                        return "Installing";
                    if (Downloading && !Error)
                        return "Downloading";
                    if (Installed && !Error)
                        return "Installed";
                    if (UnInstalled && !Error)
                        return "Uninstalled";
                    if (WaitingForDependency)
                        return "Installing dependencies";
                    if (PercentDownloaded == 100 && !Error)
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
