using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json.Linq;
using RZ.Extensions;
using RZ.Server.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;

namespace RZ.Plugin.Feedback.Azure
{
    public class Plugin_Log : ILog
    {
        private IMemoryCache _cache;
        private static AzureLogAnalytics AzureLog = new AzureLogAnalytics("", "", "");
        private static string IP2LocationURL = "";
        private static readonly HttpClient client = new HttpClient();

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

            if (string.IsNullOrEmpty(AzureLog.WorkspaceId))
            {
                if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LogWorkspaceID")) && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LogSharedKey")))
                {
                    AzureLog = new AzureLogAnalytics(Environment.GetEnvironmentVariable("LogWorkspaceID"), Environment.GetEnvironmentVariable("LogSharedKey"), "RuckZuck");
                }
            }

            string wwwpath = Settings["wwwPath"] ?? PluginPath;

            IP2LocationURL = Environment.GetEnvironmentVariable("IP2LocationURL") ?? "";
        }

        public void WriteLog(string Text, string clientip, int EventId = 0, string customerid = "")
        {
            AzureLog.WorkspaceId.ToString();
            if (!string.IsNullOrEmpty(AzureLog.WorkspaceId))
            {
                Task.Run(() =>
                {
                    try
                    {
                        string sourceIP = clientip;
                        //if customerid is an IP use this instead of clientip because of CDN
                        if(customerid.Count(f => f ==  '.') == 3)
                        {
                            sourceIP = customerid;
                        }
                        bool bCached = false;
                        _cache.TryGetValue(sourceIP, out string Loc);

                        if(string.IsNullOrEmpty(Loc))
                        {
                            Loc = GetLocAsync(sourceIP.Trim()).Result;
                        } else
                        {
                            bCached = true;
                        }


                        if(!string.IsNullOrEmpty(Loc))
                        {
                            if(!bCached)
                            {
                                var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(300)); //cache hash for x Seconds
                                _cache.Set(sourceIP, Loc, cacheEntryOptions);
                            }
                            
                            var jLoc = JObject.Parse(Loc);
                            string sLocation = jLoc["Location"].ToString();

                            AzureLog.Post(new { Computer = clientip, EventID = EventId, CustomerID = customerid, Description = Text, Country = jLoc["Country"].ToString(), State = jLoc["State"].ToString(), Location = jLoc["Location"].ToString(), Long = jLoc["Long"].ToString(), Lat = jLoc["Lat"].ToString() });

                        }
                        else
                        {
                            AzureLog.Post(new { Computer = clientip, EventID = EventId, CustomerID = customerid, Description = Text });
                        }
                    }
                    catch { }
                });
            }
        }

        private static async Task<string> GetLocAsync(string IP)
        {
            if (!string.IsNullOrEmpty(IP2LocationURL))
            {
                var stringTask = client.GetStringAsync($"{IP2LocationURL}?ip={IP}");
                var loc = await stringTask;

                return loc;
            }

            return "";
        }
    }
}
