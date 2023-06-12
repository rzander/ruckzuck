using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json.Linq;
using RZ.Server;
using RZ.Server.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RZ.SWLookup.Plugin
{
    public class Plugin_SWLookup : ISWLookup
    {
        private IMemoryCache _cache;
        private long SlidingExpiration = 300; //5min cache for Softwares
        private HttpClient oClient = new HttpClient();
        private static HttpClient oEntitiesClient = new HttpClient()
        {
            DefaultRequestHeaders = { Accept = { new MediaTypeWithQualityHeaderValue("application/json") },  }
        };

        private HttpClient updClient = new HttpClient()
        {
            DefaultRequestHeaders = { IfMatch = { new EntityTagHeaderValue("\"*\"") }, Accept = { new MediaTypeWithQualityHeaderValue("application/json") } }
        };

        public string Name
        {
            get
            {
                return Assembly.GetExecutingAssembly().ManifestModule.Name;
            }
        }

        public System.Collections.Generic.Dictionary<string, string> Settings { get; set; }

        public bool Forward
        {
            get { return false; }
        }

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
            string swlookup = Path.Combine(wwwpath, "swlookup");

        }

        public string GetShortname(string name = "", string ver = "", string man = "", string customerid = "")
        {
            string sResult = "";
            string sID = (Base.clean(man.ToLower()) + Base.clean(name.ToLower()) + Base.clean(ver.ToLower())).Trim();
            string sRowKey = Hash.CalculateMD5HashString(sID);

            //Try to get value from Memory
            if (_cache.TryGetValue("lookup-" + sID, out sResult))
            {
                if (sResult == "-1")
                    return null;
                else
                    return sResult;
            }

            JArray jRes = getlatestSoftwareHash(Settings["lookURL"] + "?" + Settings["lookSAS"], sRowKey, "lookup");
            
            //JArray jRes = GetSoftwares(name.ToLower(), ver.ToLower(), man.ToLower());
            foreach (JObject jObj in jRes)
            {
                if (jObj["ShortName"] != null)
                {
                    string shortname = jObj["ShortName"].ToString();

                    var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(SlidingExpiration)); //cache hash for x Seconds
                    _cache.Set("lookup-" + sID, shortname, cacheEntryOptions);

                    return shortname;
                }
                else
                {
                    var cacheEntryOptions2 = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(SlidingExpiration * 2)); //cache hash for x Seconds
                    _cache.Set("lookup-" + sID, "", cacheEntryOptions2);
                    return "";
                }
            }

            var cacheEntryOptions3 = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(SlidingExpiration * 2)); //cache hash for x Seconds
            _cache.Set("lookup-" + sID, "-1", cacheEntryOptions3);
            return null;
        }

        public bool SetShortname(string name = "", string ver = "", string man = "", string shortname = "", string customerid = "")
        {
            try
            {
                string manufacturer = Server.Base.clean(man).Trim().ToLower();
                string productname = Server.Base.clean(name).Trim().ToLower();
                string productversion = Server.Base.clean(ver).Trim().ToLower();

                string sID = Hash.CalculateMD5HashString((manufacturer + productname + productversion).Trim());

                string sResult = "";

                //Try to get value from Memory
                if (_cache.TryGetValue("lookup-" + sID, out sResult))
                {
                    return true;
                }

                //Try to get value from Memory
                if (_cache.TryGetValue("setshort-" + sID, out sResult))
                {
                    if (!string.IsNullOrEmpty(sResult))
                        return true;
                }

                JObject jEntity = new JObject();
                jEntity.Add("Manufacturer", manufacturer);
                jEntity.Add("ProductName", productname);
                jEntity.Add("ProductVersion", productversion);

                if (string.IsNullOrEmpty(shortname))
                {
                    #region automatch
                    JArray jMap = getAutoMap(Settings["mapURL"] + "?" + Settings["mapSAS"]);
                    foreach (JObject jCheck in jMap)
                    {
                        bool bName = false;
                        bool bManu = false;
                        bool bVer = false;

                        try
                        {
                            if (jCheck["xProductName"] != null && !string.IsNullOrEmpty(jCheck["xProductName"].ToString()))
                            {
                                bName = Match(jCheck["xProductName"].ToString(), productname);
                            }
                            else bName = true;

                            if (jCheck["xProductVersion"] != null && !string.IsNullOrEmpty(jCheck["xProductVersion"].ToString()))
                            {
                                bVer = Match(jCheck["xProductVersion"].ToString(), productversion);
                            }
                            else bVer = true;

                            if (jCheck["xManufacturer"] != null && !string.IsNullOrEmpty(jCheck["xManufacturer"].ToString()))
                            {
                                bManu = Match(jCheck["xManufacturer"].ToString(), manufacturer);
                            }
                            else bManu = true;

                            if (bName && bManu && bVer)
                                shortname = jCheck["shortname"].ToString().ToLower();
                        }
                        catch { }
                    }


                    #endregion
                }
                shortname = shortname.Trim().ToLower();

                if (!string.IsNullOrEmpty(shortname))
                {
                    jEntity.Add("ShortName", shortname);
                }
                else
                {
                    jEntity.Add("ShortName", "");
                }

                //string sID = (manufacturer.ToLower() + productname.ToLower() + productversion.ToLower()).Trim();
                //Console.WriteLine(sID);

                string sRowKey = sID; // Hash.CalculateMD5HashString(sID);

                _ = InsertEntityAsync(Settings["lookURL"] + "?" + Settings["lookSAS"], "lookup", sRowKey, jEntity.ToString());

                //if (!string.IsNullOrEmpty(shortname))
                //    UpdateEntityAsync(Settings["lookURL"] + "?" + Settings["lookSAS"], "lookup", sRowKey, jEntity.ToString()); //only update if there is a shortname

                var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromHours(1)); //cache hash for 1 hour
                _cache.Set("setshort-" + sID, "exist", cacheEntryOptions);

                return true;
            }
            catch { }

            return false;
        }

        public async Task InsertEntityAsync(string url, string PartitionKey, string RowKey, string JSON)
        {
            await QueueInsertAsync(JSON, Settings["swqSAS"], Settings["swqURL"]);
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

                HttpContent oCont = new StringContent(jObj.ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8, "application/json");
                oCont.Headers.Add("x-ms-version", "2017-04-17");
                oCont.Headers.Add("x-ms-date", DateTime.Now.ToUniversalTime().ToString("R"));
                var oRes = updClient.PutAsync(url, oCont);
                oRes.Wait(5000);
            }
            catch (Exception ex)
            {
                ex.ToString();
                updClient = new HttpClient()
                {
                    DefaultRequestHeaders = { IfMatch = { new EntityTagHeaderValue("\"*\"") }, Accept = { new MediaTypeWithQualityHeaderValue("application/json") } }
                };
            }
        }

        private async Task QueueInsertAsync(string JSON, string sasToken, string sURL)
        {
            try
            {
                string url = $"{sURL}?timeout=10&{sasToken}";
                string body = $"<QueueMessage><MessageText>{JSON}</MessageText></QueueMessage>";
                HttpContent oCont = new StringContent(body);
                var oRes = await oClient.PostAsync(url, oCont, new System.Threading.CancellationTokenSource(10000).Token); //10s expiration
            }
            catch
            {
                oClient = new HttpClient(); 
            }
        }

        public IEnumerable<string> SWLookupItems(string filter, string customerid = "")
        {
            List<string> lResult = new List<string>();
            string sRepository = Settings["swlookup"];
            //List<string> lOut = Directory.GetFiles(sRepository, filter, SearchOption.AllDirectories).ToList();

            foreach (string sLine in Directory.EnumerateFiles(sRepository, filter, SearchOption.AllDirectories))
            {
                string sOut = "";
                try
                {
                    sOut = sLine.Replace(sRepository, "").TrimStart(new char[] { '\\', '/' });
                    if (sOut.Split(Path.DirectorySeparatorChar).Length < 4)
                    {
                        sOut = sOut.Replace(Path.GetFileName(sOut), Path.DirectorySeparatorChar + Path.GetFileName(sOut));
                    }
                }
                catch { }
                if(!string.IsNullOrEmpty(sOut))
                    yield return sOut;
            }
        }

        private JArray getlatestSoftwareHash(string url, string rowkey, string partitionkey = "known")
        {
            try
            {
                string sasToken = url.Substring(url.IndexOf("?") + 1);
                string sURL = url.Substring(0, url.IndexOf("?"));

                string uri = sURL + "()?$filter=PartitionKey eq '" + partitionkey + "' and RowKey eq '" + rowkey + "'&$select=Manufacturer,ProductName,ProductVersion,ShortName,Description,ProductURL,IconId,Downloads,Category&" + sasToken;
                return getEntities(uri);

            }
            catch { }

            return new JArray();
        }

        public static JArray getEntities(string url)
        {
            try
            {
                string nextPart = "";
                string nextRow = "";
 
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                var content = string.Empty;

                if (!oEntitiesClient.DefaultRequestHeaders.Contains("x-ms-version"))
                {
                    oEntitiesClient.DefaultRequestHeaders.Add("x-ms-version", "2017-04-17");
                }
                if (oEntitiesClient.DefaultRequestHeaders.Contains("x-ms-date"))
                {
                    oEntitiesClient.DefaultRequestHeaders.Remove("x-ms-date");
                }
                oEntitiesClient.DefaultRequestHeaders.Add("x-ms-date", DateTime.Now.ToUniversalTime().ToString("R"));

                using (var response = oEntitiesClient.GetAsync(url).Result)
                {
                    if (response.Headers.Contains("x-ms-continuation-NextPartitionKey"))
                    {
                        nextPart = response.Headers.GetValues("x-ms-continuation-NextPartitionKey").FirstOrDefault();
                    }
                    if (response.Headers.Contains("x-ms-continuation-NextRowKey"))
                    {
                        nextRow = response.Headers.GetValues("x-ms-continuation-NextRowKey").FirstOrDefault();
                    }
                    content = response.Content.ReadAsStringAsync().Result;
                }

                var jres = JObject.Parse(content);

                JArray jResult = jres["value"] as JArray;

                //Load next Page if there are more than 1000 Items...
                if (!string.IsNullOrEmpty(nextPart))
                {
                    string sNewURL = url.Split("&NextPartitionKey=")[0];
                    jResult.Merge(getEntities(sNewURL + $"&NextPartitionKey={nextPart}&NextRowKey={nextRow}"));
                }

                return jResult;
            }
            catch { }

            return null;
        }

        public static JArray getEntities_old(string url)
        {
            try
            {
                string nextPart = "";
                string nextRow = "";
                HttpWebRequest request = null;

                request = (HttpWebRequest)WebRequest.Create(url);

                request.Method = "GET";
                request.Headers.Add("x-ms-version", "2017-04-17");
                request.Headers.Add("x-ms-date", DateTime.Now.ToUniversalTime().ToString("R"));
                request.Accept = "application/json;odata=nometadata";
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                var content = string.Empty;

                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    nextPart = response.Headers["x-ms-continuation-NextPartitionKey"];
                    nextRow = response.Headers["x-ms-continuation-NextRowKey"];
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

                //Load next Page if there are more than 1000 Items...
                if (!string.IsNullOrEmpty(nextPart))
                {
                    string sNewURL = url.Split("&NextPartitionKey=")[0];
                    jResult.Merge(getEntities(sNewURL + $"&NextPartitionKey={ nextPart }&NextRowKey={ nextRow }"));
                }

                return jResult;
            }
            catch { }

            return null;
        }

        private bool Match(string regx, string input )
        {
            Regex rgx = new Regex(regx, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            return rgx.IsMatch(input);
        }

        private JArray getAutoMap(string url, string Customer = "automap")
        {
            JArray jResult = new JArray();
            //Try to get value from Memory
            if (_cache.TryGetValue("automap-" + Customer, out jResult))
            {
                return jResult;
            }

            try
            {
                string sasToken = url.Substring(url.IndexOf("?") + 1);
                string sURL = url.Substring(0, url.IndexOf("?"));

                string uri = sURL + "()?$filter=PartitionKey eq '" + Customer + "'&" + sasToken;
                jResult = getEntities(uri);

                var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(16)); //cache hash for 16 Minutes
                _cache.Set("automap-" + Customer, jResult, cacheEntryOptions);

                return jResult;
            }
            catch { }

            return new JArray();
        }

        public JArray CheckForUpdates(JArray Softwares, string customerid = "")
        {
            return new JArray();
        }
    }
}
