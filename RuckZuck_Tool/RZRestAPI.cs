using System;
using System.Collections.Generic;
using System.Net;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using System.Linq;

namespace RuckZuck_WCF
{
    public static class RZRestAPI
    {
        public static string sURL = "https://ruckzuck.azurewebsites.net/wcf/RZService.svc";
        //internal static string sURL = "http://localhost:7727/RZService.svc";

        public static string Token;
        public static string GetAuthToken(string Username, string Password)
        {
            try
            {
                WebClient oClient = new WebClient();
                oClient.Headers.Add("Username:" + Username);
                oClient.Headers.Add("Password:" + Password);
                oClient.Headers.Add("Content-Type:application/json");
                string sResult = oClient.DownloadString(sURL + "/rest/AuthenticateUser");
                Token = sResult.Replace("\"", "");
                return Token;
            }
            catch { }

            return "";

        }

        public static List<GetSoftware> SWGet(string Shortname)
        {
            WebClient oClient = new WebClient();
            oClient.Headers.Add("AuthenticatedToken:" + Token);
            oClient.Headers.Add("Content-Type:application/xml");
            string sResult = oClient.DownloadString(sURL + "/rest/SWGetShort?name=" + WebUtility.UrlEncode(Shortname));

            return DataContractDeSerializeObject<List<GetSoftware>>(sResult);
        }

        public static List<GetSoftware> SWGet(string PackageName, string PackageVersion)
        {
            WebClient oClient = new WebClient();
            oClient.Headers.Add("AuthenticatedToken:" + Token);
            oClient.Headers.Add("Content-Type:application/xml");
            string sResult = oClient.DownloadString(sURL + "/rest/SWGet?name=" + WebUtility.UrlEncode(PackageName) + "&ver=" + PackageVersion);

            return DataContractDeSerializeObject<List<GetSoftware>>(sResult);
        }

        public static List<GetSoftware> SWGet(string PackageName, string Manufacturer, string PackageVersion)
        {
            WebClient oClient = new WebClient();
            oClient.Headers.Add("AuthenticatedToken:" + Token);
            oClient.Headers.Add("Content-Type:application/xml");
            string sResult = oClient.DownloadString(sURL + "/rest/SWGetPkg?name=" + WebUtility.UrlEncode(PackageName) + "&manuf=" + WebUtility.UrlEncode(Manufacturer) + "&ver=" + PackageVersion);

            return DataContractDeSerializeObject<List<GetSoftware>>(sResult);
        }

        public static List<GetSoftware> SWResults(string Searchstring)
        {
            try
            {
                WebClient oClient = new WebClient();
                oClient.Headers.Add("AuthenticatedToken:" + Token);
                oClient.Headers.Add("Content-Type:application/xml");
                string sResult = oClient.DownloadString(sURL + "/rest/SWResults?search=" + Searchstring);

                return DataContractDeSerializeObject<List<GetSoftware>>(sResult);
            }
            catch { }

            return new List<GetSoftware>();
        }

        public static async Task Feedback(string productName, string productVersion, string manufacturer, string working, string userKey, string feedback)
        {
            if (!string.IsNullOrEmpty(feedback))
            {
                await Task.Run(() =>
                {
                    try
                    {
                        WebClient oClient = new WebClient();
                        oClient.Headers.Add("AuthenticatedToken:" + Token);
                        oClient.Headers.Add("Content-Type:application/xml");
                        string sResult = oClient.DownloadString(sURL + "/rest/Feedback?name=" + WebUtility.UrlEncode(productName) + "&ver=" + WebUtility.UrlEncode(productVersion) + "&man=" + WebUtility.UrlEncode(manufacturer) + "&ok=" + working + "&user=" + WebUtility.UrlEncode(userKey) + "&text=" + WebUtility.UrlEncode(feedback));
                    }
                    catch { }
                });
            }
        }

