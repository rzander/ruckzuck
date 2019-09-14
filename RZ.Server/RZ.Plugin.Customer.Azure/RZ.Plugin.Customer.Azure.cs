using Microsoft.Extensions.Caching.Memory;
using RZ.Server.Interfaces;
using System;
using System.Collections.Generic;
using System.Reflection;
using RZ.Extensions;
using System.IO;
using System.Linq;

namespace RZ.Plugin.Customer.Azure
{
    public class Plugin_Customer : ICustomer
    {
        private IMemoryCache _cache;
        private static AzureLogAnalytics AzureLog = new AzureLogAnalytics("", "", "");

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
                if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("Log-WorkspaceID")) && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("Log-SharedKey")))
                {
                    AzureLog = new AzureLogAnalytics(Environment.GetEnvironmentVariable("Log-WorkspaceID"), Environment.GetEnvironmentVariable("Log-SharedKey"), "RuckZuck");
                }
            }

            string wwwpath = Settings["wwwPath"] ?? PluginPath;

            string ipdb = Path.Combine(wwwpath, "ipdb");
            if (!Directory.Exists(ipdb))
                Directory.CreateDirectory(ipdb);

            Settings.Add("ipdb", ipdb);
        }

        /// <summary>
        /// IP2Lon from http://geekswithblogs.net/rgupta/archive/2009/04/29/convert-ip-to-long-and-vice-versa-c.aspx
        /// </summary>
        /// <param name="ip"></param>
        /// <returns></returns>
        public static long IP2Long(string ip)
        {
            string[] ipBytes;
            double num = 0;
            if (!string.IsNullOrEmpty(ip))
            {
                ipBytes = ip.Split('.');
                for (int i = ipBytes.Length - 1; i >= 0; i--)
                {
                    num += ((int.Parse(ipBytes[i]) % 256) * Math.Pow(256, (3 - i)));
                }
            }
            return (long)num;
        }

        private List<location> GetLocations(long IP)
        {
            string lIP = IP.ToString("D10");
            int iFolder = int.Parse(lIP.Substring(0, 2));
            int iPrefix = int.Parse(lIP.Substring(2, 3));
            string sFile = iPrefix.ToString("D3") + "_IPLocation.csv";
            string sPath = Settings["ipdb"];

            string sCSV = Path.Combine(sPath, iFolder.ToString("D2"), sFile);

            List<location> Loc = ReadCSV(sCSV);
            if (Loc.Count(t => t.loIP <= IP & t.hiIP >= IP) == 0)
            {
                Loc.Clear();
            }
            int iMaxLoop = 5;
            while (Loc.Count == 0 && iMaxLoop > 0)
            {
                iMaxLoop--;

                if (iPrefix >= 0)
                    iPrefix--;
                else
                {
                    iFolder--;
                    iPrefix = 999;
                }

                sCSV = Path.Combine(sPath, iFolder.ToString("D2"), iPrefix.ToString("D3") + "_IPLocation.csv");
                Loc = ReadCSV(sCSV);
                if (Loc.Count(t => t.loIP <= IP & t.hiIP >= IP) == 0)
                {
                    Loc.Clear();
                }
            }

            return Loc.Where(t => t.loIP <= IP & t.hiIP >= IP).ToList();
        }

        private static List<location> ReadCSV(string Filename)
        {
            if (File.Exists(Filename))
            {
                List<location> Locations = new List<location>();
                using (var reader = new StreamReader(Filename))
                {
                    while (!reader.EndOfStream)
                    {
                        try
                        {
                            var line = reader.ReadLine();
                            var values = line.Split("\",\"");

                            Locations.Add(new location() { loIP = long.Parse(values[0].TrimStart('"')), hiIP = long.Parse(values[1]), ISO = values[2], Country = values[3], State = values[4], Location = values[5], Long = values[7], Lat = values[6], ZIP = values[8], TimeZone = values[9].TrimEnd('"') });
                        }
                        catch { }
                    }
                }

                return Locations;

            }

            return new List<location>();
        }

        public string GetURL(string customerid = "", string ip = "")
        {
            if (customerid == "swtesting")
                return "https://ruckzuck.azurewebsites.net";

            if (customerid == "proxy")
                return "https://rzproxy.azurewebsites.net";

            if (customerid.Split('.').Length == 3) // if customerid is IP, use CDN as we know the source ip
                return "https://cdn.ruckzuck.tools";


            return "https://ruckzuck.azurewebsites.net";
        }

        class location
        {
            public long loIP;
            public long hiIP;
            public string ISO;
            public string Country;
            public string State;
            public string Location;
            public string Long;
            public string Lat;
            public string ZIP;
            public string TimeZone;
        }
    }
}
