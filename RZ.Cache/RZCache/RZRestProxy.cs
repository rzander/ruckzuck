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
using System.Diagnostics;

namespace RuckZuck_WCF
{
    public static class RZRestProxy
    {
        internal static IMemoryCache _cache;
        public static string sURL = "https://ruckzuck.azurewebsites.net/wcf/RZService.svc";
        //internal static string sURL = "http://localhost:7727/RZService.svc";

        public static string Token = "deecdc6b-ad08-42ab-a743-a3c0f9033c80";
        public static int CatalogTTL = 1;
        public static string contentType = "application/json";
        public static string localURL = "http://localhost:5000";
        public static string ipfs_GW_URL = "https://gateway.ipfs.io/ipfs";
        public static string Proxy = "";
        public static string ProxyUserPW = "";
        public static int UseIPFS = 0;
        public static bool RedirectToIPFS = false;
        private static HttpClient oClient = new HttpClient();
        private static HttpClientHandler handler;

        private static byte[] bProxyUser
        {
            get
            {
                return Encoding.ASCII.GetBytes(ProxyUserPW);
            }
        }

        public static string GetAuthToken(string Username, string Password)
        {
            try
            {


                if (handler == null)
                {

                    handler = new HttpClientHandler();
                    if (!string.IsNullOrEmpty(Proxy))
                    {
                        handler.Proxy = new WebProxy(Proxy, true);
                        handler.UseProxy = true;
                    }

                    handler.AllowAutoRedirect = true;
                    handler.MaxAutomaticRedirections = 5;
                    handler.CheckCertificateRevocationList = false;
                    handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => { return true; }; //To prevent Issue with FW
                    oClient = new HttpClient(handler);
                }

                if (!_cache.TryGetValue("PW" + (Username + Password).GetHashCode(StringComparison.InvariantCultureIgnoreCase), out Token))
                {
                    oClient.DefaultRequestHeaders.Clear();
                    oClient.DefaultRequestHeaders.Accept.Clear();

                    if (!string.IsNullOrEmpty(ProxyUserPW))
                    {
                        oClient.DefaultRequestHeaders.ProxyAuthorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(bProxyUser));
                    }
                    oClient.DefaultRequestHeaders.Add("Username", Username);
                    oClient.DefaultRequestHeaders.Add("Password", Password);
                    oClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(contentType));
                    var response = oClient.GetStringAsync(sURL + "/rest/AuthenticateUser");
                    response.Wait(15000);
                    if (response.IsCompleted)
                    {
                        Token = response.Result.Replace("\"", "");
                        if (!string.IsNullOrEmpty(Token))
                        {
                            // Set cache options.
                            var cacheEntryOptions = new MemoryCacheEntryOptions()
                                // Keep in cache for this time, reset time if accessed.
                                .SetSlidingExpiration(TimeSpan.FromSeconds(300));

                            _cache.Set("PW" + (Username + Password).GetHashCode(StringComparison.InvariantCultureIgnoreCase), Token, cacheEntryOptions);
                            oClient.DefaultRequestHeaders.Add("AuthenticatedToken", Token);
                            oClient.DefaultRequestHeaders.Remove("Username");
                            oClient.DefaultRequestHeaders.Remove("Password");
                            return Token;
                        }
                    }
                }
                else
                {
                    //oClient.DefaultRequestHeaders.Add("AuthenticatedToken", Token);
                    return Token;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Auth. Error: " + ex.Message);
            }

            return "";

        }

