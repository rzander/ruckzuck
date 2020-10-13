using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json.Linq;
using RZ.Server;
using RZ.Server.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace Plugin_Software
{
    public class Plugin_Software : ISoftware
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

        public JArray GetSoftwares(string shortname, string customerid = "")
        {
            JArray jResult = new JArray();
            //Try to get value from Memory
            if (_cache.TryGetValue("sn-" + shortname, out jResult))
            {
                UpdateURLs(ref jResult);

                return jResult;
            }

            string sRepository = Settings["repository"];

            if (!File.Exists(Path.Combine(sRepository, Base.clean(shortname.ToLower()) + ".json")))
                return new JArray();

            string sJson = File.ReadAllText(Path.Combine(sRepository, Base.clean(shortname.ToLower()) + ".json"));
            try
            {
                if(sJson.TrimStart().StartsWith("["))
                    jResult = JArray.Parse(sJson);
                else
                {
                    JObject jObj = JObject.Parse(sJson);

                    jResult = new JArray();
                    jResult.Add(jObj);
                }


                //fix PreRequisite issue on client when parsing json with null value prerequisite
                foreach (JObject jObj in jResult)
                {
                    JToken jPreReq;
                    if (jObj.TryGetValue("PreRequisites", out jPreReq))
                    {
                        if (string.IsNullOrEmpty(jPreReq.ToString()))
                        {
                            jObj["PreRequisites"] = new JArray();
                        }
                    }
                }

                var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(SlidingExpiration)); //cache hash for x Seconds
                _cache.Set("sn-" + shortname, jResult, cacheEntryOptions);

                UpdateURLs(ref jResult);

                return jResult;
            }
            catch { }

            try
            {
                jResult = new JArray();
                JObject oObj = JObject.Parse(sJson);
                
                //fix PreRequisite issue on client when parsing json with null value prerequisite
                JToken jPreReq;
                if (oObj.TryGetValue("PreRequisites", out jPreReq))
                {
                    if (string.IsNullOrEmpty(jPreReq.ToString()))
                    {
                        oObj["PreRequisites"] = new JArray();
                    }
                }

                jResult.Add(oObj);

                var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(SlidingExpiration)); //cache hash for x Seconds
                _cache.Set("sn-" + shortname, jResult, cacheEntryOptions);

                UpdateURLs(ref jResult);

                return jResult;
            }
            catch { }

            return new JArray();
        }

        public JArray GetSoftwares(string name = "", string ver = "", string man = "_unknown", string customerid = "")
        {
            JArray jResult = new JArray();
            //Try to get value from Memory
            if (_cache.TryGetValue("mnv-" + man+name+ver, out jResult))
            {
                UpdateURLs(ref jResult);

                return jResult;
            }

            string sRepository = Settings["repository"];

            string lookupPath = Path.Combine(sRepository, "customers", customerid, Base.clean(man.ToLower()), Base.clean(name.ToLower()), Base.clean(ver.ToLower()));

            if (Directory.Exists(lookupPath))
            {
                foreach (string sFile in Directory.GetFiles(lookupPath, "*.json"))
                {
                    string sJson = File.ReadAllText(sFile);

                    try
                    {
                        jResult = JArray.Parse(sJson);

                        //fix PreRequisite issue on client when parsing json with null value prerequisite
                        foreach (JObject jObj in jResult)
                        {
                            JToken jPreReq;
                            if (jObj.TryGetValue("PreRequisites", out jPreReq))
                            {
                                if (string.IsNullOrEmpty(jPreReq.ToString()))
                                {
                                    jObj["PreRequisites"] = new JArray();
                                }
                            }
                        }

                        var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(SlidingExpiration)); //cache hash for x Seconds
                        _cache.Set("mnv-" + man + name + ver, jResult, cacheEntryOptions);

                        UpdateURLs(ref jResult);

                        return jResult;
                    }
                    catch { }

                    try
                    {
                        jResult = new JArray();
                        JObject oObj = JObject.Parse(sJson);

                        //fix PreRequisite issue on client when parsing json with null value prerequisite
                        JToken jPreReq;
                        if (oObj.TryGetValue("PreRequisites", out jPreReq))
                        {
                            if (string.IsNullOrEmpty(jPreReq.ToString()))
                            {
                                oObj["PreRequisites"] = new JArray();
                            }
                        }

                        jResult.Add(oObj);

                        var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(SlidingExpiration)); //cache hash for x Seconds
                        _cache.Set("mnv-" + man + name + ver, jResult, cacheEntryOptions);

                        UpdateURLs(ref jResult);

                        return jResult;
                    }
                    catch { }
                }
            }

            lookupPath = Path.Combine(sRepository, Base.clean(man.ToLower()), Base.clean(name.ToLower()), Base.clean(ver.ToLower()));

            if (Directory.Exists(lookupPath))
            {
                foreach(string sFile in Directory.GetFiles(lookupPath, "*.json"))
                {
                    string sJson = File.ReadAllText(sFile);

                    try
                    {
                        jResult = JArray.Parse(sJson);

                        //fix PreRequisite issue on client when parsing json with null value prerequisite
                        foreach(JObject jObj in jResult)
                        {
                            JToken jPreReq;
                            if (jObj.TryGetValue("PreRequisites", out jPreReq))
                            {
                                if (string.IsNullOrEmpty(jPreReq.ToString()))
                                {
                                    jObj["PreRequisites"] = new JArray();
                                }
                            }
                        }

                        var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(SlidingExpiration)); //cache hash for x Seconds
                        _cache.Set("mnv-" + man + name + ver, jResult, cacheEntryOptions);

                        UpdateURLs(ref jResult);

                        return jResult;
                    }
                    catch { }

                    try
                    {
                        jResult = new JArray();
                        JObject oObj = JObject.Parse(sJson);

                        //fix PreRequisite issue on client when parsing json with null value prerequisite
                        JToken jPreReq;
                        if (oObj.TryGetValue("PreRequisites", out jPreReq))
                        {
                            if (string.IsNullOrEmpty(jPreReq.ToString()))
                            {
                                oObj["PreRequisites"] = new JArray();
                            }
                        }

                        jResult.Add(oObj);

                        var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(SlidingExpiration)); //cache hash for x Seconds
                        _cache.Set("mnv-" + man + name + ver, jResult, cacheEntryOptions);

                        UpdateURLs(ref jResult);

                        return jResult;
                    }
                    catch { }
                }
            }

            return new JArray();
        }

        public bool UploadSoftware(JArray Software, string customerid = "")
        {
            try
            {
                JObject jSoftware = Software[0] as JObject;

                JToken oOut;
                string shortname;
                string manufacturer;
                string productname;
                string productversion;

                if (!jSoftware.TryGetValue("Shortname", out oOut))
                    return false;
                else
                {
                    if (string.IsNullOrEmpty(oOut.Value<string>()))
                        return false;
                    shortname = Base.clean(oOut.Value<string>());
                }

                if (!jSoftware.TryGetValue("Manufacturer", out oOut))
                    manufacturer = "_unknown";
                else
                {
                    if (string.IsNullOrEmpty(oOut.Value<string>()))
                        manufacturer = "_unknown";
                    else
                        manufacturer = Base.clean(oOut.Value<string>());
                }

                if (!jSoftware.TryGetValue("ProductName", out oOut))
                    return false;
                else
                    productname = Base.clean(oOut.Value<string>());

                if (!jSoftware.TryGetValue("ProductVersion", out oOut))
                    productversion = "";
                else
                    productversion = Base.clean(oOut.Value<string>());

                #region Icon fix
                //Cache Icons for existing IconID and as FileHash (for the future)
                try
                {
                    JToken jIconID;
                    string sIconHash = RZ.Server.Hash.CalculateMD5HashString(jSoftware["Image"].ToString());
                    string IconsPath = Settings["icons"];
                    byte[] bIcon = jSoftware["Image"].ToObject(typeof(byte[])) as byte[];

                    if (!File.Exists(Path.Combine(IconsPath, sIconHash + ".jpg")))
                        File.WriteAllBytesAsync(Path.Combine(IconsPath, sIconHash + ".jpg"), bIcon);

                    //Add IconID if missing for backward compatibility (V1.x)
                    if (jSoftware.TryGetValue("IconId", out jIconID))
                    {
                        if (!string.IsNullOrEmpty(jIconID.ToString()))
                        {
                            if (!File.Exists(Path.Combine(IconsPath, jIconID.ToString() + ".jpg")))
                                File.WriteAllBytesAsync(Path.Combine(IconsPath, jIconID.ToString() + ".jpg"), bIcon);
                        }
                    }
                    else
                    {
                        Int32 iIconID = (int)(DateTime.Now.Ticks >> 10); //trim ticks
                        foreach (JObject jSW in Software)
                        {
                            try
                            {
                                jSW.Add("IconID", iIconID);
                            }
                            catch { }
                        }
                    }

                    //Add IconHash for >= 2.x Versions
                    foreach (JObject jSW in Software)
                    {
                        try
                        {
                            if (jSW["IconHash"] == null)
                                jSW.Add("IconHash", sIconHash);
                            else
                                jSW["IconHash"] = sIconHash;
                        }
                        catch { }
                    }
                }
                catch { }
                #endregion

                string sRepository = Settings["repository"];

                string sArchivePath = Path.Combine(sRepository, manufacturer, productname, productversion);

                if (!Directory.Exists(sArchivePath))
                    Directory.CreateDirectory(sArchivePath.Trim());

                File.WriteAllText(Path.Combine(sArchivePath, shortname + ".json"), Software.ToString());

                jSoftware.Property("Image").Remove(); //remove Image to reduce size and speedup read..
                File.WriteAllText((Path.Combine(sRepository, shortname + ".json")), Software.ToString());



                return true;
            }
            catch(Exception ex)
            {
                ex.Message.ToString();
            }

            return false;
        }

        public async Task<Stream> GetIcon(string shortname, string customerid = "", int size = 0)
        {
            Stream bResult;
            byte[] bCache;

            //Try to get value from Memory
            if (_cache.TryGetValue("ico-" + shortname, out bCache))
            {
                return new MemoryStream(bCache);
            }

            JArray jFull = GetSoftwares(shortname, customerid);
            foreach (JObject jObj in jFull)
            {
                try
                {
                    if (jObj["Image"] != null)
                    {
                        bResult = new MemoryStream(jObj["Image"].ToObject(typeof(byte[])) as byte[]);
                        MemoryStream ms = new MemoryStream();
                        bResult.CopyTo(ms);

                        var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(90)); //cache icon for 90 Minutes
                        _cache.Set("ico-" + shortname.ToLower(), ms.ToArray(), cacheEntryOptions);

                        return bResult;
                    }
                    else
                    {
                        bResult = await GetIcon(0, jObj["IconHash"].ToString());
                        MemoryStream ms = new MemoryStream();
                        bResult.CopyTo(ms);

                        var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(90)); //cache icon for 90 Minutes
                        _cache.Set("ico-" + shortname.ToLower(), ms.ToArray(), cacheEntryOptions);

                        return bResult;
                    }

                    //var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(60)); //cache catalog for 30 Minutes
                    //_cache.Set("ico-" + shortname, bResult, cacheEntryOptions);

                    //return bResult;
                }
                catch { }
            }

            return null;
        }

        public async Task<Stream> GetIcon(Int32 iconid = 0, string iconhash = "", string customerid = "", int size = 0)
        {
            string sico = iconhash;

            if (iconid > 0)
                sico = iconid.ToString();

            Stream bResult;
            byte[] bCache;

            //Try to get value from Memory
            if (_cache.TryGetValue("ico-" + sico, out bCache))
            {
                var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(95)); //cache icon for other 90 Minutes
                _cache.Set("ico-" + sico, bCache, cacheEntryOptions);

                return new MemoryStream(bCache);
            }

            string IconsPath = Settings["icons"];
            if (File.Exists(Path.Combine(IconsPath, sico + ".jpg")))
            {
                bCache = File.ReadAllBytes(Path.Combine(IconsPath, sico + ".jpg"));

                var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(95)); //cache icon for other 90 Minutes
                _cache.Set("ico-" + sico, bCache, cacheEntryOptions);

                return new MemoryStream(bCache);
            }

            return null;
        }

        private void UpdateURLs(ref JArray jSource)
        {
            foreach (JObject jObj in jSource)
            {
                JToken oContentID;

                if (jObj.TryGetValue("ContentID", out oContentID))
                {
                    string sContentID = oContentID.Value<string>();
                    string sContentPath = Path.Combine(Settings["content"], sContentID);

                    //Check if content directory exists
                    if (!Directory.Exists(sContentPath))
                        continue;

                    foreach (JObject oFiles in jObj["Files"])
                    {
                        if (File.Exists(Path.Combine(sContentPath, oFiles["FileName"].Value<string>())))
                        {
                            string sBase = Base.localURL;
                            if (Environment.GetEnvironmentVariable("localURL") != null)
                                sBase = Environment.GetEnvironmentVariable("localURL"); //If hosted in a container, the localURL represensts the server URL

                            oFiles["URL"] = sBase + "/rest/v2/GetFile/"  + oContentID + "/" + oFiles["FileName"].ToString().Replace("\\", "/");
                        }
                    }
                }
                else
                    continue;
            }
        }

        public async Task<IActionResult> GetFile(string FilePath, string customerid = "")
        {
            string sFullPath = Path.Combine(Settings["content"], FilePath);
            if(File.Exists(sFullPath))
            {
                //var memory = new MemoryStream();
                //using (var stream = new FileStream(sFullPath, FileMode.Open,FileAccess.Read, FileShare.Read))
                //{
                //    await stream.CopyToAsync(memory);
                //}
                //memory.Position = 0;

                return new FileStreamResult(new FileStream(sFullPath, FileMode.Open, FileAccess.Read, FileShare.Read), new MediaTypeHeaderValue("application/octet-stream"));
            }

            return null;
        }

        public string GetShortname(string name = "", string ver = "", string man = "", string customerid = "")
        {
            string sResult = "";
            //Try to get value from Memory
            if (_cache.TryGetValue("lookup-" + man + name + ver, out sResult))
            {
                return sResult;
            }

            string sRepository = Settings["repository"];

            string lookupPath = Path.Combine(sRepository, Base.clean(man), Base.clean(name), Base.clean(ver));

            if (Directory.Exists(lookupPath))
            {
                foreach (string sFile in Directory.GetFiles(lookupPath, "*.json"))
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
            }

            return "";
        }

        public bool UploadSoftwareWaiting(JArray Software, string customerid = "")
        {
            throw new NotImplementedException();
        }

        public List<string> GetPendingApproval(string customerid = "")
        {
            throw new NotImplementedException();
        }

        public bool Approve(string Software, string customerid = "")
        {
            throw new NotImplementedException();
        }

        public bool Decline(string Software, string customerid = "")
        {
            throw new NotImplementedException();
        }

        public string GetPending(string Software, string customerid = "")
        {
            throw new NotImplementedException();
        }

        public bool IncCounter(string shortname = "", string counter = "", string customerid = "")
        {
            throw new NotImplementedException();
        }
    }
}
