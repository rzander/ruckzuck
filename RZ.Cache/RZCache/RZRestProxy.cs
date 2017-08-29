using System;
using System.Collections.Generic;
using System.Net;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Xml.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using static RuckZuck_WCF.RZRestProxy;
using System.Threading;
using Microsoft.Extensions.Caching.Memory;

namespace RuckZuck_WCF
{
    public static class RZRestProxy
    {
        internal static IMemoryCache _cache;
        public static string sURL = "https://ruckzuck.azurewebsites.net/wcf/RZService.svc";
        //internal static string sURL = "http://localhost:7727/RZService.svc";

        public static string Token = "deecdc6b-ad08-42ab-a743-a3c0f9033c80";
        public static int CatalogTTL = 1;
        public static string contentType = "application/xml";
        public static string localURL = "http://localhost:5000";

        public static string GetAuthToken(string Username, string Password)
        {
            try
            {

                if (!_cache.TryGetValue("PW" + (Username + Password).GetHashCode(StringComparison.InvariantCultureIgnoreCase), out Token))
                {
                    using (var oClient = new HttpClient())
                    {
                        oClient.DefaultRequestHeaders.Add("Username", Username);
                        oClient.DefaultRequestHeaders.Add("Password", Password);
                        oClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                        var response = oClient.GetStringAsync(sURL + "/rest/AuthenticateUser");
                        response.Wait(5000);
                        if (response.IsCompleted)
                        {
                            Token = response.Result.Replace("\"", "");

                            // Set cache options.
                            var cacheEntryOptions = new MemoryCacheEntryOptions()
                                // Keep in cache for this time, reset time if accessed.
                                .SetSlidingExpiration(TimeSpan.FromSeconds(300));

                            _cache.Set("PW" + (Username + Password).GetHashCode(StringComparison.InvariantCultureIgnoreCase), Token, cacheEntryOptions);

                            return Token;
                        }
                    }
                }
                else
                    return Token;
            }
            catch(Exception ex)
            {
                Console.WriteLine("Auth. Error: " + ex.Message);
            }

            return "";

        }

