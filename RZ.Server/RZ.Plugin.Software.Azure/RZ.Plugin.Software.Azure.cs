using Microsoft.Extensions.Caching.Memory;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;
using RZ.Server;
using RZ.Server.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Plugin_Software
{
    public class Plugin_Software : ISoftware
    {
        private IMemoryCache _cache;
        private long SlidingExpiration = 300; //5min cache for Softwares

        public string Name
        {
            get
            {
                return Assembly.GetExecutingAssembly().ManifestModule.Name;
            }
        }

        public Dictionary<string, string> Settings { get; set; }

        public void Init(string PluginPath)
        {
            //Check if MemoryCache is initialized
            if (_cache != null)
            {
                _cache.Dispose();
            }

            _cache = new MemoryCache(new MemoryCacheOptions());

            if (Settings == null)
                Settings = new Dictionary<string, string>();

            string wwwpath = Settings["wwwPath"] ?? PluginPath;

            string icons = Path.Combine(wwwpath, "icons");
            if (!Directory.Exists(icons))
                Directory.CreateDirectory(icons);

            Settings.Add("icons", icons);
        }

        public JArray GetSoftwares(string shortname)
        {
            JArray jResult = new JArray();
            //Try to get value from Memory
            if (_cache.TryGetValue("sn-" + shortname.ToLower(), out jResult))
            {
                return jResult;
            }

            foreach(JObject jObj in getlatestSoftware(Settings["catURL"] + "?" + Settings["catSAS"], shortname.ToLower(), "known"))
            {
                jResult = GetSoftwares(jObj["ProductName"].ToString().ToLower(), jObj["ProductVersion"].ToString().ToLower(), jObj["Manufacturer"].ToString().ToLower());

                var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(SlidingExpiration)); //cache hash for x Seconds
                _cache.Set("sn-" + shortname.ToLower(), jResult, cacheEntryOptions);

                return jResult;
            }
            


            return new JArray();
        }

        public JArray GetSoftwares(string name = "", string ver = "", string man = "_unknown")
        {
            JArray jResult = new JArray();

            try
            {
                if (string.IsNullOrEmpty(man))
                    man = "_unknown";

                //Try to get value from Memory
                if (_cache.TryGetValue("mnv-" + Base.clean(man).ToLower() + Base.clean(name).ToLower() + Base.clean(ver).ToLower(), out jResult))
                {
                    return jResult;
                }


                CloudBlobContainer oRepoContainer = new CloudBlobContainer(new Uri(Settings["repoURL"] + "?" + Settings["repoSAS"]));
                string sID = Base.clean(man).ToLower() + "/" + Base.clean(name).ToLower() + "/" + Base.clean(ver).ToLower();
                var oDir = oRepoContainer.GetDirectoryReference(sID);

                foreach (CloudBlockBlob oItem in oDir.ListBlobsSegmentedAsync(new BlobContinuationToken()).Result.Results)
                {
                    if (oItem.Name.ToLower().EndsWith(".json"))
                    {
                        jResult = JArray.Parse(oItem.DownloadTextAsync().Result);

                        UpdateURLs(ref jResult); //Update URL's before caching to cache the change...

                        var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(SlidingExpiration)); //cache hash for x Seconds
                        _cache.Set("mnv-" + Base.clean(man).ToLower() + Base.clean(name).ToLower() + Base.clean(ver).ToLower(), jResult, cacheEntryOptions);

                        return jResult;
                    }
                }
            }
            catch
            { }

            return new JArray();
        }

        public bool UploadSoftware(JArray Software)
        {
            try
            {
                JObject jSoftware = Software[0] as JObject;

                JToken oOut;
                string shortname = "";
                string manufacturer;
                string productname;
                string productversion;

                if (jSoftware["Shortname"] == null)
                {
                    if (jSoftware["ShortName"] != null)
                        shortname = jSoftware["ShortName"].ToString();
                }
                else
                {
                    shortname = jSoftware["Shortname"].ToString();
                }

                if(string.IsNullOrEmpty(shortname))
                    return false;

                if (!jSoftware.TryGetValue("Manufacturer", out oOut))
                    manufacturer = "_unknown";
                else
                {
                    if (string.IsNullOrEmpty(oOut.Value<string>()))
                        manufacturer = "_unknown";
                    else
                        manufacturer = Base.clean(oOut.Value<string>());
                }

                if (!jSoftware.TryGetValue("ProductName", out oOut))
                    return false;
                else
                    productname = Base.clean(oOut.Value<string>());

                if (!jSoftware.TryGetValue("ProductVersion", out oOut))
                    productversion = "";
                else
                    productversion = Base.clean(oOut.Value<string>());

                #region Icon fix
                string sIconHash = "";
                Int32 iIconID = 0;
                //Cache Icons for existing IconID and as FileHash (for the future)
                try
                {
                    JToken jIconID;
                    sIconHash = RZ.Server.Hash.CalculateMD5HashString(jSoftware["Image"].ToString());
                    //string IconsPath = Settings["icons"];
                    byte[] bIcon = jSoftware["Image"].ToObject(typeof(byte[])) as byte[];

                    CloudBlobContainer oIcoContainer = new CloudBlobContainer(new Uri(Settings["iconURL"] + "?" + Settings["iconSAS"]));
                    CloudBlockBlob cIcoBlock = oIcoContainer.GetBlockBlobReference(sIconHash + ".jpg");

                    if (!cIcoBlock.ExistsAsync().Result)
                        cIcoBlock.UploadFromStreamAsync(new MemoryStream(bIcon));

                    //Add IconID if missing for backward compatibility (V1.x)
                    if (jSoftware.TryGetValue("IconId", out jIconID))
                    {
                        iIconID = jIconID.Value<Int32>();

                        if (!string.IsNullOrEmpty(jIconID.ToString()))
                        {
                            CloudBlockBlob cIcoBlock2 = oIcoContainer.GetBlockBlobReference(jIconID.ToString() + ".jpg");

                            if (!cIcoBlock2.ExistsAsync().Result)
                                cIcoBlock2.UploadFromStreamAsync(new MemoryStream(bIcon));
                        }
                    }
                    else
                    {
                        iIconID = (int)(DateTime.Now.Ticks >> 10); //trim ticks
                        foreach (JObject jSW in Software)
                        {
                            try
                            {
                                jSW.Add("IconID", iIconID);

                                CloudBlockBlob cIcoBlock2 = oIcoContainer.GetBlockBlobReference(jIconID.ToString() + ".jpg");

                                if (!cIcoBlock2.ExistsAsync().Result)
                                    cIcoBlock2.UploadFromStreamAsync(new MemoryStream(bIcon));
                            }
                            catch { }
                        }
                    }

                    //Add IconHash for >= 2.x Versions
                    foreach (JObject jSW in Software)
                    {
                        try
                        {
                            if (jSW["IconHash"] == null)
                                jSW.Add("IconHash", sIconHash);
                            else
                                jSW["IconHash"] = sIconHash;
                        }
                        catch { }
                    }
                }
                catch { }
                #endregion


                //cleanup
                foreach (JObject jObj in Software)
                {
                    if (jObj["Author"] != null)
                        jObj.Remove("Author");
                }

                CloudBlobContainer oRepoContainer = new CloudBlobContainer(new Uri(Settings["repoURL"] + "?" + Settings["repoSAS"]));
                var oDir = oRepoContainer.GetDirectoryReference(manufacturer.ToLower() + "/" + productname.ToLower() + "/" + productversion.ToLower());
                CloudBlockBlob cRepoBlock = oDir.GetBlockBlobReference(shortname.ToLower() + ".json");

                if (!cRepoBlock.ExistsAsync().Result)
                    cRepoBlock.UploadTextAsync(Software.ToString(Newtonsoft.Json.Formatting.None));


                foreach (JObject jObj in Software)
                {
                    JObject jEntity = new JObject();
                    jEntity.Add("Manufacturer", Base.clean(jObj["Manufacturer"].ToString()));
                    jEntity.Add("ProductName", Base.clean(jObj["ProductName"].ToString()));
                    jEntity.Add("ProductVersion", Base.clean(jObj["ProductVersion"].ToString()));

                    if (jObj["ShortName"] != null)
                    {
                        jEntity.Add("ShortName", jObj["ShortName"].ToString());
                        jEntity.Add("shortname", jObj["ShortName"].ToString().ToLower());
                    }
                    else
                    {
                        jEntity.Add("ShortName", jObj["Shortname"].ToString());
                        jEntity.Add("shortname", jObj["Shortname"].ToString().ToLower());
                    }

                    if(jObj["ProductDescription"] != null)
                        jEntity.Add("Description", jObj["ProductDescription"].ToString());
                    else
                        jEntity.Add("Description", jObj["Description"].ToString());

                    if(jObj["ProjectURL"] != null)
                        jEntity.Add("ProductURL", jObj["ProjectURL"].ToString());
                    else
                        jEntity.Add("ProductURL", jObj["ProductURL"].ToString());

                    if(jObj["Category"] != null)
                        jEntity.Add("Category", jObj["Category"].ToString());
                    else
                        jEntity.Add("Category", string.Join(';', jObj["Categories"].Value<List<string>>()));

                    jEntity.Add("IsLatest", true);
                    
                    //DL, Fail and Success on Insert only
                    jEntity.Add("Downloads", 0);
                    jEntity.Add("Failures", 0);
                    jEntity.Add("Success", 0);

                    if (jEntity["IconHash"] == null)
                        jEntity.Add("IconHash", sIconHash);
                    if (jEntity["IconId"] == null)
                        jEntity.Add("IconId", iIconID);

                    string sID = (jEntity["Manufacturer"].ToString().ToLower() + jEntity["ProductName"].ToString().ToLower() + jEntity["ProductVersion"].ToString().ToLower()).Trim();
                    string sRowKey = Hash.CalculateMD5HashString(sID);

                    jEntity.Add("ModifyDate", DateTime.Now.ToUniversalTime().ToString("o"));

                    InsertEntityAsync(Settings["catURL"] + "?" + Settings["catSAS"], "known", sRowKey, jEntity.ToString(Newtonsoft.Json.Formatting.None));
                    //MergeEntityAsync(Settings["catURL"] + "?" + Settings["catSAS"], "known", sRowKey, jEntity.ToString(Newtonsoft.Json.Formatting.None));

                    break;
                }

                return true;
            }
            catch(Exception ex)
            {
                ex.Message.ToString();
            }

            return false;
        }

        public async Task<Stream> GetIcon(string shortname)
        {
            Stream bResult;
            byte[] bCache;

            //ry to get value from Memory
            if (_cache.TryGetValue("ico-" + shortname.ToLower(), out bCache))
            {
                return new MemoryStream(bCache);
            }

            JArray jFull = GetSoftwares(shortname.ToLower());
            foreach (JObject jObj in jFull)
            {
                try
                {
                    if (jObj["Image"] != null)
                    {
                        bResult = new MemoryStream(jObj["Image"].ToObject(typeof(byte[])) as byte[]);
                        MemoryStream ms = new MemoryStream();
                        bResult.CopyTo(ms);

                        var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(90)); //cache icon for 90 Minutes
                        _cache.Set("ico-" + shortname.ToLower(), ms.ToArray(), cacheEntryOptions);

                        return bResult;
                    }
                    else
                    {
                        if (jObj["IconHash"] != null)
                        {
                            bResult = await GetIcon(0, jObj["IconHash"].ToString());
                            MemoryStream ms = new MemoryStream();
                            bResult.CopyTo(ms);

                            var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(90)); //cache icon for 90 Minutes
                            _cache.Set("ico-" + shortname.ToLower(), ms.ToArray(), cacheEntryOptions);

                            return bResult;
                        }
                    }
                }
                catch { }
            }

            return null;
        }

        public async Task<Stream> GetIcon(Int32 iconid = 0, string iconhash = "")
        {
            string sico = iconhash;

            if (iconid > 0)
                sico = iconid.ToString();

            Stream bResult;
            byte[] bCache;

            //Try to get value from Memory
            if (_cache.TryGetValue("ico-" + sico, out bCache))
            {
                var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(95)); //cache icon for other 90 Minutes
                _cache.Set("ico-" + sico, bCache, cacheEntryOptions);

                return new MemoryStream(bCache);
            }

            //Try to load Icon from Disk
            if (File.Exists(Path.Combine(Settings["icons"], sico + ".jpg")))
            {

                bCache = File.ReadAllBytes(Path.Combine(Settings["icons"], sico + ".jpg"));

                var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(95)); //cache icon for other 90 Minutes
                _cache.Set("ico-" + sico, bCache, cacheEntryOptions);

                return new MemoryStream(bCache);
            }

            try
            {
                string sURL = Settings["iconURL"] + "/" + sico + ".jpg?" + Settings["iconSAS"];

                WebRequest myWebRequest = WebRequest.Create(sURL);
                WebResponse myWebResponse = myWebRequest.GetResponse();
                bResult = myWebResponse.GetResponseStream();

                MemoryStream ms = new MemoryStream();
                bResult.CopyTo(ms);
                bCache = ms.ToArray();

                var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(95)); //cache icon for 90 Minutes
                _cache.Set("ico-" + sico, bCache, cacheEntryOptions);

                try
                {
                    File.WriteAllBytes(Path.Combine(Settings["icons"], sico + ".jpg"), bCache);
                }
                catch { }

                return new MemoryStream(bCache);
            }
            catch { }

            return null;
        }

        private void UpdateURLs(ref JArray jSource)
        {
            foreach (JObject jObj in jSource)
            {
                try
                {
                    JToken oContentID;

                    //Empty PreRequisites should be handled by the Client; Fix after 1.6.2.14!
                    try
                    {
                        if (jObj["PreRequisites"] == null)
                        {
                            string[] oReq = new string[0];
                            jObj.Add("PreRequisites", JToken.FromObject(oReq));
                        }

                        if (!jObj["PreRequisites"].HasValues)
                        {
                            string[] oReq = new string[0];
                            jObj["PreRequisites"] = JToken.FromObject(oReq);
                        }
                    }
                    catch { }

                    try
                    {
                        jObj["Manufacturer"] = Base.clean(jObj["Manufacturer"].Value<string>());
                        jObj["ProductName"] = Base.clean(jObj["ProductName"].Value<string>());
                        jObj["ProductVersion"] = Base.clean(jObj["ProductVersion"].Value<string>());
                    }
                    catch { }

                    if (jObj.TryGetValue("ContentID", out oContentID))
                    {
                        string sContentID = oContentID.Value<string>();

                        CloudBlobContainer oRepoContainer = new CloudBlobContainer(new Uri(Settings["contURL"] + "?" + Settings["contSAS"]));
                        var oDir = oRepoContainer.GetDirectoryReference(sContentID);

                        List<string> FileNames = new List<string>();
                        foreach (CloudBlockBlob oItem in oDir.ListBlobsSegmentedAsync(new BlobContinuationToken()).Result.Results)
                        {
                            FileNames.Add(Path.GetFileName(oItem.Name.ToLower()));
                        }

                        foreach (JObject oFiles in jObj["Files"])
                        {
                            if (FileNames.Contains(oFiles["FileName"].Value<string>().ToLower()))
                            {
                                //oFiles["URL"] = Base.localURL + "/rest/v2/GetFile/" + sContentID + "/" + oFiles["FileName"].ToString().Replace("\\", "/");
                                oFiles["URL"] = "https://cdn.ruckzuck.tools/rest/v2/GetFile/" + sContentID + "/" + oFiles["FileName"].ToString().Replace("\\", "/");
                                
                            }
                        }
                    }
                    else
                        continue;
                }
                catch { }
            }
        }

        public async Task<Stream> GetFile(string FilePath)
        {
                string sURL = Settings["contURL"] + "/" + FilePath.Replace('\\', '/') + "?" + Settings["contSAS"];

                WebRequest myWebRequest = WebRequest.Create(sURL);
                WebResponse myWebResponse = myWebRequest.GetResponse();
                return myWebResponse.GetResponseStream();
        }

        public string GetShortname(string name = "", string ver = "", string man = "")
        {
            string sResult = "";
            string sID = (Base.clean(man.ToLower()) + Base.clean(name.ToLower()) + Base.clean(ver.ToLower())).Trim();
            string sRowKey = Hash.CalculateMD5HashString(sID);

            //Try to get value from Memory
            if (_cache.TryGetValue("lookup-" + sID, out sResult))
            {
                return sResult;
            }

            JArray jRes = getlatestSoftwareHash(Settings["catURL"] + "?" + Settings["catSAS"], sRowKey, "known");
            //JArray jRes = GetSoftwares(name.ToLower(), ver.ToLower(), man.ToLower());
            foreach(JObject jObj in jRes)
            {
                string shortname = jObj["ShortName"].ToString();

                var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(SlidingExpiration)); //cache hash for x Seconds
                _cache.Set("lookup-" + sID, shortname, cacheEntryOptions);

                return shortname;
            }
            
            return "";
        }

        private void InsertEntityAsync(string url, string PartitionKey, string RowKey, string JSON)
        {
            Task.Run(() =>
            {
                try
                {
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                    var jObj = JObject.Parse(JSON);
                    jObj.Add("PartitionKey", PartitionKey);
                    jObj.Add("RowKey", RowKey);
                    using (HttpClient oClient = new HttpClient())
                    {
                        oClient.DefaultRequestHeaders.Accept.Clear();
                        oClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                        HttpContent oCont = new StringContent(jObj.ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8, "application/json");
                        oCont.Headers.Add("x-ms-version", "2017-04-17");
                        oCont.Headers.Add("Prefer", "return-no-content");
                        oCont.Headers.Add("x-ms-date", DateTime.Now.ToUniversalTime().ToString("R"));
                        var oRes = oClient.PostAsync(url, oCont);
                        oRes.Wait();
                    }

                }
                catch { }
            });

        }

        private void UpdateEntityAsync(string url, string PartitionKey, string RowKey, string JSON)
        {

            try
            {
                string sasToken = url.Substring(url.IndexOf("?") + 1);
                string sURL = url.Substring(0, url.IndexOf("?"));

                url = sURL + "(PartitionKey='" + PartitionKey + "',RowKey='" + RowKey + "')?" + sasToken;

                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                var jObj = JObject.Parse(JSON);
                using (HttpClient oClient = new HttpClient())
                {
                    oClient.DefaultRequestHeaders.Accept.Clear();
                    oClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    oClient.DefaultRequestHeaders.Add("If-Match", "*");
                    HttpContent oCont = new StringContent(jObj.ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8, "application/json");
                    oCont.Headers.Add("x-ms-version", "2017-04-17");
                    oCont.Headers.Add("x-ms-date", DateTime.Now.ToUniversalTime().ToString("R"));
                    var oRes = oClient.PutAsync(url, oCont);
                    oRes.Wait();
                }

            }
            catch (Exception ex)
            {
                ex.Message.ToString();
            }


        }

        private void MergeEntityAsync(string url, string PartitionKey, string RowKey, string JSON, string ETag = "*")
        {
            Task.Run(() =>
            {
                try
                {
                    string sasToken = url.Substring(url.IndexOf("?") + 1);
                    string sURL = url.Substring(0, url.IndexOf("?"));

                    url = sURL + "(PartitionKey='" + PartitionKey + "',RowKey='" + RowKey + "')?" + sasToken;

                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                    var jObj = JObject.Parse(JSON);
                    using (HttpClient oClient = new HttpClient())
                    {
                        oClient.DefaultRequestHeaders.Accept.Clear();
                        oClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                        oClient.DefaultRequestHeaders.Add("If-Match", ETag);
                        HttpContent oCont = new StringContent(jObj.ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8, "application/json");
                        oCont.Headers.Add("x-ms-version", "2017-04-17");
                        oCont.Headers.Add("x-ms-date", DateTime.Now.ToUniversalTime().ToString("R"));
                        var oRes = oClient.PatchAsync(url, oCont);
                        oRes.Wait();
                    }

                }
                catch (Exception ex)
                {
                    ex.Message.ToString();
                }
            });
        }

        private string GetEntityAsync(string url, string PartitionKey, string RowKey)
        {
            Task.Run(() =>
            {
                try
                {
                    string sasToken = url.Substring(url.IndexOf("?") + 1);
                    string sURL = url.Substring(0, url.IndexOf("?"));

                    url = sURL + "(PartitionKey='" + PartitionKey + "',RowKey='" + RowKey + "')?" + sasToken;

                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                    using (HttpClient oClient = new HttpClient())
                    {
                        oClient.DefaultRequestHeaders.Accept.Clear();
                        oClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                        var oRes = oClient.GetStringAsync(url);
                        oRes.Wait();
                        oRes.Result.ToString();
                    }

                }
                catch (Exception ex)
                {
                    ex.Message.ToString();
                }
            });

            return "";
        }

        private JArray getlatestSoftware(string url, string ShortName, string Customer = "known")
        {
            try
            {
                string sasToken = url.Substring(url.IndexOf("?") + 1);
                string sURL = url.Substring(0, url.IndexOf("?"));

                var request = (HttpWebRequest)WebRequest.Create(sURL + "()?$filter=PartitionKey eq '" + Customer.ToLower() + "' and shortname eq '" + WebUtility.UrlEncode(ShortName.ToLower()) + "' and IsLatest eq true&" + sasToken);

                request.Method = "GET";
                request.Headers.Add("x-ms-version", "2017-04-17");
                request.Headers.Add("x-ms-date", DateTime.Now.ToUniversalTime().ToString("R"));
                request.Accept = "application/json;odata=nometadata";
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                var content = string.Empty;

                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    using (var stream = response.GetResponseStream())
                    {
                        using (var sr = new StreamReader(stream))
                        {
                            content = sr.ReadToEnd();
                        }
                    }
                }

                var jres = JObject.Parse(content);

                JArray jResult = jres["value"] as JArray;

                return jResult;
            }
            catch { }

            return new JArray();
        }

        private JArray getlatestSoftwareHash(string url, string rowkey, string partitionkey = "known")
        {
            try
            {
                string sasToken = url.Substring(url.IndexOf("?") + 1);
                string sURL = url.Substring(0, url.IndexOf("?"));

                var request = (HttpWebRequest)WebRequest.Create(sURL + "()?$filter=PartitionKey eq '" + partitionkey + "' and RowKey eq '" + rowkey + "'&$select=Manufacturer,ProductName,ProductVersion,ShortName,Description,ProductURL,IconId,Downloads,Category&" + sasToken);

                request.Method = "GET";
                request.Headers.Add("x-ms-version", "2017-04-17");
                request.Headers.Add("x-ms-date", DateTime.Now.ToUniversalTime().ToString("R"));
                request.Accept = "application/json;odata=nometadata";
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                var content = string.Empty;

                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    using (var stream = response.GetResponseStream())
                    {
                        using (var sr = new StreamReader(stream))
                        {
                            content = sr.ReadToEnd();
                        }
                    }
                }

                var jres = JObject.Parse(content);

                JArray jResult = jres["value"] as JArray;

                return jResult;
            }
            catch { }

            return new JArray();
        }

        public bool IncCounter(string ShortName = "", string counter = "DL", string Customer = "known")
        {
            try
            {
                string sasToken = Settings["catSAS"];
                string sURL = Settings["catURL"];

                switch (counter.ToUpper())
                {
                    case "DL":
                        try
                        {
                            var request = (HttpWebRequest)WebRequest.Create(sURL + "()?$filter=PartitionKey eq '" + Customer.ToLower() + "' and shortname eq '" + WebUtility.UrlEncode(ShortName.ToLower()) + "' and IsLatest eq true&$select=PartitionKey,RowKey,Downloads&" + sasToken);

                            request.Method = "GET";
                            request.Headers.Add("x-ms-version", "2017-04-17");
                            request.Headers.Add("x-ms-date", DateTime.Now.ToUniversalTime().ToString("R"));
                            request.Accept = "application/json";
                            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                            var content = string.Empty;
                            string ETag = "";
                            using (var response = (HttpWebResponse)request.GetResponse())
                            {
                                using (var stream = response.GetResponseStream())
                                {
                                    using (var sr = new StreamReader(stream))
                                    {
                                        content = sr.ReadToEnd();
                                    }
                                }
                            }

                            var jres = JObject.Parse(content);

                            JArray jResult = jres["value"] as JArray;
                            ETag = jResult[0]["odata.etag"].ToString();
                            string PartitionKey = jResult[0]["PartitionKey"].ToString();
                            string RowKey = jResult[0]["RowKey"].ToString();
                            int iCount = jResult[0]["Downloads"].Value<int>();
                            iCount++;
                            JObject jUpd = new JObject();
                            jUpd.Add("Downloads", iCount);

                            MergeEntityAsync(Settings["catURL"] + "?" + Settings["catSAS"], PartitionKey, RowKey, jUpd.ToString(), ETag);
                        }
                        catch { }
                        break;

                    case "FAILURE":
                        try
                        {
                            var request = (HttpWebRequest)WebRequest.Create(sURL + "()?$filter=PartitionKey eq '" + Customer.ToLower() + "' and shortname eq '" + ShortName.ToLower() + "' and IsLatest eq true&$select=PartitionKey,RowKey,Failures&" + sasToken);

                            request.Method = "GET";
                            request.Headers.Add("x-ms-version", "2017-04-17");
                            request.Headers.Add("x-ms-date", DateTime.Now.ToUniversalTime().ToString("R"));
                            request.Accept = "application/json";
                            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                            var content = string.Empty;
                            string ETag = "";
                            using (var response = (HttpWebResponse)request.GetResponse())
                            {
                                using (var stream = response.GetResponseStream())
                                {
                                    using (var sr = new StreamReader(stream))
                                    {
                                        content = sr.ReadToEnd();
                                    }
                                }
                            }

                            var jres = JObject.Parse(content);

                            JArray jResult = jres["value"] as JArray;
                            ETag = jResult[0]["odata.etag"].ToString();
                            string PartitionKey = jResult[0]["PartitionKey"].ToString();
                            string RowKey = jResult[0]["RowKey"].ToString();
                            int iCount = jResult[0]["Failures"].Value<int>();
                            iCount++;
                            JObject jUpd = new JObject();
                            jUpd.Add("Failures", iCount);

                            MergeEntityAsync(Settings["catURL"] + "?" + Settings["catSAS"], PartitionKey, RowKey, jUpd.ToString(), ETag);
                        }
                        catch { }
                        break;

                    case "SUCCESS":
                        try
                        {
                            var request = (HttpWebRequest)WebRequest.Create(sURL + "()?$filter=PartitionKey eq '" + Customer.ToLower() + "' and shortname eq '" + ShortName.ToLower() + "' and IsLatest eq true&$select=PartitionKey,RowKey,Success&" + sasToken);

                            request.Method = "GET";
                            request.Headers.Add("x-ms-version", "2017-04-17");
                            request.Headers.Add("x-ms-date", DateTime.Now.ToUniversalTime().ToString("R"));
                            request.Accept = "application/json";
                            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                            var content = string.Empty;
                            string ETag = "";
                            using (var response = (HttpWebResponse)request.GetResponse())
                            {
                                using (var stream = response.GetResponseStream())
                                {
                                    using (var sr = new StreamReader(stream))
                                    {
                                        content = sr.ReadToEnd();
                                    }
                                }
                            }

                            var jres = JObject.Parse(content);

                            JArray jResult = jres["value"] as JArray;
                            ETag = jResult[0]["odata.etag"].ToString();
                            string PartitionKey = jResult[0]["PartitionKey"].ToString();
                            string RowKey = jResult[0]["RowKey"].ToString();
                            int iCount = jResult[0]["Success"].Value<int>();
                            iCount++;
                            JObject jUpd = new JObject();
                            jUpd.Add("Success", iCount);

                            MergeEntityAsync(Settings["catURL"] + "?" + Settings["catSAS"], PartitionKey, RowKey, jUpd.ToString(), ETag);
                        }
                        catch { }
                        break;
                }
 

                return true;
            }
            catch { }

            return false;
        }

        //Upload SW and wait for approval
        public bool UploadSoftwareWaiting(JArray Software)
        {
            try
            {
                string ProductName = Base.clean(Software[0]["ProductName"].ToString()).Trim();
                string ProductVersion = Base.clean(Software[0]["ProductVersion"].ToString()).Trim();
                string Manufacturer = Base.clean(Software[0]["Manufacturer"].ToString()).Trim();

                string sIconId = DateTime.Now.Year.ToString().Substring(2, 2) + DateTime.Now.DayOfYear.ToString("D" + 3) + Convert.ToInt32(DateTime.Now.TimeOfDay.TotalMinutes).ToString();
                int iIconID = int.Parse(sIconId);

                foreach (JObject jObj in Software)
                {
                    try
                    {
                        if (jObj["SWId"] == null)
                            jObj.Add("SWId", iIconID);
                        else
                            jObj["SWId"] = iIconID;

                        if (jObj["IconId"] == null)
                            jObj.Add("IconId", iIconID);
                        else
                            jObj["IconId"] = iIconID;
                    }
                    catch { }
                }
                CloudBlobContainer oWaitContainer = new CloudBlobContainer(new Uri(Settings["waitURL"] + "?" + Settings["waitSAS"]));
                CloudBlockBlob cJsonBlock = oWaitContainer.GetBlockBlobReference(Manufacturer + "-" + ProductName+ "-" + ProductVersion + ".json");
                var upl = cJsonBlock.UploadTextAsync(Software.ToString());
                upl.Wait(5000);

                return true;
            }
            catch { }

            return false;
        }

        public List<string> GetPendingApproval()
        {
            List<string> lRes = new List<string>();
            try
            {
                CloudBlobContainer oWaitContainer = new CloudBlobContainer(new Uri(Settings["waitURL"] + "?" + Settings["waitSAS"]));
                foreach(CloudBlockBlob lItem in oWaitContainer.ListBlobsSegmentedAsync(new BlobContinuationToken()).Result.Results)
                {
                    lRes.Add(lItem.Name.Replace(Path.GetExtension(lItem.Name), ""));
                }
                
            }
            catch { }

            return lRes;
        }

        public bool Approve(string Software)
        {
            try
            {
                CloudBlobContainer oWaitContainer = new CloudBlobContainer(new Uri(Settings["waitURL"] + "?" + Settings["waitSAS"]));

                foreach (CloudBlockBlob lItem in oWaitContainer.ListBlobsSegmentedAsync(Software + ".json", new BlobContinuationToken()).Result.Results)
                {
                    string sJSON = lItem.DownloadTextAsync().Result;
                    if (sJSON.StartsWith('['))
                    {
                        JArray jSW = JArray.Parse(sJSON);

                        #region remove IsLatest on old Catalog Item
                        try
                        {
                            string shortname = "";
                            if(jSW[0]["Shortname"] != null)
                                shortname = jSW[0]["Shortname"].ToString();
                            else
                            {
                                if (jSW[0]["ShortName"] != null)
                                    shortname = jSW[0]["ShortName"].ToString();
                            }

                            if (!string.IsNullOrEmpty(shortname))
                            {
                                JArray joldSW = getlatestSoftware(Settings["catURL"] + "?" + Settings["catSAS"], shortname.ToLower(),  "known");
                                foreach(JObject jOldItem in joldSW)
                                {
                                    jOldItem["IsLatest"] = false; //disable isLatest Flag
                                    UpdateEntityAsync(Settings["catURL"] + "?" + Settings["catSAS"], jOldItem["PartitionKey"].ToString(), jOldItem["RowKey"].ToString(), jOldItem.ToString());
                                }
                            }
                        }
                        catch { }
                        #endregion

                        if (UploadSoftware(jSW))
                        {
                            var tDel = lItem.DeleteAsync();
                            tDel.Wait(5000);
                            return true;
                        }
                    }

                }
            }
            catch { }

            return false;
        }

        public bool Decline(string Software)
        {
            try
            {
                CloudBlobContainer oWaitContainer = new CloudBlobContainer(new Uri(Settings["waitURL"] + "?" + Settings["waitSAS"]));

                foreach (CloudBlockBlob lItem in oWaitContainer.ListBlobsSegmentedAsync(Software + ".json", new BlobContinuationToken()).Result.Results)
                {
                    lItem.DeleteAsync();
                }

                return true;
            }
            catch { }

            return false;
        }

        public string GetPending(string Software)
        {
            try
            {
                CloudBlobContainer oWaitContainer = new CloudBlobContainer(new Uri(Settings["waitURL"] + "?" + Settings["waitSAS"]));

                foreach (CloudBlockBlob lItem in oWaitContainer.ListBlobsSegmentedAsync(Software + ".json", new BlobContinuationToken()).Result.Results)
                {
                    string sJSON = lItem.DownloadTextAsync().Result;

                    return sJSON;
                }
            }
            catch { }

            return "";
        }
    }
}
