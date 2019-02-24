using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json.Linq;
using RZ.Server;
using RZ.Server.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace RZ.SWLookup.Plugin
{
    public class Plugin_SWLookup : ISWLookup
    {
        private IMemoryCache _cache;
        private long SlidingExpiration = 900; //15min cache for Softwares

        public string Name
        {
            get
            {
                return Assembly.GetExecutingAssembly().ManifestModule.Name;
            }
        }

        public System.Collections.Generic.Dictionary<string, string> Settings { get; set; }

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
            if (!Directory.Exists(swlookup))
                Directory.CreateDirectory(swlookup);

            Settings.Add("swlookup", swlookup);

        }

        public bool Forward
        {
            get { return true; }
        }


        public string GetShortname(string name = "", string ver = "", string man = "")
        {
            return "";
        }

        public bool SetShortname(string name = "", string ver = "", string man = "", string shortname = "")
        {
              return false;
        }


        public IEnumerable<string> SWLookupItems(string filter)
        {
            return null;
        }

        public JArray CheckForUpdates(JArray Softwares)
        {
             JArray jRes = RZRestAPIv2.CheckForUpdates(Softwares);
            Console.WriteLine("CheckForUpdates:" + jRes.Count.ToString() + " updates detected.");
            return jRes;
        }
    }
}
