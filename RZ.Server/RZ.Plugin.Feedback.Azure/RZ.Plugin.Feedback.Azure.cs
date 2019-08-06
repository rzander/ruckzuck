using Microsoft.Azure.ServiceBus;
using Microsoft.Extensions.Caching.Memory;
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

namespace RZ.Plugin.Feedback.Azure
{
    public class Plugin_Feedback : IFeedback
    {
        private IMemoryCache _cache;
        static string sbconnection = "Endpoint=sb://ruckzuck.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=LtCxU2rKG6D9j/LQaqQWwkE2wU2hbV1y5RNzw8qcFlA=";
        TopicClient tcRuckZuck = new TopicClient(sbconnection, "RuckZuck", RetryPolicy.Default);

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

        public Task<bool> StoreFeedback(string name = "", string ver = "", string man = "", string shortname = "", string feedback = "", string user = "", bool? failure = null, string ip = "", string customerid = "")
        {
            var t = Task.Run(() =>
            {
                try
                {
                    Message bMSG;
                    if (feedback == "NEW Version ?!")
                    {
                        bMSG = new Message() { Label = "RuckZuck/WCF/NEW/" + name + ";" + ver, TimeToLive = new TimeSpan(24, 0, 0) };
                        failure = true;
                    }
                    else
                    {
                        if (failure == false)
                        {
                            bMSG = new Message() { Label = "RuckZuck/WCF/Feedback/success/" + name + ";" + ver, TimeToLive = new TimeSpan(24, 0, 0) };
                            //_hubContext.Clients.All.SendAsync("Append", "<li class=\"list-group-item list-group-item-success\">%tt% - success (" + name + ")</li>");
                        }
                        else
                        {
                            //_hubContext.Clients.All.SendAsync("Append", "<li class=\"list-group-item list-group-item-danger\">%tt% - failed (" + name + ")</li>");
                            bMSG = new Message() { Label = "RuckZuck/WCF/Feedback/failure/" + name + ";" + ver, TimeToLive = new TimeSpan(24, 0, 0) };
                        }
                    }

                    bMSG.UserProperties.Add("User", user);
                    bMSG.UserProperties.Add("feedback", feedback);
                    bMSG.UserProperties.Add("ProductName", name);
                    bMSG.UserProperties.Add("ProductVersion", ver);
                    bMSG.UserProperties.Add("Manufacturer", man);

                    if (!string.IsNullOrEmpty(ip))
                        bMSG.UserProperties.Add("ClientIP", ip);

                    if (!string.IsNullOrEmpty(customerid))
                        bMSG.UserProperties.Add("CustomerID", customerid);

                    tcRuckZuck.SendAsync(bMSG);

                    JObject jEntity = new JObject();
                    jEntity.Add("Manufacturer", man);
                    jEntity.Add("ProductName", name);
                    jEntity.Add("ProductVersion", ver);
                    jEntity.Add("Shortname", shortname);
                    jEntity.Add("Feedback", feedback);
                    jEntity.Add("User", user);
                    jEntity.Add("Failure", failure ?? false);

                    string manufacturer = Server.Base.clean(man).Trim();
                    string productname = Server.Base.clean(name).Trim();
                    string productversion = Server.Base.clean(ver).Trim();

                    string sID = Hash.CalculateMD5HashString((manufacturer + productname + productversion).Trim());
                    string sRowKey = Hash.CalculateMD5HashString(sID);

                    if (failure == true) //only store failures...
                    {
                        InsertEntityAsync(Settings["feedbackURL"] + "?" + Settings["feedbackSAS"], "feedback", sRowKey, jEntity.ToString());
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

        public void PushBullet(string Message, string Body)
        {
            try
            {
                using (HttpClient oClient = new HttpClient())
                {
                    oClient.DefaultRequestHeaders.Accept.Clear();
                    oClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    HttpContent oCont = new StringContent("{\"channel_tag\": \"ruckzuck\",  \"type\": \"note\", \"title\": \"" + Message + "\", \"body\": \"" + Body + "\"}", Encoding.UTF8 ,"application/json");
                    //oCont.Headers.Add("Authorization", "Bearer o.Z565qibZ52yKj3lB9yWXflKuBiVnmsiU");
                    oClient.DefaultRequestHeaders.Add("Authorization", "Bearer o.Z565qibZ52yKj3lB9yWXflKuBiVnmsiU");
                    var oRes = oClient.PostAsync("https://api.pushbullet.com/v2/pushes", oCont);
                    oRes.Wait();
                }
                /*
                WebRequest request = WebRequest.Create("https://api.pushbullet.com/v2/pushes");
                request.Method = "POST";
                request.Headers.Add("Authorization", "Bearer o.Z565qibZ52yKj3lB9yWXflKuBiVnmsiU"); //Bearer o.WP944MGzspDyIxmiT61zv7S7Lrxnnx5c
                request.ContentType = "application/json; charset=UTF-8";
                string postData =
                    "{\"channel_tag\": \"ruckzuck\",  \"type\": \"note\", \"title\": \"" + Message + "\", \"body\": \"" + Body + "\"}";
                byte[] byteArray = Encoding.UTF8.GetBytes(postData);
                request.ContentLength = byteArray.Length;
                Stream dataStream = request.GetRequestStream();
                dataStream.Write(byteArray, 0, byteArray.Length);
                dataStream.Close();*/
            }
            catch { }
        }

        public Task<bool> SendNotification(string message = "", string body = "", string customerid = "")
        {
            Task<bool> t = Task<bool>.Run(() =>
            {
                PushBullet(message, body);
                return true;
            });

            return t;
        }
    }
}