        public static string SWResults(string Searchstring)
        {
            string sCatFile = @"wwwroot/rzcat.xml";
            string sResult = "";
            if (contentType.ToLower() == "application/json")
            {
                if (_cache.TryGetValue("SWResult-" + Searchstring, out sResult))
                {
                    return sResult;
                }
                sCatFile = @"wwwroot/rzcat.json";
            }

            try
            {
                if (string.IsNullOrEmpty(Searchstring))
                {
                    if (File.Exists(sCatFile))
                    {
                        if (CatalogTTL == 0 || DateTime.Now.ToUniversalTime() - File.GetCreationTime(sCatFile).ToUniversalTime() <= new TimeSpan(CatalogTTL, 0, 1))
                        {
                            sResult = File.ReadAllText(sCatFile);
                            var cacheEntryOptions = new MemoryCacheEntryOptions()
                                .SetSlidingExpiration(TimeSpan.FromSeconds(60));
                            _cache.Set("SWResult-" + Searchstring, sResult, cacheEntryOptions);
                            return sResult;
                        }
                    }
                }
                else
                {

                }

                using (var handler = new HttpClientHandler())
                {
                    //handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => { return true; }; //To prevent Issue with FW

                    using (var oClient = new HttpClient(handler))
                    {
                        oClient.DefaultRequestHeaders.Add("AuthenticatedToken", Token);
                        oClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(contentType));
                        var response = oClient.GetStringAsync(sURL + "/rest/SWResults?search=" + Searchstring);
                        response.Wait(7000); //7s max.
                        if (response.IsCompleted)
                        {
                            sResult = response.Result;
                            // Set cache options.
                            var cacheEntryOptions = new MemoryCacheEntryOptions()
                                .SetSlidingExpiration(TimeSpan.FromSeconds(60));
                            _cache.Set("SWResult-" + Searchstring, sResult, cacheEntryOptions);

                            if (string.IsNullOrEmpty(Searchstring))
                                File.WriteAllText(sCatFile, sResult);

                            return sResult;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            //return old File
            if (File.Exists(sCatFile))
            {
                return File.ReadAllText(sCatFile);
            }

            return "";
        }

        public static string SWGet(string Shortname)
        {
            string sResult = "";
            if (!_cache.TryGetValue("SWGET1-" + Shortname.GetHashCode(StringComparison.InvariantCultureIgnoreCase), out sResult))
            {
                try
                {
                    var oClient = new HttpClient();
                    oClient.DefaultRequestHeaders.Add("AuthenticatedToken", Token);
                    oClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(contentType));
                    var response = oClient.GetStringAsync(sURL + "/rest/SWGetShort?name=" + WebUtility.UrlEncode(Shortname));
                    response.Wait(5000);
                    if (response.IsCompleted)
                    {
                        sResult = response.Result;

                        // Set cache options.
                        var cacheEntryOptions = new MemoryCacheEntryOptions()
                            .SetSlidingExpiration(TimeSpan.FromSeconds(330));

                        _cache.Set("SWGET1-" + Shortname.GetHashCode(StringComparison.InvariantCultureIgnoreCase), sResult, cacheEntryOptions);

                        return sResult;
                    }
                }
                catch { }
            }

            return sResult;
        }

        public static string SWGet(string PackageName, string PackageVersion)
        {
            string sResult = "";
            if (!_cache.TryGetValue("SWGET2-" + (PackageName + PackageVersion).GetHashCode(StringComparison.InvariantCultureIgnoreCase), out sResult))
            {
                try
                {
                    var oClient = new HttpClient();
                    oClient.DefaultRequestHeaders.Add("AuthenticatedToken", Token);
                    oClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(contentType));
                    var response = oClient.GetStringAsync(sURL + "/rest/SWGet?name=" + WebUtility.UrlEncode(PackageName) + "&ver=" + PackageVersion);
                    response.Wait(5000);
                    if (response.IsCompleted)
                    {
                        sResult = response.Result;

                        // Set cache options.
                        var cacheEntryOptions = new MemoryCacheEntryOptions()
                            .SetSlidingExpiration(TimeSpan.FromSeconds(330));
                        sResult = response.Result;
                        _cache.Set("SWGET2-" + (PackageName + PackageVersion).GetHashCode(StringComparison.InvariantCultureIgnoreCase), sResult, cacheEntryOptions);
                        return sResult;
                    }
                }
                catch { }
            }

            return "";
        }

        public static string SWGet(string PackageName, string Manufacturer, string PackageVersion)
        {
            string sResult = "";
            if (!_cache.TryGetValue("SWGET3-" + (PackageName + Manufacturer + PackageVersion).GetHashCode(StringComparison.InvariantCultureIgnoreCase), out sResult))
            {
                try
                {
                    var oClient = new HttpClient();
                    oClient.DefaultRequestHeaders.Add("AuthenticatedToken", Token);
                    oClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(contentType));
                    var response = oClient.GetStringAsync(sURL + "/rest/SWGetPkg?name=" + WebUtility.UrlEncode(PackageName) + "&manuf=" + WebUtility.UrlEncode(Manufacturer) + "&ver=" + PackageVersion);
                    response.Wait(5000);
                    if (response.IsCompleted)
                    {
                        sResult = response.Result;

                        // Set cache options.
                        var cacheEntryOptions = new MemoryCacheEntryOptions()
                            .SetSlidingExpiration(TimeSpan.FromSeconds(330));
                        sResult = response.Result;
                        _cache.Set("SWGET3-" + (PackageName + Manufacturer + PackageVersion).GetHashCode(StringComparison.InvariantCultureIgnoreCase), sResult, cacheEntryOptions);
                        return sResult;
                    }
                }
                catch { }
            }

            return "";
        }

        public static async Task<string> Feedback(string productName, string productVersion, string manufacturer, string working, string userKey, string feedback)
        {
            if (!string.IsNullOrEmpty(feedback))
            {
                try
                {
                    var oClient = new HttpClient();
                    oClient.DefaultRequestHeaders.Add("AuthenticatedToken", Token);
                    oClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(contentType));
                    var oRes = await oClient.GetStringAsync(sURL + "/rest/Feedback?name=" + WebUtility.UrlEncode(productName) + "&ver=" + WebUtility.UrlEncode(productVersion) + "&man=" + WebUtility.UrlEncode(manufacturer) + "&ok=" + working + "&user=" + WebUtility.UrlEncode(userKey) + "&text=" + WebUtility.UrlEncode(feedback));
                    return oRes;
                }
                catch { }
            }

            return "";
        }

