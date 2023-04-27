using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json.Linq;
using RZ.Server;
using RZ.Server.Interfaces;
using Serilog;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Plugin_Software
{
    public class Plugin_Software : ISoftware
    {
        private IMemoryCache _cache;
        private long SlidingExpiration = 300; //5min cache for Softwares
        private HttpClient oClient = new HttpClient();
        private HttpClient oFileClient = new HttpClient();
        private HttpClient oIconClient = new HttpClient();
        private static HttpClient oEntitiesClient = new HttpClient();

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

            string icons = Path.Combine(wwwpath, "icons");
            if (!Directory.Exists(icons))
                Directory.CreateDirectory(icons);

            Settings.Add("icons", icons);

            string content = Path.Combine(wwwpath, "content");
            Settings.Add("content", content);
        }

        public JArray GetSoftwares(string shortname, string customerid = "")
        {
            JArray jResult = new JArray();

            try
            {
                //Try to get value from Memory
                if (_cache.TryGetValue("sn-" + shortname.ToLower() + customerid, out jResult))
                {
                    return jResult;
                }
            }
            catch { }

            foreach (JObject jObj in getlatestSoftware(Settings["catURL"] + "?" + Settings["catSAS"], shortname.ToLower(), "known"))
            {
                try
                {
                    jResult = GetSoftwares(jObj["ProductName"].ToString().ToLower(), jObj["ProductVersion"].ToString().ToLower(), jObj["Manufacturer"].ToString().ToLower(), customerid);

                    if (jResult.HasValues)
                    {
                        var cacheEntryOptions = new MemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromSeconds(SlidingExpiration)); //cache hash for x Seconds
                        _cache.Set("sn-" + shortname.ToLower() + customerid, jResult, cacheEntryOptions);

                        return jResult;
                    }
                }
                catch { }
            }

            var cacheEntryOptions2 = new MemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromSeconds(15)); //cache hash for 15 Seconds
            _cache.Set("sn-" + shortname.ToLower() + customerid, new JArray(), cacheEntryOptions2);

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

                //CloudBlobContainer oRepoContainer = new CloudBlobContainer(new Uri(Settings["repoURL"] + "?" + Settings["repoSAS"]));
                string sID = Base.clean(man).ToLower() + "/" + Base.clean(name).ToLower() + "/" + Base.clean(ver).ToLower();
                //var oDir = oRepoContainer.GetDirectoryReference(sID);

                CancellationToken canTok = new CancellationTokenSource(15000).Token;
                BlobServiceClient oRepoClient = new BlobServiceClient(new Uri(Settings["repoURL"] + "?" + Settings["repoSAS"]));
                var blobContainerClient = oRepoClient.GetBlobContainerClient("");


                // Call the listing operation and return pages of the specified size.
                var resultSegment = blobContainerClient.GetBlobs(BlobTraits.None, BlobStates.None, sID, canTok).AsPages(default, 25);

                // Enumerate the blobs returned for each page.
                foreach (Azure.Page<BlobItem> blobPage in resultSegment)
                {
                    foreach (BlobItem blobItem in blobPage.Values)
                    {
                        try
                        {
                            if (blobItem.Name.EndsWith(".json"))
                            {
                                BlobClient bClient = blobContainerClient.GetBlobClient(blobItem.Name);
                                jResult = JArray.Parse((bClient.DownloadContentAsync().Result).Value.Content.ToString());

                                UpdateURLs(ref jResult); //Update URL's before caching to cache the change...

                                if (Environment.GetEnvironmentVariable("CacheContent") == "true")
                                {
                                    _ = downloadFilesAsync(jResult);
                                }

                                if (jResult.Count > 0)
                                {
                                    var cacheEntryOptions = new MemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromSeconds(SlidingExpiration)); //cache hash for x Seconds
                                    _cache.Set("mnv-" + Base.clean(man).ToLower() + Base.clean(name).ToLower() + Base.clean(ver).ToLower(), jResult, cacheEntryOptions);
                                }
                                else
                                {
                                    var cacheEntryOptions = new MemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromSeconds(15)); //cache hash for 15 Seconds
                                    _cache.Set("mnv-" + Base.clean(man).ToLower() + Base.clean(name).ToLower() + Base.clean(ver).ToLower(), jResult, cacheEntryOptions);
                                }
                                return jResult;
                            }
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("E184: " + ex.Message);
            }

            return new JArray();
        }

        public async Task downloadFilesAsync(JArray jPackage)
        {
            foreach (JObject jSW in jPackage)
            {
                try
                {
                    string sContentID = jSW["ContentID"].Value<string>();
                    foreach (JObject oFiles in jSW["Files"])
                    {
                        try
                        {
                            if (oFiles["URL"].Value<string>().ToLower().Contains("https://cdn.ruckzuck.tools/rest/v2/GetFile"))
                                continue; //Skip file from cdn.ruckzuck.tools

                            string sContentPath = Path.Combine(Settings["content"], sContentID);
                            if (!File.Exists(Path.Combine(sContentPath, oFiles["FileName"].Value<string>())))
                            {
                                if (!Directory.Exists(sContentPath))
                                {
                                    Directory.CreateDirectory(sContentPath);
                                }
                                using (HttpClient hClient = new HttpClient())
                                {
                                    var response = await hClient.GetAsync(oFiles["URL"].Value<string>());
                                    using (var fs = new FileStream(Path.Combine(sContentPath, oFiles["FileName"].Value<string>()), FileMode.CreateNew))
                                    {
                                        CancellationToken ct = new CancellationTokenSource(90000).Token; //90s timeout
                                        await response.Content.CopyToAsync(fs, ct);
                                    }
                                }
                                Log.Verbose("downloadFilesAsync cached file: {file}", oFiles["FileName"].Value<string>());
                            }
                        }
                        catch(Exception ex)
                        {
                            Log.Error("ERROR 201: downloadFilesAsync, {ex}", ex.Message);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("ERROR 209: downloadFilesAsync, {ex}", ex.Message);
                }
            }
        }

        public bool UploadSoftware(JArray Software, string customerid = "")
        {
            try
            {
                JObject jSoftware = Software[0] as JObject;

                JToken oOut;
                string shortname = "";
                string manufacturer;
                string productname;
                string productversion;

                if (jSoftware["Shortname"] == null)
                {
                    if (jSoftware["ShortName"] != null)
                        shortname = jSoftware["ShortName"].ToString();
                }
                else
                {
                    shortname = jSoftware["Shortname"].ToString();
                }

                if (string.IsNullOrEmpty(shortname))
                    return false;

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

                string sIconHash = "";
                Int32 iIconID = 0;
                //Cache Icons for existing IconID and as FileHash (for the future)
                try
                {
                    JToken jIconID;
                    sIconHash = RZ.Server.Hash.CalculateMD5HashString(jSoftware["Image"].ToString());
                    //string IconsPath = Settings["icons"];
                    byte[] bIcon = jSoftware["Image"].ToObject(typeof(byte[])) as byte[];

                    //CloudBlobContainer oIcoContainer = new CloudBlobContainer(new Uri(Settings["iconURL"] + "?" + Settings["iconSAS"]));
                    //CloudBlockBlob cIcoBlock = oIcoContainer.GetBlockBlobReference(sIconHash + ".jpg");

                    CancellationToken canTok = new CancellationTokenSource(150000).Token;
                    BlobServiceClient oIcoClient = new BlobServiceClient(new Uri(Settings["iconURL"] + "?" + Settings["iconSAS"]));
                    var blobIcoContainerClient = oIcoClient.GetBlobContainerClient("");
                    var blob = blobIcoContainerClient.GetBlobClient(sIconHash + ".jpg");

                    if (!blob.Exists())
                    {
                        blobIcoContainerClient.UploadBlob(sIconHash + ".jpg", new MemoryStream(bIcon), canTok);
                    }

                    //if (!cIcoBlock.ExistsAsync().Result)
                    //    cIcoBlock.UploadFromStreamAsync(new MemoryStream(bIcon));

                    //Add IconID if missing for backward compatibility (V1.x)
                    if (jSoftware.TryGetValue("IconId", out jIconID))
                    {
                        //iIconID = jIconID.Value<Int32>();

                        //if (!string.IsNullOrEmpty(jIconID.ToString()))
                        //{
                        //    CloudBlockBlob cIcoBlock2 = oIcoContainer.GetBlockBlobReference(jIconID.ToString() + ".jpg");

                        //    if (!cIcoBlock2.ExistsAsync().Result)
                        //        cIcoBlock2.UploadFromStreamAsync(new MemoryStream(bIcon));
                        //}
                    }
                    else
                    {
                        //iIconID = (int)(DateTime.Now.Ticks >> 10); //trim ticks
                        //foreach (JObject jSW in Software)
                        //{
                        //    try
                        //    {
                        //        jSW.Add("IconID", iIconID);

                        //        CloudBlockBlob cIcoBlock2 = oIcoContainer.GetBlockBlobReference(jIconID.ToString() + ".jpg");

                        //        if (!cIcoBlock2.ExistsAsync().Result)
                        //            cIcoBlock2.UploadFromStreamAsync(new MemoryStream(bIcon));
                        //    }
                        //    catch { }
                        //}
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

                #endregion Icon fix

                //cleanup
                foreach (JObject jObj in Software)
                {
                    if (jObj["Author"] != null)
                        jObj.Remove("Author");
                }

                CancellationToken canTok2 = new CancellationTokenSource(15000).Token;
                BlobServiceClient oRepoClient = new BlobServiceClient(new Uri(Settings["repoURL"] + "?" + Settings["repoSAS"]));
                var blobRepoContainerClient = oRepoClient.GetBlobContainerClient(manufacturer.ToLower() + "/" + productname.ToLower() + "/" + productversion.ToLower());
                var repoFile = blobRepoContainerClient.GetBlobClient(manufacturer.ToLower() + "/" + productname.ToLower() + "/" + productversion.ToLower() + "/" + shortname.ToLower() + ".json");

                if (!repoFile.Exists())
                {
                    blobRepoContainerClient.UploadBlob(manufacturer.ToLower() + "/" + productname.ToLower() + "/" + productversion.ToLower() + "/" + shortname.ToLower() + ".json", BinaryData.FromString(Software.ToString(Newtonsoft.Json.Formatting.None)));
                }

                //CloudBlobContainer oRepoContainer = new CloudBlobContainer(new Uri(Settings["repoURL"] + "?" + Settings["repoSAS"]));
                //var oDir = oRepoContainer.GetDirectoryReference(manufacturer.ToLower() + "/" + productname.ToLower() + "/" + productversion.ToLower());
                //CloudBlockBlob cRepoBlock = oDir.GetBlockBlobReference(shortname.ToLower() + ".json");

                //if (!cRepoBlock.ExistsAsync().Result)
                //    cRepoBlock.UploadTextAsync(Software.ToString(Newtonsoft.Json.Formatting.None));

                foreach (JObject jObj in Software)
                {
                    JObject jEntity = new JObject();
                    jEntity.Add("Manufacturer", Base.clean(jObj["Manufacturer"].ToString()));
                    jEntity.Add("ProductName", Base.clean(jObj["ProductName"].ToString()));
                    jEntity.Add("ProductVersion", Base.clean(jObj["ProductVersion"].ToString()));

                    if (jObj["ShortName"] != null)
                    {
                        jEntity.Add("ShortName", jObj["ShortName"].ToString());
                        jEntity.Add("shortname", jObj["ShortName"].ToString().ToLower());
                    }
                    else
                    {
                        jEntity.Add("ShortName", jObj["Shortname"].ToString());
                        jEntity.Add("shortname", jObj["Shortname"].ToString().ToLower());
                    }

                    if (jObj["ProductDescription"] != null)
                        jEntity.Add("Description", jObj["ProductDescription"].ToString());
                    else
                        jEntity.Add("Description", jObj["Description"].ToString());

                    if (jObj["ProjectURL"] != null)
                        jEntity.Add("ProductURL", jObj["ProjectURL"].ToString());
                    else
                        jEntity.Add("ProductURL", jObj["ProductURL"].ToString());

                    if (jObj["Category"] != null)
                        jEntity.Add("Category", jObj["Category"].ToString());
                    else
                        jEntity.Add("Category", string.Join(';', jObj["Categories"].Value<List<string>>()));

                    jEntity.Add("IsLatest", true);

                    //DL, Fail and Success on Insert only
                    jEntity.Add("Downloads", 0);
                    jEntity.Add("Failures", 0);
                    jEntity.Add("Success", 0);

                    if (jEntity["IconHash"] == null)
                        jEntity.Add("IconHash", sIconHash);
                    if (jEntity["IconId"] == null)
                        jEntity.Add("IconId", iIconID);

                    string sID = (jEntity["Manufacturer"].ToString().ToLower() + jEntity["ProductName"].ToString().ToLower() + jEntity["ProductVersion"].ToString().ToLower()).Trim();
                    string sRowKey = Hash.CalculateMD5HashString(sID);

                    jEntity.Add("ModifyDate", DateTime.Now.ToUniversalTime().ToString("o"));

                    InsertEntityAsync(Settings["catURL"] + "?" + Settings["catSAS"], "known", sRowKey, jEntity.ToString(Newtonsoft.Json.Formatting.None));
                    //MergeEntityAsync(Settings["catURL"] + "?" + Settings["catSAS"], "known", sRowKey, jEntity.ToString(Newtonsoft.Json.Formatting.None));

                    break;
                }

                return true;
            }
            catch (Exception ex)
            {
                ex.Message.ToString();
            }

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
                                image.Mutate(i => i.Resize(new ResizeOptions { Size = new SixLabors.ImageSharp.Size(size, size) }));
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
                                    image.Mutate(i => i.Resize(new ResizeOptions { Size = new SixLabors.ImageSharp.Size(size, size) }));
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

            Stream bResult;
            byte[] bCache;

            if (string.IsNullOrEmpty(sico))
                return null;

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
                string sURL = Settings["iconURL"] + "/" + sico + ".jpg?" + Settings["iconSAS"];

                //WebRequest myWebRequest = WebRequest.Create(sURL);
                //WebResponse myWebResponse = myWebRequest.GetResponse();
                //bResult = myWebResponse.GetResponseStream();

                bResult = await oIconClient.GetStreamAsync(sURL);

                MemoryStream ms = new MemoryStream();
                bResult.CopyTo(ms);
                bCache = ms.ToArray();

                if (size > 0)
                {
                    using (Image image = Image.Load(bCache))
                    {
                        image.Mutate(i => i.Resize(new ResizeOptions { Size = new SixLabors.ImageSharp.Size(size, size) }));
                        using (var imgs = new MemoryStream())
                        {
                            var imageEncoder = image.GetConfiguration().ImageFormatsManager.FindEncoder(PngFormat.Instance);
                            image.Save(imgs, imageEncoder);
                            bCache = imgs.ToArray();
                        }
                    }
                }

                var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(95)); //cache icon for 90 Minutes
                _cache.Set("ico-" + size.ToString() + sico, bCache, cacheEntryOptions);

                try
                {
                    File.WriteAllBytes(Path.Combine(Settings["icons"], sico + "_" + size.ToString() + ".jpg"), bCache);
                }
                catch { }
                await Task.CompletedTask;
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
                    //try
                    //{
                    //    if (jObj["PreRequisites"] == null)
                    //    {
                    //        string[] oReq = new string[0];
                    //        jObj.Add("PreRequisites", JToken.FromObject(oReq));
                    //    }

                    //    if (!jObj["PreRequisites"].HasValues)
                    //    {
                    //        string[] oReq = new string[0];
                    //        jObj["PreRequisites"] = JToken.FromObject(oReq);
                    //    }
                    //}
                    //catch { }

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
                        if (_cache.TryGetValue("files-" + sContentID, out FileNames))
                        { }
                        else
                        {
                            FileNames = new List<string>();
                            //No need to load blob if local content exists...
                            if (!Directory.Exists(Path.Combine(Settings["content"], sContentID)))
                            {
                                CancellationToken canTok = new CancellationToken();
                                BlobServiceClient oRepoClient = new BlobServiceClient(new Uri(Settings["contURL"] + "?" + Settings["contSAS"]));
                                var blobContainerClient = oRepoClient.GetBlobContainerClient("");

                                // Call the listing operation and return pages of the specified size.
                                var resultSegment = blobContainerClient.GetBlobs(BlobTraits.None, BlobStates.None, sContentID, canTok).AsPages(default, 25);

                                // Enumerate the blobs returned for each page.
                                foreach (Azure.Page<BlobItem> blobPage in resultSegment)
                                {
                                    foreach (BlobItem blobItem in blobPage.Values)
                                    {
                                        FileNames.Add(Path.GetFileName(blobItem.Name.ToLower()));
                                    }
                                }

                                var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(SlidingExpiration)); //cache hash for x Seconds
                                _cache.Set("files-" + sContentID, FileNames, cacheEntryOptions);

                                //CloudBlobContainer oRepoContainer = new CloudBlobContainer(new Uri(Settings["contURL"] + "?" + Settings["contSAS"]));
                                //var oDir = oRepoContainer.GetDirectoryReference(sContentID);

                                //foreach (CloudBlockBlob oItem in oDir.ListBlobsSegmentedAsync(new BlobContinuationToken()).Result.Results)
                                //{
                                //    FileNames.Add(Path.GetFileName(oItem.Name.ToLower()));
                                //}

                                //var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(SlidingExpiration)); //cache hash for x Seconds
                                //_cache.Set("files-" + sContentID, FileNames, cacheEntryOptions);
                            }
                        }

                        foreach (JObject oFiles in jObj["Files"])
                        {
                            //Skip blob if local content folder exists
                            string sContentPath = Path.Combine(Settings["content"], sContentID);
                            if (File.Exists(Path.Combine(sContentPath, oFiles["FileName"].Value<string>())))
                            {
                                string sBase = Base.localURL;
                                if (Environment.GetEnvironmentVariable("localURL") != null)
                                    sBase = Environment.GetEnvironmentVariable("localURL"); //If hosted in a container, the localURL represensts the server URL

                                oFiles["URL"] = sBase + "/rest/v2/GetFile/" + oContentID + "/" + oFiles["FileName"].ToString().Replace("\\", "/");
                                continue;
                            }

                            if (FileNames.Contains(oFiles["FileName"].Value<string>().ToLower()))
                            {
                                //oFiles["URL"] = Base.localURL + "/rest/v2/GetFile/" + sContentID + "/" + oFiles["FileName"].ToString().Replace("\\", "/");
                                oFiles["URL"] = "https://cdn.ruckzuck.tools/rest/v2/GetFile/" + sContentID + "/" + oFiles["FileName"].ToString().Replace("\\", "/");
                            }
                        }
                    }
                    else
                        continue;
                }
                catch { }
            }
        }

        public async Task<Stream> GetFile(string FilePath, string customerid = "")
        {
            string sURL = Settings["contURL"] + "/" + FilePath.Replace('\\', '/') + "?" + Settings["contSAS"];

            return await oFileClient.GetStreamAsync(sURL);

            //WebRequest myWebRequest = WebRequest.Create(sURL);
            //WebResponse myWebResponse = myWebRequest.GetResponse();
            //return new FileStreamResult(myWebResponse.GetResponseStream(), "application/octet-stream");
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

            JArray jRes = getlatestSoftwareHash(Settings["catURL"] + "?" + Settings["catSAS"], sRowKey, "known");
            //JArray jRes = GetSoftwares(name.ToLower(), ver.ToLower(), man.ToLower());
            foreach (JObject jObj in jRes)
            {
                string shortname = jObj["shortname"].ToString();

                var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(SlidingExpiration * 2)); //cache hash for x Seconds
                _cache.Set("lookup-" + sID, shortname, cacheEntryOptions);

                return shortname;
            }

            var cacheEntryOptions2 = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(SlidingExpiration * 2)); //cache hash for x Seconds
            _cache.Set("lookup-" + sID, "", cacheEntryOptions2);
            return "";
        }

        private void InsertEntityAsync(string url, string PartitionKey, string RowKey, string JSON)
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

        private void UpdateEntityAsync(string url, string PartitionKey, string RowKey, string JSON)
        {
            try
            {
                string sasToken = url.Substring(url.IndexOf("?") + 1);
                string sURL = url.Substring(0, url.IndexOf("?"));

                url = sURL + "(PartitionKey='" + PartitionKey + "',RowKey='" + RowKey + "')?" + sasToken;

                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                var jObj = JObject.Parse(JSON);
                using (HttpClient oClient = new HttpClient())
                {
                    oClient.DefaultRequestHeaders.Accept.Clear();
                    oClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    oClient.DefaultRequestHeaders.Add("If-Match", "*");
                    HttpContent oCont = new StringContent(jObj.ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8, "application/json");
                    oCont.Headers.Add("x-ms-version", "2017-04-17");
                    oCont.Headers.Add("x-ms-date", DateTime.Now.ToUniversalTime().ToString("R"));
                    var oRes = oClient.PutAsync(url, oCont);
                    oRes.Wait();
                }
            }
            catch (Exception ex)
            {
                ex.Message.ToString();
            }
        }

        private static void MergeEntityAsync(string url, string PartitionKey, string RowKey, string JSON, string ETag = "*")
        {
            Task.Run(() =>
            {
                try
                {
                    string sasToken = url.Substring(url.IndexOf("?") + 1);
                    string sURL = url.Substring(0, url.IndexOf("?"));

                    url = sURL + "(PartitionKey='" + PartitionKey + "',RowKey='" + RowKey + "')?" + sasToken;

                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                    var jObj = JObject.Parse(JSON);
                    using (HttpClient oClient = new HttpClient())
                    {
                        oClient.DefaultRequestHeaders.Accept.Clear();
                        oClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                        oClient.DefaultRequestHeaders.Add("If-Match", ETag);
                        HttpContent oCont = new StringContent(jObj.ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8, "application/json");
                        oCont.Headers.Add("x-ms-version", "2017-04-17");
                        oCont.Headers.Add("x-ms-date", DateTime.Now.ToUniversalTime().ToString("R"));
                        var oRes = oClient.PatchAsync(url, oCont);
                        oRes.Wait();
                    }
                }
                catch (Exception ex)
                {
                    ex.Message.ToString();
                }
            });
        }

        private string GetEntityAsync(string url, string PartitionKey, string RowKey)
        {
            Task.Run(() =>
            {
                try
                {
                    string sasToken = url.Substring(url.IndexOf("?") + 1);
                    string sURL = url.Substring(0, url.IndexOf("?"));

                    url = sURL + "(PartitionKey='" + PartitionKey + "',RowKey='" + RowKey + "')?" + sasToken;

                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                    using (HttpClient oClient = new HttpClient())
                    {
                        oClient.DefaultRequestHeaders.Accept.Clear();
                        oClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                        var oRes = oClient.GetStringAsync(url);
                        oRes.Wait();
                        oRes.Result.ToString();
                    }
                }
                catch (Exception ex)
                {
                    ex.Message.ToString();
                }
            });

            return "";
        }

        private JArray getlatestSoftware(string url, string ShortName, string Customer = "known")
        {
            try
            {
                string sasToken = url.Substring(url.IndexOf("?") + 1);
                string sURL = url.Substring(0, url.IndexOf("?"));

                string uri = sURL + "()?$filter=(PartitionKey eq '" + Customer.ToLower() + "') and (shortname eq '" + WebUtility.UrlEncode(ShortName.ToLower()) + "') and (IsLatest eq true)&" + sasToken;
                return getEntities(uri);

                //var request = (HttpWebRequest)WebRequest.Create(sURL + "()?$filter=(PartitionKey eq '" + Customer.ToLower() + "') and (shortname eq '" + WebUtility.UrlEncode(ShortName.ToLower()) + "') and (IsLatest eq true)&" + sasToken);

                //request.Method = "GET";
                //request.Headers.Add("x-ms-version", "2017-04-17");
                //request.Headers.Add("x-ms-date", DateTime.Now.ToUniversalTime().ToString("R"));
                //request.Accept = "application/json;odata=nometadata";
                //ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                //var content = string.Empty;

                //using (var response = (HttpWebResponse)request.GetResponse())
                //{
                //    nextPart = response.Headers["x-ms-continuation-NextPartitionKey"];
                //    nextRow = response.Headers["x-ms-continuation-NextRowKey"];
                //    using (var stream = response.GetResponseStream())
                //    {
                //        using (var sr = new StreamReader(stream))
                //        {
                //            content = sr.ReadToEnd();
                //        }
                //    }
                //}

                //var jres = JObject.Parse(content);

                //JArray jResult = jres["value"] as JArray;

                //return jResult;
            }
            catch { }

            return new JArray();
        }

        private JArray getlatestSoftwareHash(string url, string rowkey, string partitionkey = "known")
        {
            try
            {
                string sasToken = url.Substring(url.IndexOf("?") + 1);
                string sURL = url.Substring(0, url.IndexOf("?"));

                string uri = sURL + "()?$filter=PartitionKey eq '" + partitionkey + "' and RowKey eq '" + rowkey + "'&$select=Manufacturer,ProductName,ProductVersion,shortname,Description,ProductURL,IconId,Downloads,Category&" + sasToken;
                return getEntities(uri);

                //var request = (HttpWebRequest)WebRequest.Create(sURL + "()?$filter=PartitionKey eq '" + partitionkey + "' and RowKey eq '" + rowkey + "'&$select=Manufacturer,ProductName,ProductVersion,ShortName,Description,ProductURL,IconId,Downloads,Category&" + sasToken);

                //request.Method = "GET";
                //request.Headers.Add("x-ms-version", "2017-04-17");
                //request.Headers.Add("x-ms-date", DateTime.Now.ToUniversalTime().ToString("R"));
                //request.Accept = "application/json;odata=nometadata";
                //ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                //var content = string.Empty;

                //using (var response = (HttpWebResponse)request.GetResponse())
                //{
                //    nextPart = response.Headers["x-ms-continuation-NextPartitionKey"];
                //    nextRow = response.Headers["x-ms-continuation-NextRowKey"];
                //    using (var stream = response.GetResponseStream())
                //    {
                //        using (var sr = new StreamReader(stream))
                //        {
                //            content = sr.ReadToEnd();
                //        }
                //    }
                //}

                //var jres = JObject.Parse(content);

                //JArray jResult = jres["value"] as JArray;

                //return jResult;
            }
            catch { }

            return new JArray();
        }

        public static JArray getEntities(string url)
        {
            try
            {
                string nextPart = "";
                string nextRow = "";
                //HttpWebRequest request = null;

                //request = (HttpWebRequest)WebRequest.Create(url);

                //request.Method = "GET";
                //request.Headers.Add("x-ms-version", "2017-04-17");
                //request.Headers.Add("x-ms-date", DateTime.Now.ToUniversalTime().ToString("R"));
                //request.Accept = "application/json;odata=nometadata";
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                var content = string.Empty;

                oEntitiesClient.DefaultRequestHeaders.Accept.Clear();
                //oEntitiesClient.DefaultRequestHeaders.Accept.Add(MediaTypeWithQualityHeaderValue.Parse("application/json;odata=verbose"));
                oEntitiesClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                if (!oEntitiesClient.DefaultRequestHeaders.Contains("x-ms-version"))
                {
                    oEntitiesClient.DefaultRequestHeaders.Add("x-ms-version", "2017-04-17");
                }
                if (oEntitiesClient.DefaultRequestHeaders.Contains("x-ms-date"))
                {
                    oEntitiesClient.DefaultRequestHeaders.Remove("x-ms-date");
                }
                oEntitiesClient.DefaultRequestHeaders.Add("x-ms-date", DateTime.Now.ToUniversalTime().ToString("R"));

                using (var response = oEntitiesClient.GetAsync(url).Result)
                {
                    if (response.Headers.Contains("x-ms-continuation-NextPartitionKey"))
                    {
                        nextPart = response.Headers.GetValues("x-ms-continuation-NextPartitionKey").FirstOrDefault();
                    }
                    if (response.Headers.Contains("x-ms-continuation-NextRowKey"))
                    {
                        nextRow = response.Headers.GetValues("x-ms-continuation-NextRowKey").FirstOrDefault();
                    }
                    content = response.Content.ReadAsStringAsync().Result;
                }

                //using (var response = (HttpWebResponse)request.GetResponse())
                //{
                //    nextPart = response.Headers["x-ms-continuation-NextPartitionKey"];
                //    nextRow = response.Headers["x-ms-continuation-NextRowKey"];
                //    using (var stream = response.GetResponseStream())
                //    {
                //        using (var sr = new StreamReader(stream))
                //        {
                //            content = sr.ReadToEnd();
                //        }
                //    }
                //}

                var jres = JObject.Parse(content);

                JArray jResult = jres["value"] as JArray;

                //Load next Page if there are more than 1000 Items...
                if (!string.IsNullOrEmpty(nextPart))
                {
                    string sNewURL = url.Split("&NextPartitionKey=")[0];
                    jResult.Merge(getEntities(sNewURL + $"&NextPartitionKey={nextPart}&NextRowKey={nextRow}"));
                }

                return jResult;
            }
            catch { }

            return null;
        }

        private static readonly object syncObject = new object();
        private Mutex mut = new Mutex(true, "inccounter");

        private void incQueue(string ShortName, string sasToken, string sURL)
        {
            Task.Run(() =>
            {
                try
                {
                    string url = $"{sURL}?timeout=10&{sasToken}";
                    string body = $"<QueueMessage><MessageText>{ShortName}</MessageText></QueueMessage>";
                    HttpContent oCont = new StringContent(body);
                    var oRes = oClient.PostAsync(url, oCont);
                    oRes.Wait(5000);
                }
                catch { }

                //using (HttpClient oClient = new HttpClient())
                //{
                //    string url = $"{sURL}?timeout=10&{sasToken}";
                //    string body = $"<QueueMessage><MessageText>{ShortName}</MessageText></QueueMessage>";
                //    HttpContent oCont = new StringContent(body);
                //    var oRes = oClient.PostAsync(url, oCont);
                //    oRes.Wait(5000);
                //    oRes.Result.ToString();
                //}
            });
        }

        public bool IncCounter(string ShortName = "", string counter = "DL", string Customer = "known")
        {
            switch (counter.ToUpper())
            {
                case "DL":
                    incQueue(ShortName, Settings["dlqSAS"], Settings["dlqURL"]);
                    //inc(ShortName, Settings["catSAS"], Settings["catURL"], "known", "Downloads");
                    break;

                case "FAILURE":
                    incQueue(ShortName, Settings["faqSAS"], Settings["faqURL"]);
                    //inc(ShortName, Settings["catSAS"], Settings["catURL"], "known", "Failures");
                    break;

                case "SUCCESS":
                    incQueue(ShortName, Settings["suqSAS"], Settings["suqURL"]);
                    //inc(ShortName, Settings["catSAS"], Settings["catURL"], "known", "Success");
                    break;
            }

            return true;
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

                        if (jObj["IconId"] == null)
                            jObj.Add("IconId", iIconID);
                        else
                            jObj["IconId"] = iIconID;
                    }
                    catch { }
                }

                CancellationToken canTok = new CancellationTokenSource(15000).Token;
                BlobServiceClient oWaitClient = new BlobServiceClient(new Uri(Settings["waitURL"] + "?" + Settings["waitSAS"]));
                var blobContainerClient = oWaitClient.GetBlobContainerClient("");

                blobContainerClient.UploadBlob(Manufacturer + "-" + ProductName + "-" + ProductVersion + ".json", BinaryData.FromString(Software.ToString()));

                //CloudBlobContainer oWaitContainer = new CloudBlobContainer(new Uri(Settings["waitURL"] + "?" + Settings["waitSAS"]));
                //CloudBlockBlob cJsonBlock = oWaitContainer.GetBlockBlobReference(Manufacturer + "-" + ProductName + "-" + ProductVersion + ".json");
                //var upl = cJsonBlock.UploadTextAsync(Software.ToString());
                //upl.Wait(5000);

                return true;
            }
            catch { }

            return false;
        }

        public List<string> GetPendingApproval(string customerid = "")
        {
            List<string> lRes = new List<string>();
            try
            {
                CancellationToken canTok = new CancellationTokenSource(15000).Token;
                BlobServiceClient oWaitClient = new BlobServiceClient(new Uri(Settings["waitURL"] + "?" + Settings["waitSAS"]));
                var blobContainerClient = oWaitClient.GetBlobContainerClient("");

                //CloudBlobContainer oWaitContainer = new CloudBlobContainer(new Uri(Settings["waitURL"] + "?" + Settings["waitSAS"]));
                foreach (var oBlob in blobContainerClient.GetBlobs(BlobTraits.All, BlobStates.None, null, canTok))
                {
                    //var bc = blobContainerClient.GetBlobClient(oBlob.Name);
                    //string sJSON = bc.DownloadContent(canTok).Value.Content.ToString();
                    lRes.Add(oBlob.Name.Replace(Path.GetExtension(oBlob.Name), ""));
                }

                //    BlobContinuationToken continuationToken = null;
                //List<IListBlobItem> results = new List<IListBlobItem>();
                //do
                //{
                //    var response = oWaitContainer.ListBlobsSegmentedAsync(continuationToken).Result;
                //    continuationToken = response.ContinuationToken;
                //    results.AddRange(response.Results);
                //}
                //while (continuationToken != null);

                //foreach (CloudBlockBlob lItem in results)
                //{
                //    try
                //    {
                //        lRes.Add(lItem.Name.Replace(Path.GetExtension(lItem.Name), ""));
                //    }
                //    catch { }
                //}
            }
            catch { }

            return lRes;
        }

        public bool Approve(string Software, string customerid = "")
        {
            try
            {
                CancellationToken canTok = new CancellationTokenSource(15000).Token;
                BlobServiceClient oWaitClient = new BlobServiceClient(new Uri(Settings["waitURL"] + "?" + Settings["waitSAS"]));
                var blobContainerClient = oWaitClient.GetBlobContainerClient("");

                //CloudBlobContainer oWaitContainer = new CloudBlobContainer(new Uri(Settings["waitURL"] + "?" + Settings["waitSAS"]));
                foreach (var oBlob in blobContainerClient.GetBlobs(BlobTraits.All, BlobStates.None, Software + ".json", canTok))
                {
                    var bc = blobContainerClient.GetBlobClient(oBlob.Name);
                    string sJSON = bc.DownloadContent(canTok).Value.Content.ToString();

                    //string sJSON = lItem.DownloadTextAsync().Result;
                    if (sJSON.StartsWith('['))
                    {
                        JArray jSW = JArray.Parse(sJSON);

                        #region remove IsLatest on old Catalog Item

                        try
                        {
                            string shortname = "";
                            if (jSW[0]["Shortname"] != null)
                                shortname = jSW[0]["Shortname"].ToString();
                            else
                            {
                                if (jSW[0]["ShortName"] != null)
                                    shortname = jSW[0]["ShortName"].ToString();
                            }

                            if (!string.IsNullOrEmpty(shortname))
                            {
                                JArray joldSW = getlatestSoftware(Settings["catURL"] + "?" + Settings["catSAS"], shortname.ToLower(), "known");
                                foreach (JObject jOldItem in joldSW)
                                {
                                    jOldItem["IsLatest"] = false; //disable isLatest Flag
                                    UpdateEntityAsync(Settings["catURL"] + "?" + Settings["catSAS"], jOldItem["PartitionKey"].ToString(), jOldItem["RowKey"].ToString(), jOldItem.ToString());
                                }
                            }
                        }
                        catch { }

                        #endregion remove IsLatest on old Catalog Item

                        if (UploadSoftware(jSW))
                        {
                            bc.Delete();
                            //var tDel = lItem.DeleteAsync();
                            //tDel.Wait(5000);
                            return true;
                        }
                    }
                }

                //foreach (CloudBlockBlob lItem in oWaitContainer.ListBlobsSegmentedAsync(Software + ".json", new BlobContinuationToken()).Result.Results)
                //{
                //}
            }
            catch { }

            return false;
        }

        public bool Decline(string Software, string customerid = "")
        {
            try
            {
                //CloudBlobContainer oWaitContainer = new CloudBlobContainer(new Uri(Settings["waitURL"] + "?" + Settings["waitSAS"]));

                CancellationToken canTok = new CancellationTokenSource(15000).Token;
                BlobServiceClient oWaitClient = new BlobServiceClient(new Uri(Settings["waitURL"] + "?" + Settings["waitSAS"]));
                var blobContainerClient = oWaitClient.GetBlobContainerClient("");

                //foreach (CloudBlockBlob lItem in oWaitContainer.ListBlobsSegmentedAsync(Software + ".json", new BlobContinuationToken()).Result.Results)
                //{
                //    lItem.DeleteAsync();
                //}

                foreach (var oBlob in blobContainerClient.GetBlobs(BlobTraits.All, BlobStates.None, Software + ".json", canTok))
                {
                    var bc = blobContainerClient.GetBlobClient(oBlob.Name);
                    bc.Delete(DeleteSnapshotsOption.None, null, canTok);
                }

                return true;
            }
            catch { }

            return false;
        }

        public string GetPending(string Software, string customerid = "")
        {
            try
            {
                //CloudBlobContainer oWaitContainer = new CloudBlobContainer(new Uri(Settings["waitURL"] + "?" + Settings["waitSAS"]));

                CancellationToken canTok = new CancellationTokenSource(15000).Token;
                BlobServiceClient oWaitClient = new BlobServiceClient(new Uri(Settings["waitURL"] + "?" + Settings["waitSAS"]));
                var blobContainerClient = oWaitClient.GetBlobContainerClient("");

                foreach (var oBlob in blobContainerClient.GetBlobs(BlobTraits.All, BlobStates.None, Software + ".json", canTok))
                {
                    var bc = blobContainerClient.GetBlobClient(oBlob.Name);
                    string sJSON = bc.DownloadContent(canTok).Value.Content.ToString();
                    return sJSON;
                }
            }
            catch { }

            return "";
        }
    }
}