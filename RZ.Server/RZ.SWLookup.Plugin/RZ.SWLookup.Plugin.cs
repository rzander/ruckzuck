using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json.Linq;
using RZ.Server;
using RZ.Server.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

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

        public void Init(string PluginPath)
        {
            //Check if MemoryCache is initialized
            if (_cache == null)
            {
                _cache = new MemoryCache(new MemoryCacheOptions());
            }

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
            get { return false; }
        }

        public string GetShortname(string name = "", string ver = "", string man = "")
        {
            string sResult = "";
            //Try to get value from Memory
            if (_cache.TryGetValue("lookup-" + man + name + ver, out sResult))
            {
                return sResult;
            }

            string sRepository = Settings["swlookup"];

            string lookupPath = Path.Combine(sRepository, Base.clean(man), Base.clean(name), Base.clean(ver));

            if (Directory.Exists(lookupPath))
            {
                foreach (string sFile in Directory.GetFiles(lookupPath, "*.json", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        string shortname = (Path.GetFileName(sFile).Replace(Path.GetExtension(sFile), ""));

                        var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(SlidingExpiration)); //cache hash for x Seconds
                        _cache.Set("lookup-" + man + name + ver, shortname, cacheEntryOptions);

                        return shortname;
                    }
                    catch { }
                }

                //retry if shortnane is defined in the productname folder
                foreach (string sFile in Directory.GetFiles(Path.Combine(sRepository, Base.clean(man), Base.clean(name)), "*.json", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        string shortname = (Path.GetFileName(sFile).Replace(Path.GetExtension(sFile), ""));

                        var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(SlidingExpiration)); //cache hash for x Seconds
                        _cache.Set("lookup-" + man + name + ver, shortname, cacheEntryOptions);

                        return shortname;
                    }
                    catch { }
                }

                //File not Found, create it...
                File.WriteAllText(Path.Combine(lookupPath, "unknown" + ".nop"), ".");
            }
            else
            {
                try
                {
                    Directory.CreateDirectory(lookupPath);
                    File.WriteAllText(Path.Combine(lookupPath, "unknown" + ".nop"), ".");
                }
                catch { }

            }

            return "";
        }

        public bool SetShortname(string name = "", string ver = "", string man = "", string shortname = "")
        {
            try
            {
                string manufacturer = Server.Base.clean(man);
                string productname = Server.Base.clean(name);
                string productversion = Server.Base.clean(ver);

                string sLookupPath = Settings["swlookup"];

                string sArchivePath = Path.Combine(sLookupPath, manufacturer, productname, productversion);

                if (!Directory.Exists(sArchivePath))
                    Directory.CreateDirectory(sArchivePath);

                if (!string.IsNullOrEmpty(shortname))
                {
                    File.WriteAllText(Path.Combine(sArchivePath, shortname + ".json"), ".");
                }


                return true;
            }
            catch { }

            return false;
        }


        public IEnumerable<string> SWLookupItems(string filter)
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

        public JArray CheckForUpdates(JArray Softwares)
        {
            return new JArray();
        }
    }
}