        public static string SWResults(string Searchstring)
        {
            string sCatFile = @"wwwroot/rzcat.json";
            string sResult = "";

            if (_cache.TryGetValue("SWResult-" + Searchstring, out sResult))
            {
                return sResult;
            }
            sCatFile = @"wwwroot/rzcat.json";


            try
            {
                if (string.IsNullOrEmpty(Searchstring))
                {
                    if (File.Exists(sCatFile))
                    {
                        if (CatalogTTL == 0 || DateTime.Now.ToUniversalTime() - File.GetLastWriteTimeUtc(sCatFile) <= new TimeSpan(CatalogTTL, 0, 1))
                        {
                            sResult = File.ReadAllText(sCatFile);
                            if (sResult.StartsWith("[") & sResult.Length > 64) //check if it's JSON
                            {
                                var cacheEntryOptions = new MemoryCacheEntryOptions()
                                    .SetSlidingExpiration(TimeSpan.FromSeconds(60));
                                _cache.Set("SWResult-" + Searchstring, sResult, cacheEntryOptions);
                                return sResult;
                            }
                        }
                    }
                }
                else
                {

                }

                //oClient.DefaultRequestHeaders.Remove("AuthenticatedToken"); //Remove existing Token
                //oClient.DefaultRequestHeaders.Add("AuthenticatedToken", Token);
                //oClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(contentType));
                oClient.DefaultRequestHeaders.Accept.Clear();
                oClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                var response = oClient.GetStringAsync(sURL + "/rest/SWResults?search=" + Searchstring);
                response.Wait(10000); //10s max.
                if (response.IsCompleted)
                {
                    sResult = response.Result;
                    if (sResult.StartsWith('[') & sResult.Length > 64)
                    {
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
                    //oClient.DefaultRequestHeaders.Add("AuthenticatedToken", Token);
                    //oClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(contentType));
                    oClient.DefaultRequestHeaders.Accept.Clear();
                    oClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    var response = oClient.GetStringAsync(sURL + "/rest/SWGetShort?name=" + WebUtility.UrlEncode(Shortname));
                    response.Wait(15000);
                    if (response.IsCompleted)
                    {
                        sResult = response.Result;

                        if (sResult.StartsWith('[') & sResult.Length > 64)
                        {
                            // Set cache options.
                            var cacheEntryOptions = new MemoryCacheEntryOptions()
                                .SetSlidingExpiration(TimeSpan.FromSeconds(330));

                            _cache.Set("SWGET1-" + Shortname.GetHashCode(StringComparison.InvariantCultureIgnoreCase), sResult, cacheEntryOptions);

                            return sResult;
                        }
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
                    //oClient.DefaultRequestHeaders.Add("AuthenticatedToken", Token);
                    //oClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(contentType));
                    oClient.DefaultRequestHeaders.Accept.Clear();
                    oClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    var response = oClient.GetStringAsync(sURL + "/rest/SWGet?name=" + WebUtility.UrlEncode(PackageName) + "&ver=" + PackageVersion);
                    response.Wait(15000);
                    if (response.IsCompleted)
                    {
                        sResult = response.Result;

                        if (sResult.StartsWith('[') & sResult.Length > 64)
                        {
                            // Set cache options.
                            var cacheEntryOptions = new MemoryCacheEntryOptions()
                            .SetSlidingExpiration(TimeSpan.FromSeconds(330));
                            sResult = response.Result;
                            _cache.Set("SWGET2-" + (PackageName + PackageVersion).GetHashCode(StringComparison.InvariantCultureIgnoreCase), sResult, cacheEntryOptions);
                            return sResult;
                        }
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

                    //oClient.DefaultRequestHeaders.Add("AuthenticatedToken", Token);
                    //oClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(contentType));
                    oClient.DefaultRequestHeaders.Accept.Clear();
                    oClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    var response = oClient.GetStringAsync(sURL + "/rest/SWGetPkg?name=" + WebUtility.UrlEncode(PackageName) + "&manuf=" + WebUtility.UrlEncode(Manufacturer) + "&ver=" + PackageVersion);
                    response.Wait(5000);
                    if (response.IsCompleted)
                    {
                        sResult = response.Result;

                        if (sResult.StartsWith('[') & sResult.Length > 64)
                        {
                            // Set cache options.
                            var cacheEntryOptions = new MemoryCacheEntryOptions()
                            .SetSlidingExpiration(TimeSpan.FromSeconds(330));
                            sResult = response.Result;
                            _cache.Set("SWGET3-" + (PackageName + Manufacturer + PackageVersion).GetHashCode(StringComparison.InvariantCultureIgnoreCase), sResult, cacheEntryOptions);
                            return sResult;
                        }
                    }
                }
                catch { }
            }

            return "";
        }

        public static async Task<string> Feedback(string productName, string productVersion, string manufacturer, string architecture, string working, string userKey, string feedback)
        {
            if (!string.IsNullOrEmpty(feedback))
            {
                try
                {
                    //oClient.DefaultRequestHeaders.Add("AuthenticatedToken", Token);
                    //oClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(contentType));
                    var oRes = await oClient.GetStringAsync(sURL + "/rest/Feedback?name=" + WebUtility.UrlEncode(productName) + "&ver=" + WebUtility.UrlEncode(productVersion) + "&man=" + WebUtility.UrlEncode(manufacturer) + "&arch=" + architecture + "&ok=" + working + "&user=" + WebUtility.UrlEncode(userKey) + "&text=" + WebUtility.UrlEncode(feedback));
                    return oRes;
                }
                catch { }
            }

            return "";
        }

        public static string GetSWDefinitions(string productName, string productVersion, string manufacturer)
        {
            Console.WriteLine("GET SW:" + productName);
            string s1 = NormalizeString(productName + productVersion + manufacturer);
            string sSWFile = @"wwwroot/rzsw/" + s1 + ".json";
            contentType = "application/json";
            try
            {
                if (!Directory.Exists("wwwroot/rzsw"))
                    Directory.CreateDirectory("wwwroot/rzsw");
                if (!Directory.Exists("wwwroot/files"))
                    Directory.CreateDirectory("wwwroot/files");


                if (File.Exists(sSWFile))
                {
                    if (CatalogTTL == 0 || DateTime.Now.ToUniversalTime() - File.GetLastWriteTimeUtc(sSWFile) <= new TimeSpan(CatalogTTL, 0, 1))
                    {
                        string sContent = File.ReadAllText(sSWFile);
                        if (sContent.Length > 64)
                        {
                            return sContent;
                        }
                        else
                        {
                            File.Delete(sSWFile);
                        }
                    }
                }

                //oClient.DefaultRequestHeaders.Add("AuthenticatedToken", Token);
                //oClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(contentType));
                oClient.DefaultRequestHeaders.Accept.Clear();
                oClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                var response = oClient.GetStringAsync(sURL + "/rest/GetSWDefinition?name=" + WebUtility.UrlEncode(productName) + "&ver=" + WebUtility.UrlEncode(productVersion) + "&man=" + WebUtility.UrlEncode(manufacturer));
                response.Wait(15000);
                if (response.IsCompleted)
                {
                    string sResult = response.Result;

                    try
                    {
                        var oAddSW = Newtonsoft.Json.JsonConvert.DeserializeObject<List<AddSoftware>>(sResult);
                        bool isReady = true;
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
                                        isReady = false;
                                        if (!RedirectToIPFS)
                                        {
                                            var oRes = _DownloadFile(oDL.URL, sDir + "/" + oDL.FileName);
                                            Thread.Sleep(500);
                                        }
                                        else
                                            isReady = true;

                                    }

                                    if (!IsFileLocked(new FileInfo(sDir + "/" + oDL.FileName)))
                                    {
                                        string sHash = "";
                                        if (UseIPFS == 1)
                                        {
                                            sHash = IPFSAdd(sDir + "/" + oDL.FileName);
                                        }
                                        if(RedirectToIPFS)
                                        {
                                            sHash = GetIPFS(oSW.ContentID, oDL.FileName).Trim('"');
                                        }
                                        if(!string.IsNullOrEmpty(sHash))
                                        {
                                            if (!RedirectToIPFS)
                                            {
                                                long lSize = new FileInfo(sDir + "/" + oDL.FileName).Length;
                                                if (lSize > 8100 & sHash.StartsWith("Qm"))
                                                {
                                                    AddIPFS(oSW.ContentID, oDL.FileName, sHash, lSize, true);
                                                }
                                            }
                                            //https://gateway.ipfs.io/ipfs/QmNMKonBPBuE8NyGPcu1prkaE9MaJBHTGMLB9JX6sFMStQ/
                                            oDL.URL = ipfs_GW_URL + "/" + sHash + "/";
                                        }
                                        else
                                        {
                                            oDL.URL = localURL + "/rest/dl/" + oSW.ContentID + "/" + oDL.FileName;
                                        }

                                    }
                                    else
                                    {
                                        isReady = false;
                                    }

                                }
                            }
                        }
                        if (isReady)
                        {
                            sResult = Newtonsoft.Json.JsonConvert.SerializeObject(oAddSW);
                            if (sResult.StartsWith('[') & sResult.Length > 64)
                            {
                                File.WriteAllText(sSWFile, sResult);
                            }
                        }

                    }
                    catch(Exception ex)
                    {
                        Console.WriteLine("Error:" + ex.Message);
                    }


                    return sResult;
                }


            }
            catch(Exception ex)
            {
                Console.WriteLine("ERROR: " + ex.Message);
            }

