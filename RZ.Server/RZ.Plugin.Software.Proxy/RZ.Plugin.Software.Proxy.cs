using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json.Linq;
using RZ.Server;
using RZ.Server.Interfaces;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;

namespace Plugin_Software
{
    public class Plugin_Software : ISoftware
    {
        private IMemoryCache _cache;
        private long SlidingExpiration = 1800; //30min cache for Softwares
        private static HttpClient Client { get; } = new HttpClient();

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
            if (_cache.TryGetValue("sn-" + shortname.ToLower(), out jResult))
            {
                return jResult;
            }

            jResult = RZRestAPIv2.GetSoftwares(shortname, customerid);

            if(jResult.Count > 0)
            {
                var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(SlidingExpiration)); //cache hash for x Seconds
                _cache.Set("sn-" + shortname.ToLower(), jResult, cacheEntryOptions);

                return jResult;
            }

            return new JArray();
        }

        public JArray GetSoftwares(string name = "", string ver = "", string man = "_unknown", string customerid = "")
        {
            JArray jResult = new JArray();

            try
            {
                if (string.IsNullOrEmpty(man))
                    man = "_unknown";

                //Try to get value from Memory
                if (_cache.TryGetValue("mnv-" + Base.clean(man).ToLower() + Base.clean(name).ToLower() + Base.clean(ver).ToLower(), out jResult))
                {
                    return jResult;
                }

                jResult = RZRestAPIv2.GetSoftwares(name, ver, man, customerid);

                if (jResult.Count > 0)
                {
                    UpdateURLs(ref jResult); //Update URL's before caching to cache the change...

                    var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(SlidingExpiration)); //cache hash for x Seconds
                    _cache.Set("mnv-" + Base.clean(man).ToLower() + Base.clean(name).ToLower() + Base.clean(ver).ToLower(), jResult, cacheEntryOptions);

                    return jResult;
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine("ERROR: GetSoftwares - " + ex.Message);
            }

            return new JArray();
        }

        public bool UploadSoftware(JArray Software, string customerid = "")
        {
            return false;
        }

        public async Task<Stream> GetIcon(string shortname, string customerid = "", int size = 0)
        {
            Stream bResult;
            byte[] bCache;

            //ry to get value from Memory
            if (_cache.TryGetValue("ico-" + size.ToString() + shortname.ToLower(), out bCache))
            {
                return new MemoryStream(bCache);
            }

            JArray jFull = GetSoftwares(shortname.ToLower(), customerid);
            foreach (JObject jObj in jFull)
            {
                try
                {
                    if (jObj["Image"] != null)
                    {
                        bResult = new MemoryStream(jObj["Image"].ToObject(typeof(byte[])) as byte[]);
                        MemoryStream ms = new MemoryStream();
                        bResult.CopyTo(ms);
                        bCache = ms.ToArray();

                        if (size > 0)
                        {
                            using (Image image = Image.Load(bCache))
                            {
                                image.Mutate(i => i.Resize(new ResizeOptions { Size = new SixLabors.Primitives.Size(size, size) }));
                                using (var imgs = new MemoryStream())
                                {
                                    var imageEncoder = image.GetConfiguration().ImageFormatsManager.FindEncoder(PngFormat.Instance);
                                    image.Save(imgs, imageEncoder);
                                    bCache = imgs.ToArray();
                                }
                            }
                        }

                        var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(90)); //cache icon for 90 Minutes
                        _cache.Set("ico-" + size.ToString() + shortname.ToLower(), bCache, cacheEntryOptions);

                        return new MemoryStream(bCache);
                    }
                    else
                    {
                        if (jObj["IconHash"] != null)
                        {
                            bResult = await GetIcon(0, jObj["IconHash"].ToString(), customerid, size);
                            MemoryStream ms = new MemoryStream();
                            bResult.CopyTo(ms);
                            bCache = ms.ToArray();

                            if (size > 0)
                            {
                                using (Image image = Image.Load(bCache))
                                {
                                    image.Mutate(i => i.Resize(new ResizeOptions { Size = new SixLabors.Primitives.Size(size, size) }));
                                    using (var imgs = new MemoryStream())
                                    {
                                        var imageEncoder = image.GetConfiguration().ImageFormatsManager.FindEncoder(PngFormat.Instance);
                                        image.Save(imgs, imageEncoder);
                                        bCache = imgs.ToArray();
                                    }
                                }
                            }

                            var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(90)); //cache icon for 90 Minutes
                            _cache.Set("ico-" + size.ToString() + shortname.ToLower(), bCache, cacheEntryOptions);

                            return new MemoryStream(bCache);
                        }
                    }
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

            //Stream bResult;
            byte[] bCache;

            //Try to get value from Memory
            if (_cache.TryGetValue("ico-" + size.ToString() + sico, out bCache))
            {
                var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(95)); //cache icon for other 90 Minutes
                _cache.Set("ico-" + size.ToString() + sico, bCache, cacheEntryOptions);

                return new MemoryStream(bCache);
            }

            //Try to load Icon from Disk
            if (File.Exists(Path.Combine(Settings["icons"], sico + "_" + size.ToString() + ".jpg")))
            {

                bCache = File.ReadAllBytes(Path.Combine(Settings["icons"], sico + "_" + size.ToString() + ".jpg"));

                var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(95)); //cache icon for other 90 Minutes
                _cache.Set("ico-" + size.ToString() + sico, bCache, cacheEntryOptions);

                return new MemoryStream(bCache);
            }

            try
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    Stream x = await RZRestAPIv2.GetIcon(iconid, iconhash, size);
                    await x.CopyToAsync(ms);
                    bCache = ms.ToArray();

                    if (size > 0)
                    {
                        using (Image image = Image.Load(bCache))
                        {
                            image.Mutate(i => i.Resize(new ResizeOptions { Size = new SixLabors.Primitives.Size(size, size) }));
                            using (var imgs = new MemoryStream())
                            {
                                var imageEncoder = image.GetConfiguration().ImageFormatsManager.FindEncoder(PngFormat.Instance);
                                image.Save(imgs, imageEncoder);
                                bCache = imgs.ToArray();
                            }
                        }
                    }
                }

                if (bCache.Length > 0)
                {

                    var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(95)); //cache icon for 90 Minutes
                    _cache.Set("ico-" + size.ToString() + sico, bCache, cacheEntryOptions);

                    try
                    {
                        File.WriteAllBytes(Path.Combine(Settings["icons"], sico + "_" + size.ToString() + ".jpg"), bCache);
                    }
                    catch { }
                }

                return new MemoryStream(bCache);
            }
            catch { }

            return null;
        }

        private void UpdateURLs(ref JArray jSource)
        {
            foreach (JObject jObj in jSource)
            {
                try
                {
                    JToken oContentID;

                    //Empty PreRequisites should be handled by the Client; Fix after 1.6.2.14!
                    try
                    {
                        if (jObj["PreRequisites"] == null)
                        {
                            string[] oReq = new string[0];
                            jObj.Add("PreRequisites", JToken.FromObject(oReq));
                        }

                        if (!jObj["PreRequisites"].HasValues)
                        {
                            string[] oReq = new string[0];
                            jObj["PreRequisites"] = JToken.FromObject(oReq);
                        }
                    }
                    catch { }

                    try
                    {
                        jObj["Manufacturer"] = Base.clean(jObj["Manufacturer"].Value<string>());
                        jObj["ProductName"] = Base.clean(jObj["ProductName"].Value<string>());
                        jObj["ProductVersion"] = Base.clean(jObj["ProductVersion"].Value<string>());
                    }
                    catch { }

                    if (jObj.TryGetValue("ContentID", out oContentID))
                    {
                        string sContentID = oContentID.Value<string>();

                        List<string> FileNames = new List<string>();
                        string sDir = Path.Combine(Settings["content"], sContentID);
                        if (Directory.Exists(sDir))
                        {
                            foreach (var oFile in Directory.GetFiles(sDir, "*.*", SearchOption.TopDirectoryOnly))
                            {
                                FileNames.Add(Path.GetFileName(oFile).ToLower());
                            }
                        }

                        foreach (JObject oFiles in jObj["Files"])
                        {
                            if (oFiles["URL"] != null && oFiles["URL"].ToString().ToLower().StartsWith("http"))
                            {
                                if (FileNames.Contains(oFiles["FileName"].Value<string>().ToLower()))
                                {
                                    //oFiles["URL"] = Base.localURL + "/rest/v2/GetFile/" + sContentID + "/" + oFiles["FileName"].ToString().Replace("\\", "/");
                                    oFiles["URL"] = Base.localURL + "/rest/v2/GetFile/" + sContentID + "/" + oFiles["FileName"].ToString().Replace("\\", "/");

                                }
                                else
                                {
                                    oFiles["URL"] = Base.localURL + "/rest/v2/GetFile/proxy/" + jObj["ShortName"].ToString() + "/" + sContentID + "/" + oFiles["FileName"].ToString().Replace("\\", "/");
                                }
                            }
                        }
                    }
                    else
                        continue;
                }
                catch { }
            }
        }

        public async Task<IActionResult> GetFile(string FilePath, string customerid = "")
        {
            string sPath = Path.Combine(Settings["content"], FilePath);
            if (File.Exists(sPath))
            {
                return new FileStreamResult(File.OpenRead(sPath), new MediaTypeHeaderValue("application/octet-stream"));
            }

            if (FilePath.StartsWith("proxy"))
            {
                FilePath = FilePath.Replace("\\", "/");
                string sShortName = FilePath.Split('/')[1];
                string sContentID = FilePath.Split('/')[2];
                string sFile = FilePath.Split('/')[3];

                sPath = Path.Combine(Settings["content"], sContentID, sFile);
                if (File.Exists(sPath))
                {
                    return new FileStreamResult(File.OpenRead(sPath), new MediaTypeHeaderValue("application/octet-stream"));
                }

                JArray aSW = GetSoftwares(sShortName, customerid);

                foreach (JObject jObj in aSW)
                {
                    if (jObj["ContentID"].ToString() == sContentID)
                    {
                        foreach (JObject jFiles in jObj["Files"])
                        {
                            if (sFile.ToLower() == jFiles["FileName"].ToString().ToLower())
                            {
                                string URL = jFiles["URL"].ToString();
                                string sFileName = jFiles["FileName"].ToString();
                                string sHashType = jFiles["HashType"].ToString();
                                string sFileHash = jFiles["FileHash"].ToString();

                                if (!Directory.Exists(Path.Combine(Settings["content"], sContentID)))
                                {
                                    Directory.CreateDirectory(Path.Combine(Settings["content"], sContentID));
                                }

                                try
                                {
                                    Stream ResponseStream = null;
                                    WebResponse Response = null;

                                    Int64 ContentLength = 1;
                                    Int64 ContentLoaded = 0;
                                    Int64 ioldProgress = 0;
                                    Int64 iProgress = 0;
                                    long FileSize = 0;

                                    if (URL.StartsWith("http"))
                                    {
                                        //_DownloadFile(URL, FileName).Result.ToString();
                                        var httpRequest = (HttpWebRequest)WebRequest.Create(URL);
                                        httpRequest.UserAgent = "chocolatey command line";
                                        httpRequest.AllowAutoRedirect = true;
                                        httpRequest.MaximumAutomaticRedirections = 5;
                                        Response = httpRequest.GetResponse();
                                    }


                                    if (URL.StartsWith("ftp"))
                                    {
                                        var ftpRequest = (FtpWebRequest)WebRequest.Create(URL);
                                        ftpRequest.ContentLength.ToString();
                                        ftpRequest.GetResponse();


                                        // Get back the HTTP response for web server
                                        Response = (FtpWebResponse)ftpRequest.GetResponse();
                                        ResponseStream = Response.GetResponseStream();

                                        ContentLength = Response.ContentLength;
                                    }

                                    // Get back the HTTP response for web server
                                    //Response = (HttpWebResponse)httpRequest.GetResponse();
                                    ResponseStream = Response.GetResponseStream();

                                    //Thanks to: https://blog.stephencleary.com/2016/11/streaming-zip-on-aspnet-core.html
                                    return new FileCallbackResult(new MediaTypeHeaderValue("application/octet-stream"), async (outputStream, _) =>
                                    {
                                        ResponseStream = Response.GetResponseStream();

                                        // Define buffer and buffer size
                                        int bufferSize = 32768; //4096;
                                        byte[] buffer = new byte[bufferSize];
                                        int bytesRead = 0;

                                        FileStream fileStream = System.IO.File.Create(Path.Combine(Settings["content"], sContentID, sFileName));

                                        while ((bytesRead = ResponseStream.Read(buffer, 0, bufferSize)) != 0)
                                        {
                                            try
                                            {

                                                if (FileSize > 0)
                                                {
                                                    ContentLength = FileSize;
                                                }
                                                else
                                                {
                                                    if (ContentLength == 1) { Int64.TryParse(Response.Headers.Get("Content-Length"), out ContentLength); }
                                                }

                                                fileStream.Write(buffer, 0, bytesRead);
                                                await outputStream.WriteAsync(buffer, 0, bytesRead);
                                                ContentLoaded = ContentLoaded + bytesRead;

                                                try
                                                {
                                                    iProgress = (100 * ContentLoaded) / ContentLength;
                                                    //only send status on percent change
                                                    if (iProgress != ioldProgress)
                                                    {
                                                        if ((iProgress % 10) == 5 || (iProgress % 10) == 0)
                                                        {
                                                            Console.WriteLine(sFileName + ": " + iProgress.ToString() + "%");
                                                            ioldProgress = iProgress;
                                                        }
                                                    }
                                                }
                                                catch { }
                                            }
                                            catch { }

                                        } // end while

                                        try
                                        {
                                            fileStream.Close();
                                            ResponseStream.Close();
                                            outputStream.Close();
                                        }
                                        catch { }
                                    });
                                }
                                catch
                                {
                                    File.Delete(Path.Combine(Settings["content"], sContentID, sFileName));
                                }
                            }
                        }
                    }
                }
            }

            return null;
        }

        public string GetShortname(string name = "", string ver = "", string man = "", string customerid = "")
        {
            string sResult = "";
            string sID = (Base.clean(man.ToLower()) + Base.clean(name.ToLower()) + Base.clean(ver.ToLower())).Trim();
            string sRowKey = Hash.CalculateMD5HashString(sID);

            //Try to get value from Memory
            if (_cache.TryGetValue("lookup-" + sID, out sResult))
            {
                return sResult;
            }

            JArray jSW = GetSoftwares(name, ver, man, customerid);

            foreach(JObject jObj in jSW)
            {
                if (jObj["ShortName"] != null)
                    return jObj["ShortName"].ToString();

                if (jObj["Shortname"] != null)
                    return jObj["Shortname"].ToString();
            }

            return "";
            
        }
        
        public bool IncCounter(string ShortName = "", string counter = "DL", string Customer = "known")
        {
            if (string.IsNullOrEmpty(Customer))
                Customer = "known";

            return RZRestAPIv2.IncCounter(ShortName, counter, Customer);
        }

        //Upload SW and wait for approval
        public bool UploadSoftwareWaiting(JArray Software, string customerid = "")
        {
            try
            {
                string ProductName = Base.clean(Software[0]["ProductName"].ToString()).Trim();
                string ProductVersion = Base.clean(Software[0]["ProductVersion"].ToString()).Trim();
                string Manufacturer = Base.clean(Software[0]["Manufacturer"].ToString()).Trim();

                string sIconId = DateTime.Now.Year.ToString().Substring(2, 2) + DateTime.Now.DayOfYear.ToString("D" + 3) + Convert.ToInt32(DateTime.Now.TimeOfDay.TotalMinutes).ToString();
                int iIconID = int.Parse(sIconId);

                foreach (JObject jObj in Software)
                {
                    try
                    {
                        if (jObj["SWId"] == null)
                            jObj.Add("SWId", iIconID);
                        else
                            jObj["SWId"] = iIconID;

                        //only used for V <= 1.6.2.x
                        if (jObj["IconId"] == null)
                            jObj.Add("IconId", iIconID);
                        else
                            jObj["IconId"] = iIconID;
                    }
                    catch { }
                }

                return RZRestAPIv2.UploadSoftware(Software);
            }
            catch { }

            return false;
        }

        public List<string> GetPendingApproval(string customerid = "")
        {
            List<string> lRes = new List<string>();
            return lRes;
        }

        public bool Approve(string Software, string customerid = "")
        {
            return false;
        }

        public bool Decline(string Software, string customerid = "")
        {
            return false;
        }

        public string GetPending(string Software, string customerid = "")
        {
            return "";
        }
    }

    public class FileCallbackResult : FileResult
    {
        private Func<Stream, ActionContext, Task> _callback;

        public FileCallbackResult(MediaTypeHeaderValue contentType, Func<Stream, ActionContext, Task> callback)
            : base(contentType?.ToString())
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));
            _callback = callback;
        }

        public override Task ExecuteResultAsync(ActionContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            var executor = new FileCallbackResultExecutor(context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>());
            return executor.ExecuteAsync(context, this);
        }

        private sealed class FileCallbackResultExecutor : FileResultExecutorBase
        {
            public FileCallbackResultExecutor(ILoggerFactory loggerFactory)
                : base(CreateLogger<FileCallbackResultExecutor>(loggerFactory))
            {
            }

            public Task ExecuteAsync(ActionContext context, FileCallbackResult result)
            {
                //SetHeadersAndLog(context, result);
                return result._callback(context.HttpContext.Response.Body, context);
            }
        }
    }
}