        public static string GetSWDefinitions(string productName, string productVersion, string manufacturer)
        {
            try
            {
                string s1 = NormalizeString(productName + productVersion + manufacturer);

                if (!Directory.Exists("wwwroot/rzsw"))
                    Directory.CreateDirectory("wwwroot/rzsw");
                if (!Directory.Exists("wwwroot/files"))
                    Directory.CreateDirectory("wwwroot/files");

                string sSWFile = @"wwwroot/rzsw/" + s1 + ".xml";

                if (contentType.ToLower() == "application/json")
                    sSWFile = @"wwwroot/rzsw/" + s1 + ".json";

                if (File.Exists(sSWFile))
                {
                    if (CatalogTTL == 0 || DateTime.Now.ToUniversalTime() - File.GetCreationTime(sSWFile).ToUniversalTime() <= new TimeSpan(CatalogTTL, 0, 1))
                    {
                        return File.ReadAllText(sSWFile);
                    }
                }

                using (var handler = new HttpClientHandler())
                {

                    handler.AllowAutoRedirect = true;
                    handler.MaxAutomaticRedirections = 5;
                    handler.CheckCertificateRevocationList = false;

                    using (var oClient = new HttpClient(handler))
                    {
                        oClient.DefaultRequestHeaders.Add("AuthenticatedToken", Token);
                        oClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(contentType));
                        var response = oClient.GetStringAsync(sURL + "/rest/GetSWDefinition?name=" + WebUtility.UrlEncode(productName) + "&ver=" + WebUtility.UrlEncode(productVersion) + "&man=" + WebUtility.UrlEncode(manufacturer));
                        response.Wait(5000);
                        if (response.IsCompleted)
                        {
                            string sResult = response.Result;

                            Task.Run(() =>
                            {
                                try
                                {
                                    if (contentType.ToLower() == "application/json")
                                    {
                                        var oAddSW = Newtonsoft.Json.JsonConvert.DeserializeObject<List<AddSoftware>>(sResult);
                                        foreach (var oSW in oAddSW)
                                        {
                                            foreach (var oDL in oSW.Files)
                                            {
                                                string sDir = @"wwwroot/files/" + oSW.ContentID;
                                                if (!Directory.Exists(sDir))
                                                {
                                                    Directory.CreateDirectory(sDir);
                                                }

                                                if (oDL.URL.StartsWith("http") || oDL.URL.StartsWith("ftp"))
                                                {
                                                    if (!File.Exists(sDir + "/" + oDL.FileName))
                                                    {
                                                        var oRes = _DownloadFile(oDL.URL, sDir + "/" + oDL.FileName).Status;
                                                    }

                                                    oDL.URL = localURL + "/rest/dl/" + oSW.ContentID + "/" + oDL.FileName;

                                                }
                                            }
                                        }
                                        sResult = Newtonsoft.Json.JsonConvert.SerializeObject(oAddSW);
                                        File.WriteAllText(sSWFile, sResult);
                                    }
                                }
                                catch { }
                            });

                            return response.Result;
                        }
                    }
                }
            }
            catch { }

