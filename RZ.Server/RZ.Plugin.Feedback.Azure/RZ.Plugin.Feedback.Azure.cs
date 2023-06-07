using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json.Linq;
using RZ.Server;
using RZ.Server.Interfaces;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace RZ.Plugin.Feedback.Azure
{
    public class Plugin_Feedback : IFeedback
    {
        private IMemoryCache _cache;
        private static HttpClient insClient = new HttpClient()
        {
            DefaultRequestHeaders = { Accept = { new MediaTypeWithQualityHeaderValue("application/json") } }
        };

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


            //if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("sbconnection")))
            //{
            //    sbconnection = Environment.GetEnvironmentVariable("sbconnection");
            //}

            //if (!string.IsNullOrEmpty(sbconnection))
            //{
            //    if (tcRuckZuck == null)
            //    {
            //        Console.WriteLine("SBConnection:" + sbconnection);
            //        tcRuckZuck = new TopicClient(sbconnection, "RuckZuck", RetryPolicy.Default);
            //    }
            //}
            //else
            //    tcRuckZuck = null;

        }

        public Task<bool> StoreFeedback(string name = "", string ver = "", string man = "", string shortname = "", string feedback = "", string user = "", bool? failure = null, string ip = "", string customerid = "")
        {
            var t = Task.Run(() =>
            {
                try
                {
                    if (feedback.ToLower().Trim() != "test")
                    {
                        JObject jEntity = new JObject
                        {
                            { "Manufacturer", man },
                            { "ProductName", name },
                            { "ProductVersion", ver },
                            { "Shortname", shortname },
                            { "Feedback", feedback },
                            { "User", user },
                            { "Failure", failure ?? false }
                        };

                        string manufacturer = Server.Base.clean(man).Trim();
                        string productname = Server.Base.clean(name).Trim();
                        string productversion = Server.Base.clean(ver).Trim();

                        string sID = Hash.CalculateMD5HashString((manufacturer + productname + productversion).Trim());
                        string sRowKey = Hash.CalculateMD5HashString(sID);


                        if (failure == true) //only store failures...
                        {
                            InsertEntityAsync(Settings["feedbackURL"] + "?" + Settings["feedbackSAS"], "feedback", sRowKey, jEntity.ToString());
                        }
                    }
                    return true;
                }
                catch { }

                return false;
            });

            return t as Task<bool>;
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
                    //using (HttpClient oClient = new HttpClient())
                    //{
                    //    oClient.DefaultRequestHeaders.Accept.Clear();
                    //    oClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    //    HttpContent oCont = new StringContent(jObj.ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8, "application/json");
                    //    oCont.Headers.Add("x-ms-version", "2017-04-17");
                    //    oCont.Headers.Add("Prefer", "return-no-content");
                    //    oCont.Headers.Add("x-ms-date", DateTime.Now.ToUniversalTime().ToString("R"));
                    //    var oRes = oClient.PostAsync(url, oCont);
                    //    oRes.Wait();
                    //}

                    HttpContent oCont = new StringContent(jObj.ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8, "application/json");
                    oCont.Headers.Add("x-ms-version", "2017-04-17");
                    oCont.Headers.Add("Prefer", "return-no-content");
                    oCont.Headers.Add("x-ms-date", DateTime.Now.ToUniversalTime().ToString("R"));
                    var oRes = insClient.PostAsync(url, oCont);
                    oRes.Wait(5000);
                }
                catch {
                    insClient = new HttpClient()
                    {
                        DefaultRequestHeaders = { Accept = { new MediaTypeWithQualityHeaderValue("application/json") } }
                    };
                }
            });
        }

        public Task<bool> SendNotification(string message = "", string body = "", string customerid = "")
        {
            Task<bool> t = Task<bool>.Run(() =>
            {
                return true;
            });

            return t;
        }
    }
}
