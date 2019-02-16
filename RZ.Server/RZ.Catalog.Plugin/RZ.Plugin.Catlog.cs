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

namespace RZ.Plugin.Catlog.Azure
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
            if (_cache == null)
            {
                _cache = new MemoryCache(new MemoryCacheOptions());
            }

            if (Settings == null)
                Settings = new Dictionary<string, string>();

            string wwwpath = Settings["wwwPath"] ?? PluginPath;
            string repository = Path.Combine(wwwpath, "repository");
            if (!Directory.Exists(repository))
                Directory.CreateDirectory(repository);

            string content = Path.Combine(wwwpath, "content");
            if (!Directory.Exists(content))
                Directory.CreateDirectory(content);

            string icons = Path.Combine(wwwpath, "icons");
            if (!Directory.Exists(icons))
                Directory.CreateDirectory(icons);

            Settings.Add("repository", repository);
            Settings.Add("content", content);
            Settings.Add("icons", icons);

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

            string sRepository = Settings["repository"];
            foreach (string sFile in Directory.GetFiles(sRepository, "*.json"))
            {
                try
                {
                    string shortname = Base.clean(new FileInfo(sFile).Name.Split(".json")[0]);

                    JArray jFull = new JArray();
                    string sJson = File.ReadAllText(Path.Combine(sRepository, Base.clean(shortname) + ".json"));
                    if (sJson.TrimStart().StartsWith('['))
                        jFull = JArray.Parse(sJson);
                    else
                    {
                        jFull.Add(JObject.Parse(sJson));
                    }

                    foreach (JObject jSW in jFull)
                    {
                        try
                        {
                            JObject oCatItem = new JObject();
                            oCatItem.Add("Shortname", jSW["Shortname"]);
                            oCatItem.Add("ShortName", jSW["Shortname"]);
                            oCatItem.Add("Description", jSW["Description"]);
                            oCatItem.Add("Manufacturer", jSW["Manufacturer"]);
                            oCatItem.Add("ProductName", jSW["ProductName"]);
                            oCatItem.Add("ProductVersion", jSW["ProductVersion"]);
                            oCatItem.Add("ProductURL", jSW["ProductURL"]);

                            JArray jCategories = JArray.FromObject(new string[] { "Other" });
                            try
                            {
                                if (!string.IsNullOrEmpty(jSW["Category"].ToString()))
                                    jCategories = JArray.FromObject(jSW["Category"].Value<string>().Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries));
                            }
                            catch { }
                            oCatItem.Add("Categories", jCategories);

                            oCatItem.Add("Downloads", 0);

                            if (jSW["SWId"] != null)
                            {
                                if (!string.IsNullOrEmpty(jSW["SWId"].ToString()))
                                {
                                    oCatItem.Add("SWId", jSW["SWId"].Value<Int32>());
                                    oCatItem.Add("IconId", jSW["SWId"].Value<Int32>()); //for old Apps
                                }

                            }
                            else //not needed after ruckzuck.exe 1.6.2.11
                            {
                                if (jSW["IconId"] != null)
                                {
                                    if (!string.IsNullOrEmpty(jSW["IconId"].ToString()))
                                    {
                                        oCatItem.Add("SWId", jSW["IconId"].Value<Int32>());
                                        oCatItem.Add("IconId", jSW["IconId"].Value<Int32>()); //for old Apps
                                    }

                                }
                            }

                            if(jSW["IconHash"] == null)
                            {
                                string sIconHash = RZ.Server.Hash.CalculateMD5HashString(jSW["Image"].ToString());
                                //string IconsPath = Settings["icons"];
                                byte[] bIcon = jSW["Image"].ToObject(typeof(byte[])) as byte[];
                                if (!File.Exists(Path.Combine(Settings["icons"], sIconHash + ".jpg")))
                                {
                                    File.WriteAllBytes(Path.Combine(Settings["icons"], sIconHash + ".jpg"), bIcon);
                                }
                                
                                jSW["IconHash"] = sIconHash;
                            }

                            if (!string.IsNullOrEmpty(jSW["IconHash"].ToString()))
                                oCatItem.Add("IconHash", jSW["IconHash"].ToString());

                            jResult.Add(oCatItem);
                            break; //skip other architectures or languages
                        }
                        catch (Exception ex)
                        {
                            ex.Message.ToString();
                        }
                    }
                }
                catch (Exception ex)
                {
                    ex.Message.ToString();
                }

            }

            var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(30)); //cache catalog for 30 Minutes
            _cache.Set("swcat", jResult, cacheEntryOptions);

            return jResult;
        }



    }
}
