using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json.Linq;
using RZ.Server;
using RZ.Server.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;

namespace Plugin_Software
{
    public class Plugin_Software : ICatalog
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

        }


        public JArray GetCatalog(string customerid = "", bool nocache = false)
        {
            JArray jResult = new JArray();

            if (!nocache) //skip cache ?!
            {
                //Try to get value from Memory
                if (_cache.TryGetValue("swcat", out jResult))
                {
                    return jResult;
                }
            }

            jResult = new JArray();

            jResult = getCatalog(Settings["catURL"] + "?" + Settings["catSAS"], customerid);

            var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(30)); //cache catalog for 30 Minutes
            _cache.Set("swcat", jResult, cacheEntryOptions);

            return jResult;
        }

        private JArray getCatalog(string url, string Customer = "known")
        {
            try
            {
                if (string.IsNullOrEmpty(Customer))
                    Customer = "known";

                string sasToken = url.Substring(url.IndexOf("?") + 1);
                string sURL = url.Substring(0, url.IndexOf("?"));

                var request = (HttpWebRequest)WebRequest.Create(sURL + "()?$filter=PartitionKey eq '" + Customer +"' and IsLatest eq true&$select=Manufacturer,ProductName,ProductVersion,ShortName,Description,ProductURL,IconId,Downloads,Category,IconHash,ModifyDate,Timestamp&" + sasToken);

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

                foreach (JObject jItem in jResult)
                {
                    if (!string.IsNullOrEmpty(jItem["Category"].Value<string>()))
                    {
                        jItem.Add("Categories", JArray.FromObject(jItem["Category"].Value<string>().Split(new char[] { ';', ',' }).ToList()));
                    }
                    else
                    {
                        jItem.Add("Categories", JArray.FromObject(new string[] { "Other" }));
                    }

                    jItem.Remove("Category");

                    if (jItem["SWId"] == null)
                    {
                        jItem.Add("SWId", jItem["IconId"]);
                    }

                    if (jItem["Downloads"] == null)
                    {
                        jItem.Add("Downloads", 0);
                    }

                    if (jItem["IconHash"] == null)
                        jItem.Add("IconHash", "");

                    jItem.Add("Image", null);
                    jItem.Add("Quality", 100);

                    //Backward compatibility; remove after 1.6.2.13
                    //try
                    //{
                    //    jItem.Add("Shortname", jItem["ShortName"]);
                    //}
                    //catch { }
                }

                return jResult;
            }
            catch { }

            return new JArray();
        }

    }
}
