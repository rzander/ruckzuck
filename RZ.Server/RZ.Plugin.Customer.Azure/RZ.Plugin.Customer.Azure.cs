using Microsoft.Extensions.Caching.Memory;
using RZ.Server.Interfaces;
using System;
using System.Collections.Generic;
using System.Reflection;
using RZ.Extensions;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RZ.Plugin.Customer.Azure
{
    public class Plugin_Customer : ICustomer
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

            IP2LocationURL = Environment.GetEnvironmentVariable("IP2LocationURL") ?? "";

            string wwwpath = Settings["wwwPath"] ?? PluginPath;
        }

        public string GetURL(string customerid = "", string ip = "")
        {
            //var oLoc = GetLocAsync(ip);
            //var jLoc = JObject.Parse(oLoc.Result);
            //string sLocation = jLoc["Location"].ToString();
            //if(customerid == "81.246.0.34") //blocked 10.4.2020 (7000 Adobe Reader DC MUI failures/day)
            //    return "https://block.ruckzuck.tools"; 

            if (customerid == "swtesting")
                return "https://ruckzuck.azurewebsites.net";

            if (customerid == "itnetx")
                return "https://ruckzuck-itnetx.azurewebsites.net";

            if (customerid == "vsb")
                return "https://ruckzuck-itnetx.azurewebsites.net";

            if (customerid == "lms")
                return "https://ruckzuck-itnetx.azurewebsites.net";

            if (customerid == "sws")
                return "https://ruckzuck-itnetx.azurewebsites.net";

            if (customerid == "ewb")
                return "https://ruckzuck-itnetx.azurewebsites.net";

            if (customerid == "proxy")
                return "https://rzproxy.azurewebsites.net";

            if (customerid.Split('.').Length == 4) // if customerid is IP, use CDN as we know the source ip
                return "https://cdn.ruckzuck.tools";


            return "https://cdn.ruckzuck.tools";
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