        public static List<AddSoftware> GetSWDefinitions(string productName, string productVersion, string manufacturer)
        {
            WebClient oClient = new WebClient();
            oClient.Headers.Add("AuthenticatedToken:" + Token);
            oClient.Headers.Add("Content-Type:application/xml");
            string sResult = oClient.DownloadString(sURL + "/rest/GetSWDefinition?name=" + WebUtility.UrlEncode(productName) + "&ver=" + WebUtility.UrlEncode(productVersion) + "&man=" + WebUtility.UrlEncode(manufacturer));

            return DataContractDeSerializeObject<List<AddSoftware>>(sResult);
        }

        public static List<AddSoftware> CheckForUpdate(List<AddSoftware> lSoftware)
        {
            try
            {
                string sData = DataContractSerializeObject<List<AddSoftware>>(lSoftware);

                byte[] bytes = Encoding.UTF8.GetBytes(sData);
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(sURL + "/rest/CheckForUpdateXml");
                request.Method = "POST";
                request.ContentLength = bytes.Length;
                request.Headers.Add("AuthenticatedToken:" + Token);
                request.ContentType = "application/xml";

                using (Stream requestStream = request.GetRequestStream())
                {
                    requestStream.Write(bytes, 0, bytes.Length);
                }

                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                Stream resStream = response.GetResponseStream();
                StreamReader rdStreamRdr = new StreamReader(resStream);
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    string message = String.Format("POST Failed. Received HTTP {0}",
                                response.StatusCode);
                    throw new ApplicationException(message);
                }
                else
                {
                    string message = rdStreamRdr.ReadToEnd();
                    message.ToString();
                    var oResult = DataContractDeSerializeObject<List<AddSoftware>>(message);
                    return oResult;
                }
            }
            catch { }

            return new List<AddSoftware>();
        }

        public static bool UploadSWEntry(AddSoftware lSoftware)
        {
            try
            {
                string sData = DataContractSerializeObject<AddSoftware>(lSoftware);

                byte[] bytes = Encoding.UTF8.GetBytes(sData);
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(sURL + "/rest/UploadSWEntry");
                request.Method = "POST";
                request.ContentLength = bytes.Length;
                request.Headers.Add("AuthenticatedToken:" + Token);
                request.ContentType = "application/xml";

                using (Stream requestStream = request.GetRequestStream())
                {
                    requestStream.Write(bytes, 0, bytes.Length);
                }

                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                Stream resStream = response.GetResponseStream();
                StreamReader rdStreamRdr = new StreamReader(resStream);
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    string message = String.Format("POST Failed. Received HTTP {0}",
                                response.StatusCode);
                    throw new ApplicationException(message);
                }
                else
                {
                    string message = rdStreamRdr.ReadToEnd();
                    var oResult = DataContractDeSerializeObject<bool>(message);
                    return oResult;
                }
            }
            catch { }

            return false;
        }

        public static string DataContractSerializeObject<T>(T objectToSerialize)
        {
            using (MemoryStream memStm = new MemoryStream())
            {
                var serializer = new DataContractSerializer(typeof(T));
                serializer.WriteObject(memStm, objectToSerialize);

                memStm.Seek(0, SeekOrigin.Begin);

                using (var streamReader = new StreamReader(memStm))
                {
                    string result = streamReader.ReadToEnd();
                    return result;
                }
            }
        }

        public static T DataContractDeSerializeObject<T>(string objectToDeSerialize)
        {
            // string sHeader = "<?xml version=\"1.0\" encoding=\"UTF-16LE\" ?>";
            var serializer = new DataContractSerializer(typeof(T));

            T response;

            using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(objectToDeSerialize)))
            {
                response = (T)serializer.ReadObject(ms);
            }

            return response;
        }

        public static async void TrackDownloads(string contentID)
        {
            await Task.Run(() =>
            {
                try
                {
                    WebClient oClient = new WebClient();
                    oClient.Headers.Add("AuthenticatedToken:" + Token);
                    oClient.Headers.Add("Content-Type:application/xml");
                    string sResult = oClient.DownloadString(sURL + "/rest/TrackDownloads/" + WebUtility.UrlEncode(contentID));
                }
                catch { }
            });
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