            return "";

        }

        /// <summary>
        /// Needed because the  JavaScriptSerializer does not understand base64 byte arrays (Image).. so we have to convert them...
        /// </summary>
        public class ByteArrayConverter : JsonConverter
        {
            public override void WriteJson(
                JsonWriter writer,
                object value,
                JsonSerializer serializer)
            {
                if (value == null)
                {
                    writer.WriteNull();
                    return;
                }

                byte[] data = (byte[])value;

                // Compose an array.
                writer.WriteStartArray();

                for (var i = 0; i < data.Length; i++)
                {
                    writer.WriteValue(data[i]);
                }

                writer.WriteEndArray();
            }

            public override object ReadJson(
                JsonReader reader,
                Type objectType,
                object existingValue,
                JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.StartArray)
                {
                    var byteList = new List<byte>();

                    while (reader.Read())
                    {
                        switch (reader.TokenType)
                        {
                            case JsonToken.Integer:
                                byteList.Add(Convert.ToByte(reader.Value));
                                break;
                            case JsonToken.EndArray:
                                return byteList.ToArray();
                            case JsonToken.Comment:
                                // skip
                                break;
                            default:
                                throw new Exception(
                                string.Format(
                                    "Unexpected token when reading bytes: {0}",
                                    reader.TokenType));
                        }
                    }

                    throw new Exception("Unexpected end when reading bytes.");
                }
                else
                {
                    throw new Exception(
                        string.Format(
                            "Unexpected token parsing binary. "
                            + "Expected StartArray, got {0}.",
                            reader.TokenType));
                }
            }

            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(byte[]);
            }
        }

        public static Stream GetIcon(Int32 iconid)
        {
            try
            {
                if (!Directory.Exists("wwwroot/icons"))
                    Directory.CreateDirectory("wwwroot/icons");

                if (File.Exists(@"wwwroot/icons/" + iconid.ToString() + ".jpg"))
                {
                    return File.Open(@"wwwroot/icons/" + iconid.ToString() + ".jpg", FileMode.Open);
                }
                else
                {
                    var oClient = new HttpClient();

                    //oClient.DefaultRequestHeaders.Add("AuthenticatedToken", Token);
                    oClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("image/jpeg"));
                    var response = oClient.GetStreamAsync(sURL + "/rest/GetIcon?id=" + iconid.ToString());
                    response.Wait(5000);
                    if (response.Result != null)
                    {
                        var oRet = response.Result;
                        var sIcon = new System.IO.FileStream(@"wwwroot/icons/" + iconid.ToString() + ".jpg", FileMode.Create);
                        response.Result.CopyTo(sIcon);
                        sIcon.Flush();
                        sIcon.Dispose();
                        return File.Open(@"wwwroot/icons/" + iconid.ToString() + ".jpg", FileMode.Open);
                    }
                }
            }
            catch { }

            return null;
        }

        public static Stream GetFile(string filename)
        {
            try
            {
                filename = filename.Replace('\\', '/');

                if (File.Exists(@"wwwroot/files/" + filename))
                {
                    return File.Open(@"wwwroot/files/" + filename, FileMode.Open);
                }
            }
            catch { }

            return null;
        }

        public static async void TrackDownloads(string contentID)
        {
            try
            {
                var oClient = new HttpClient();
                oClient.DefaultRequestHeaders.Add("AuthenticatedToken", Token);
                oClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(contentType));
                await oClient.GetStringAsync(sURL + "/rest/TrackDownloads/" + WebUtility.UrlEncode(contentID));
            }
            catch { }
        }

        public static string CheckForUpdate(string lSoftware)
        {
            string sResult = "";
            if (_cache.TryGetValue("CHK" + lSoftware.GetHashCode(StringComparison.InvariantCultureIgnoreCase), out sResult))
            {
                return sResult;
            }
            try
            {
                using (var oClient = new HttpClient())
                {
                    oClient.DefaultRequestHeaders.Add("AuthenticatedToken", Token);
                    oClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(contentType));
                    HttpContent oCont = new StringContent(lSoftware, Encoding.UTF8, contentType);
                    if (contentType == "application/xml")
                    {
                        var response = oClient.PostAsync(sURL + "/rest/CheckForUpdateXml", oCont);
                        response.Wait(5000);
                        if (response.IsCompleted)
                        {
                            string responseBody = response.Result.Content.ReadAsStringAsync().Result;
                            sResult = responseBody;
                            // Set cache options.
                            var cacheEntryOptions = new MemoryCacheEntryOptions()
                                .SetSlidingExpiration(TimeSpan.FromSeconds(60));

                            _cache.Set("CHK" + lSoftware.GetHashCode(StringComparison.InvariantCultureIgnoreCase), sResult, cacheEntryOptions);

                            return sResult;
                        }
                    }

                    if (contentType == "application/json")
                    {
                        var response = oClient.PostAsync(sURL + "/rest/CheckForUpdateJSON", oCont);
                        response.Wait(5000);
                        if (response.IsCompleted)
                        {
                            string responseBody = response.Result.Content.ReadAsStringAsync().Result;
                            return responseBody;
                        }
                    }
                }


            }
            catch { }

            return "";
        }

        public static bool UploadSWEntry(string lSoftware)
        {
            try
            {
                var oClient = new HttpClient();
                oClient.DefaultRequestHeaders.Add("AuthenticatedToken", Token);
                oClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(contentType));
                HttpContent oCont = new StringContent(lSoftware, Encoding.UTF8, contentType);
                if (contentType == "application/xml")
                {
                    var response = oClient.PostAsync(sURL + "/rest/UploadSWEntry", oCont);
                    response.Wait(5000);

                    if (response.Result.StatusCode == HttpStatusCode.OK)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }

            }
            catch { }

            return false;
        }

        public static string NormalizeString(string Input)
        {
            char[] arr = (Input).ToCharArray();

            arr = Array.FindAll<char>(arr, (c => (char.IsLetterOrDigit(c))));

            return new string(arr);
        }

        private static async Task<bool> _DownloadFile(string URL, string FileName)
        {
            try
            {
                using (HttpClientHandler handler = new HttpClientHandler())
                {
                    handler.AllowAutoRedirect = true;
                    handler.MaxAutomaticRedirections = 5;
                    handler.CheckCertificateRevocationList = false;
                    handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => { return true; }; //To prevent Issue with FW

                    using (var httpClient = new HttpClient(handler))
                    {
                        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "chocolatey command line");
                        using (var request = new HttpRequestMessage(HttpMethod.Get, new Uri(URL)))
                        {
                            using (Stream contentStream = await (await httpClient.SendAsync(request)).Content.ReadAsStreamAsync(),
                            stream = new FileStream(FileName, FileMode.Create, FileAccess.Write, FileShare.None, 32768, true))
                            {
                                await contentStream.CopyToAsync(stream);
                            }
                            Console.WriteLine("Donwloaded: " + URL);
                        }

                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }

            return true;
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
                    return RZRestProxy.sURL + "/rest/GetIcon?id=" + IconId.ToString();
                }
                else
                {
                    return "File://" + IconFile;
                }
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

        [JsonConverter(typeof(ByteArrayConverter))]
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

        //public RZUpdate.SWUpdate SWUpd { get; set; }
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
