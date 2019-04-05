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
                else return "";
            }

            return null;
        }

        public bool SetShortname(string name = "", string ver = "", string man = "", string shortname = "", string customerid = "")
        {
            try
            {
                string manufacturer = Server.Base.clean(man).Trim();
                string productname = Server.Base.clean(name).Trim();
                string productversion = Server.Base.clean(ver).Trim();

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

                InsertEntityAsync(Settings["lookURL"] + "?" + Settings["lookSAS"], "lookup", sRowKey, jEntity.ToString());

                if (!string.IsNullOrEmpty(shortname))
                    UpdateEntityAsync(Settings["lookURL"] + "?" + Settings["lookSAS"], "lookup", sRowKey, jEntity.ToString()); //only update if there is a shortname

                var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromHours(1)); //cache hash for 1 hour
                _cache.Set("setshort-" + sID, "exist", cacheEntryOptions);

                return true;
            }
            catch { }

            return false;
        }

        public void InsertEntityAsync(string url, string PartitionKey, string RowKey, string JSON)
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

        public void UpdateEntityAsync(string url, string PartitionKey, string RowKey, string JSON)
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
                    //jObj.Add("PartitionKey", PartitionKey);
                    //jObj.Add("RowKey", RowKey);
                    using (HttpClient oClient = new HttpClient())
                    {
                        oClient.DefaultRequestHeaders.Accept.Clear();
                        oClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                        //oClient.DefaultRequestHeaders.Add("If-Match", "*");
                        HttpContent oCont = new StringContent(jObj.ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8, "application/json");
                        oCont.Headers.Add("x-ms-version", "2017-04-17");
                        //oCont.Headers.Add("Prefer", "return-no-content");
                        //oCont.Headers.Add("If-Match", "*");
                        oCont.Headers.Add("x-ms-date", DateTime.Now.ToUniversalTime().ToString("R"));
                        var oRes = oClient.PutAsync(url, oCont);
                        oRes.Wait();
                    }

                }
                catch (Exception ex)
                {
                    ex.Message.ToString();
                }
            });
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

        private JArray getlatestSoftware(string url, string ShortName, string Customer = "known")
        {
            try
            {
                string sasToken = url.Substring(url.IndexOf("?") + 1);
                string sURL = url.Substring(0, url.IndexOf("?"));

                var request = (HttpWebRequest)WebRequest.Create(sURL + "()?$filter=PartitionKey eq '" + Customer.ToLower() + "' and shortname eq '" + WebUtility.UrlEncode(ShortName.ToLower()) + "' and IsLatest eq true&$select=Manufacturer,ProductName,ProductVersion,ShortName,Description,ProductURL,IconId,Downloads,Category&" + sasToken);

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
                DateTime dStart = DateTime.Now;
                string sasToken = url.Substring(url.IndexOf("?") + 1);
                string sURL = url.Substring(0, url.IndexOf("?"));

                var request = (HttpWebRequest)WebRequest.Create(sURL + "()?$filter=PartitionKey eq '" + partitionkey + "' and RowKey eq '" + rowkey + "'&$select=ShortName&" + sasToken);

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

                TimeSpan tDur = DateTime.Now - dStart;
                tDur.TotalMilliseconds.ToString();

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

                var request = (HttpWebRequest)WebRequest.Create(sURL + "()?$filter=PartitionKey eq '" + Customer + "'&" + sasToken);

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

                jResult = jres["value"] as JArray;

                var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(15)); //cache hash for 15 Minutes
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