            if (File.Exists(sSWFile))
            {
                    return File.ReadAllText(sSWFile);
            }
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
                    return File.Open(@"wwwroot/icons/" + iconid.ToString() + ".jpg", FileMode.Open, FileAccess.Read, FileShare.Read);
                }
                else
                {
                    //oClient.DefaultRequestHeaders.Add("AuthenticatedToken", Token);
                    //oClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("image/jpeg"));
                    var response = oClient.GetStreamAsync(sURL + "/rest/GetIcon?id=" + iconid.ToString());
                    response.Wait(5000);
                    if (response.IsCompleted)
                    {
                        var oRet = response.Result;
                        using (var sIcon = new FileStream(@"wwwroot/icons/" + iconid.ToString() + ".jpg", FileMode.Create))
                        {
                            response.Result.CopyTo(sIcon);
                            sIcon.Flush();
                            sIcon.Dispose();
                        }
                        return File.Open(@"wwwroot/icons/" + iconid.ToString() + ".jpg", FileMode.Open, FileAccess.Read, FileShare.Read);
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
                Console.WriteLine("GET File:" + filename);
                if (File.Exists(@"wwwroot/files/" + filename))
                {
                    return File.Open(@"wwwroot/files/" + filename, FileMode.Open, FileAccess.Read, FileShare.Read);
                }
            }
            catch { }

            return null;
        }

        public static async void TrackDownloadsNew(string SWId, string Architecture)
        {
            try
            {
                if (!string.IsNullOrEmpty(SWId) & !string.IsNullOrEmpty(Architecture))
                {
                    //oClient.DefaultRequestHeaders.Add("AuthenticatedToken", Token);
                    //oClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(contentType));
                    await oClient.GetStringAsync(sURL + "/rest/TrackDownloadsNew?SWId=" + SWId.ToString() + "&arch=" + WebUtility.UrlEncode(Architecture));
                }
            }
            catch { }
        }

        public static string CheckForUpdate(string lSoftware)
        {
            string sResult = "";

            try
            {
                //lets check if any previous request already found SW without update
                JsonSerializerSettings oJSet = new JsonSerializerSettings();
                oJSet.NullValueHandling = NullValueHandling.Ignore;
                var oSWUpload = JsonConvert.DeserializeObject<List<AddSoftware>>(lSoftware, oJSet);
                List<AddSoftware> lSWToCheck = new List<AddSoftware>();
                foreach (AddSoftware oSW in oSWUpload)
                {
                    AddSoftware oCheck;
                    if (!_cache.TryGetValue("noUpd" + oSW.ProductName + oSW.ProductVersion + oSW.Manufacturer, out oCheck))
                    {
                        lSWToCheck.Add(oSW);
                    }
                }
                if (lSWToCheck.Count > 0)
                {
                    lSoftware = JsonConvert.SerializeObject(lSWToCheck);
                }
                else return ""; //no updates required

                //oClient.DefaultRequestHeaders.Add("AuthenticatedToken", Token);
                //oClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(contentType));
                using (HttpContent oCont = new StringContent(lSoftware, Encoding.UTF8, contentType))
                {

                    var response = oClient.PostAsync(sURL + "/rest/CheckForUpdate", oCont);
                    response.Wait(10000);
                    if (response.IsCompleted)
                    {
                        string responseBody = response.Result.Content.ReadAsStringAsync().Result;

                        sResult = responseBody;

                        try
                        {
                            var lSWUpd = Newtonsoft.Json.JsonConvert.DeserializeObject<List<AddSoftware>>(sResult);
                            if (lSWUpd.Count == 0) //No Updates found -> cache all SW Items to prevent further check
                            {
                                // Set cache options.
                                var cacheEntryOptions = new MemoryCacheEntryOptions()
                                    .SetSlidingExpiration(new TimeSpan(4, 0, 0)); //Cache 4h

                                foreach (AddSoftware oSW in lSWToCheck)
                                {
                                    _cache.Set("noUpd" + oSW.ProductName + oSW.ProductVersion + oSW.Manufacturer, oSW, cacheEntryOptions);
                                }
                            }
                        }
                        catch { }

                        return sResult;
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

                //oClient.DefaultRequestHeaders.Add("AuthenticatedToken", Token);
                //oClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(contentType));
                HttpContent oCont = new StringContent(lSoftware, Encoding.UTF8, contentType);

                var response = oClient.PostAsync(sURL + "/rest/UploadSWEntry", oCont);
                response.Wait(5000);

                if (response.IsCompleted)
                {
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

        public static bool AddIPFS(string contentID, string fileName, string iPFS, long size, bool update)
        {
            try
            {
                var response = oClient.GetAsync(sURL + "/rest/AddIPFS?Id=" + contentID + "&file=" + fileName + "&hash=" + iPFS + "&size=" + size + "&upd=" + update);
                response.Wait(5000);

                if (response.IsCompleted)
                {
                    if (response.Result.StatusCode == HttpStatusCode.OK)
                    {
                        //Console.WriteLine("AddIPFS:" + contentID + "/" + fileName + "/" + iPFS);
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

        public static string GetIPFS(string contentID, string fileName)
        {
            try
            {
                var response = oClient.GetStringAsync(sURL + "/rest/GetIPFS?Id=" + contentID + "&file=" + fileName);
                response.Wait(5000);

                if (response.IsCompleted)
                {
                    return response.Result.Trim('"');
                }
            }
            catch { }

            return "";
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
                oClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "chocolatey command line");

                using (HttpResponseMessage response = await oClient.GetAsync(URL, HttpCompletionOption.ResponseHeadersRead))
                using (Stream streamToReadFrom = await response.Content.ReadAsStreamAsync())
                {
                    string fileToWriteTo = FileName; // Path.GetTempFileName();
                    
                    using (Stream streamToWriteTo = File.Open(fileToWriteTo, FileMode.Create))
                    {
                        await streamToReadFrom.CopyToAsync(streamToWriteTo);
                    }
                    Console.WriteLine("Donwloaded: " + URL);
                }


            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }

            return true;
        }

        private static bool IsFileLocked(FileInfo file)
        {
            if (!file.Exists)
                return false;

            FileStream stream = null;

            try
            {
                stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.None);
            }
            catch (IOException)
            {
                return true;
            }
            finally
            {
                if (stream != null)
                    stream.Close();
            }

            //file is not locked
            return false;
        }

        /// <summary>
        /// Add file to IPFS
        /// </summary>
        /// <param name="path">File</param>
        /// <returns>IPFS Hash</returns>
        public static string IPFSAdd(string path)
        {
            string sResult = "";
            try
            {
                if (UseIPFS > 0)
                {
                    var oProc = System.Diagnostics.Process.Start("ipfs", "add --nocopy \"" + path + "\"");
                    oProc.StartInfo.RedirectStandardOutput = true;
                    oProc.StartInfo.UseShellExecute = false;
                    List<string> output = new List<string>();
                    oProc.OutputDataReceived += new DataReceivedEventHandler((sender, e) =>
                    {
                        output.Add(e.Data);
                    });
                    oProc.Start();
                    oProc.BeginOutputReadLine();
                    oProc.WaitForExit(30000);
                    sResult = output.First().Split(' ')[1];
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine("ERROR: " + ex.Message);
            }

            return sResult;
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
                    string URL = sURL + "/rest/GetIcon?id=" + SWId.ToString();
                    return URL;
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
