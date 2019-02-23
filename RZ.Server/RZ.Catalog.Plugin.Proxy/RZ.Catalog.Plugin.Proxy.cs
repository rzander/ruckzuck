using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json.Linq;
using RZ.Plugin.Catlog.Proxy;
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
        string wwwpath = "";

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

            wwwpath = Settings["wwwPath"] ?? PluginPath;

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

            jResult = getCatalog("", "");

            var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(30)); //cache catalog for 30 Minutes
            _cache.Set("swcat", jResult, cacheEntryOptions);

            return jResult;
        }

        private JArray getCatalog(string url, string Customer = "")
        {
            try
            {
               return  RZRestAPIv2.GetCatalog(Customer, wwwpath);
            }
            catch { }

            return new JArray();
        }

    }
}
