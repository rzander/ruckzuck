using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json.Linq;
using System.Linq;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using static RZ.Server.RuckZuckFN;
using System.Reflection;
using Microsoft.Azure.ServiceBus;

[assembly: FunctionsStartup(typeof(Startup))]
namespace RZ.Server
{
    public static class RuckZuckFN
    {
        public static string sbconnection = "";
        public static TopicClient tcRuckZuck = null;

        public class Startup : FunctionsStartup
        {
            private ILoggerFactory _loggerFactory;
            public override void Configure(IFunctionsHostBuilder builder)
            {
                var config = new ConfigurationBuilder().AddJsonFile("local.settings.json", optional: true, reloadOnChange: true).AddEnvironmentVariables().Build();
                builder.Services.AddLogging();
                ConfigureServices(builder);
            }

            public void ConfigureServices(IFunctionsHostBuilder builder)
            {
                _loggerFactory = new LoggerFactory();
                var logger = _loggerFactory.CreateLogger("Startup");
                //logger.LogInformation("Got Here in Startup");

                if (Base._cache == null)
                {
                    Base._cache = new MemoryCache(new MemoryCacheOptions());
                }

                var binDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string ResourcePath = Path.GetFullPath(Path.Combine(binDirectory, ".."));

                Console.WriteLine("loading RZ.Software-Providers:");
                Plugins.loadPlugins(Path.Combine(Path.Combine(ResourcePath, "wwwroot"), "plugins"));

                Console.Write("loading SW-Catalog...");
                Base.GetCatalog("", true);
                Console.WriteLine(" done.");
            }
        }

        [FunctionName("GetCatalog")]
        public static async Task<IActionResult> GetCatalog([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            string customerid = req.Query["customerid"];
            customerid = customerid ?? "";
            string snocache = req.Query["nocache"];
            bool nocache = false;
            if ((snocache ?? "").ToLower() == "true")
                nocache = true;

            string ClientIP = req.HttpContext.Connection.RemoteIpAddress.ToString();


            //if (!Base.ValidateIP(ClientIP))
            //{
            //    if (Environment.GetEnvironmentVariable("EnforceGetURL") == "true")
            //        return new OkObjectResult("[]");
            //}

            if (string.IsNullOrEmpty(Base.localURL))
                Base.localURL = req.GetEncodedUrl().ToLower().Split("/rest/v2/getcatalog")[0];

            if (customerid.ToLower() == "--new--")
            {
                JArray oRes = Base.GetCatalog("", true);
                JArray jsorted = new JArray(oRes.OrderByDescending(x => (DateTimeOffset?)x["ModifyDate"]));
                JArray jTop = JArray.FromObject(jsorted.Take(30));
                return new OkObjectResult(jTop);
            }

            if (customerid.ToLower() == "--old--")
            {
                Base.ResetMemoryCache();
                JArray oRes = Base.GetCatalog(customerid);
                JArray jsorted = new JArray(oRes.OrderBy(x => (DateTimeOffset?)x["Timestamp"]));
                JArray jTop = JArray.FromObject(jsorted.Take(30));
                return new OkObjectResult(jTop);
            }

            Base.WriteLog($"Get Catalog", ClientIP, 1200, customerid);
            log.LogInformation("GetCatalog from ClientIP: " + ClientIP + " CustomerID: " + customerid);

            JArray aRes = Base.GetCatalog(customerid, nocache);

            //Cleanup
            foreach (JObject jObj in aRes)
            {
                try
                {
                    //remove quality
                    if (jObj["Quality"] != null)
                    {
                        jObj.Remove("Quality");
                    }

                    //remove Image
                    if (jObj["IconId"] != null)
                    {
                        jObj.Remove("IconId");
                    }

                    //remove Image
                    if (jObj["Image"] != null)
                    {
                        jObj.Remove("Image");
                    }
                }
                catch { }
            }

            return new OkObjectResult(aRes);
        }

        [FunctionName("GetIcon")]
        public static async Task<IActionResult> GetIcon([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            string customerid = req.Query["customerid"];
            customerid = customerid ?? "";
            string shortname = req.Query["shortname"];
            shortname = shortname ?? "";
            string iconhash = req.Query["iconhash"];
            iconhash = iconhash ?? "";
            int size = 0;
            int.TryParse(req.Query["size"], out size);
            Int32 iconid = 0;
            Int32.TryParse(req.Query["iconid"], out iconid);

            if (size > 256) //set max size 256
                size = 256;
            if (size < 0) //prevent negative numbers
                size = 0;

            if (!string.IsNullOrEmpty(shortname))
            {
                return new OkObjectResult(Base.GetIcon(shortname, customerid, size).Result);
            }

            if (!string.IsNullOrEmpty(iconhash))
            {
                return new OkObjectResult(Base.GetIcon(0, iconhash, customerid, size).Result);
            }

            if (iconid == 0)
                return null;

            return null;
        }

        [FunctionName("GetSoftwares")]
        public static async Task<IActionResult> GetSoftwares([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            string customerid = req.Query["customerid"];
            customerid = customerid ?? "";
            string shortname = req.Query["shortname"];
            shortname = shortname ?? "";
            string name = req.Query["name"];
            name = name ?? "";
            string ver = req.Query["ver"];
            ver = ver ?? "";
            string man = req.Query["man"];
            man = man ?? "";

            bool image = false;
            bool.TryParse(req.Query["image"], out image);

            string ClientIP = req.HttpContext.Connection.RemoteIpAddress.ToString();


            //if (ClientIP == "88.157.220.241" && string.IsNullOrEmpty(customerid))
            //    return Content("[]");

            if (string.IsNullOrEmpty(Base.localURL))
            {
                Base.localURL = req.GetEncodedUrl().ToLower().Split("/rest/v2/getsoftwares")[0];
                Base.WriteLog($"Set localURL: {Base.localURL}", ClientIP, 1000, customerid);
            }



            JArray jSW;
            if (!string.IsNullOrEmpty(shortname))
            {
                if (!Base.ValidateIP(ClientIP))
                {
                    if (Environment.GetEnvironmentVariable("EnforceGetURL") == "true")
                        return new OkObjectResult("[]");
                }
                else
                {
                    Base.WriteLog($"Get Definition for: {shortname}", ClientIP, 1500, customerid);
                }

                jSW = Base.GetSoftwares(shortname, customerid);
            }
            else
            {
                if (!Base.ValidateIP(ClientIP))
                {
                    if (Environment.GetEnvironmentVariable("EnforceGetURL") == "true")
                        return new OkObjectResult("[]");
                }
                else
                {
                    Base.WriteLog($"Get Definition for: {name}", ClientIP, 1500, customerid);
                }

                jSW = Base.GetSoftwares(name, ver, man, customerid);
            }
            //Cleanup
            foreach (JObject jObj in jSW)
            {
                try
                {
                    if (jObj["IconHash"] != null)
                    {
                        //Get SWId from Catalog if missing
                        if (string.IsNullOrEmpty(jObj["IconHash"].ToString()))
                        {
                            try
                            {
                                jObj["IconHash"] = Base.GetCatalog(customerid).SelectToken("$..[?(@.ShortName =='" + jObj["ShortName"] + "')]")["IconHash"];
                            }
                            catch { }
                        }
                    }

                    //generate IconURL if missing
                    if (jObj["IconURL"] == null)
                    {
                        if (jObj["IconHash"] != null)
                            jObj.Add("IconURL", Base.localURL + "/rest/v2/geticon?iconhash=" + jObj["IconHash"].ToString());
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(jObj["IconURL"].ToString()) && jObj["IconHash"] != null)
                        {
                            //switch to cdn for icons
                            string sBase = Base.localURL;
                            if (sBase.ToLower().StartsWith("https://ruckzuck.tools"))
                                sBase = "https://cdn.ruckzuck.tools";

                            jObj["IconURL"] = Base.localURL + "/rest/v2/geticon?iconhash=" + jObj["IconHash"].ToString();
                        }
                    }

                    if (jObj["IconId"] != null)
                    {
                        jObj.Remove("IconId"); //No IconID on V2!! only SWId
                    }

                    //rename Shortname to ShortName on V2
                    if (jObj["Shortname"] != null)
                    {
                        string sShortName = jObj["Shortname"].ToString();

                        jObj.Remove("Shortname");

                        if (jObj["ShortName"] == null)
                        {
                            jObj.Add("ShortName", sShortName);
                        }
                    }

                    if (jObj["SWId"] != null)
                    {
                        //Get SWId from Catalog if missing
                        if (jObj["SWId"].ToString() == "0")
                        {
                            try
                            {
                                jObj["SWId"] = Base.GetCatalog(customerid).SelectToken("$..[?(@.ShortName =='" + jObj["ShortName"] + "')]")["SWId"];
                            }
                            catch { }
                        }
                    }

                    //remove Image if not requested to reduce size
                    if (!image)
                    {
                        try
                        {
                            if (jObj["Image"] != null)
                                jObj.Remove("Image");
                        }
                        catch { }
                    }

                    //remove Author as there are no RuckZuck users anymore
                    if (jObj["Author"] != null)
                    {
                        jObj.Remove("Author");
                    }
                }
                catch { }
            }

            if (jSW != null)
                return new OkObjectResult(jSW);
            else
                return new OkObjectResult("{[]}"); //return empty json array
        }

        [FunctionName("checkforupdate")]
        public static async Task<IActionResult> checkforupdate([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            string customerid = req.Query["customerid"];
            customerid = customerid ?? "";

            string updateshash = req.Query["updateshash"];
            updateshash = updateshash ?? "";

            string ClientIP = req.HttpContext.Connection.RemoteIpAddress.ToString();

            if (!Base.ValidateIP(ClientIP))
            {
                if (Environment.GetEnvironmentVariable("EnforceGetURL") == "true")
                    return new OkObjectResult(new JArray().ToString());
            }
            else
            {
            }

            DateTime dStart = DateTime.Now;
            var oGet = new StreamReader(req.Body).ReadToEndAsync();
            JArray jItems = JArray.Parse(oGet.Result);
            if (jItems.Count > 0)
            {
                if (!string.IsNullOrEmpty(updateshash)) //still in use?
                {
                    if (updateshash != Hash.CalculateMD5HashString(oGet.Result))
                        return new OkObjectResult(new JArray().ToString());
                    else
                        Console.WriteLine("CheckForUpdates Hash Error !");
                }

                string sResult = Base.CheckForUpdates(jItems, customerid).ToString();
                TimeSpan tDuration = DateTime.Now - dStart;
                Console.WriteLine("V2 UpdateCheck duration: " + tDuration.TotalMilliseconds.ToString() + "ms");
                Base.WriteLog("V2 UpdateCheck duration: " + Math.Round(tDuration.TotalSeconds).ToString() + "s", ClientIP, 1100, customerid);
                return new OkObjectResult(sResult);
            }
            else
                return new OkObjectResult((new JArray()).ToString());
        }

        [FunctionName("geturl")]
        public static async Task<IActionResult> geturl([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            string customerid = req.Query["customerid"];
            customerid = customerid ?? "";

            string ClientIP = req.HttpContext.Connection.RemoteIpAddress.ToString();
            Base.SetValidIP(ClientIP);
            Base.WriteLog("Get URL", ClientIP, 1000, customerid);
            return new OkObjectResult(Base.GetURL(customerid, ClientIP));
        }

        [FunctionName("getip")]
        public static async Task<IActionResult> getip([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            string ClientIP = "unknown";
            try
            {
                ClientIP = req.HttpContext.Connection.RemoteIpAddress.ToString();
            }
            catch { }

            return new OkObjectResult(ClientIP);
        }

        [FunctionName("feedback")]
        public static async Task<IActionResult> feedback([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            string ClientIP = req.HttpContext.Connection.RemoteIpAddress.ToString();

            string customerid = req.Query["customerid"];
            customerid = customerid ?? "";

            string name = req.Query["name"];
            name = name ?? "";
            string ver = req.Query["ver"];
            ver = ver ?? "";
            string man = req.Query["man"];
            man = man ?? "";

            string ok = req.Query["ok"];
            ok = ok ?? "";
            string user = req.Query["user"];
            user = user ?? "";
            string text = req.Query["text"];
            text = text ?? "";

            if (!Base.ValidateIP(ClientIP))
            {
                if (Environment.GetEnvironmentVariable("EnforceGetURL") == "true")
                    return null;
            }

            string Shortname = Base.GetShortname(name, ver, man, customerid);

            if (!string.IsNullOrEmpty(Shortname))
            {
                try
                {
                    bool bWorking = false;
                    try
                    {
                        if (string.IsNullOrEmpty(ok))
                            ok = "false";

                        bool.TryParse(ok, out bWorking);

                        if (text.ToLower().Trim() != "test")
                        {
                            if (bWorking)
                            {
                                Base.WriteLog($"{Shortname} : {text}", ClientIP, 2000, customerid);
                            }
                            else
                            {
                                Base.WriteLog($"{Shortname} : {text}", ClientIP, 2001, customerid);
                            }
                        }

                        Base.StoreFeedback(name, ver, man, Shortname, text, user, !bWorking, ClientIP, customerid);
                    }
                    catch { }


                    if (bWorking)
                        Base.IncCounter(Shortname, "SUCCESS", customerid);
                    else
                        Base.IncCounter(Shortname, "FAILURE", customerid);

                }
                catch { }
            }
            else
            {
                Base.WriteLog($"{man} {name} {ver} : {text}", ClientIP, 2001, customerid);
            }

            return new OkResult();
        }

        [FunctionName("IncCounter")]
        public static async Task<IActionResult> IncCounter([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            string ClientIP = req.HttpContext.Connection.RemoteIpAddress.ToString();

            string customerid = req.Query["customerid"];
            customerid = customerid ?? "";
            string shortname = req.Query["shortname"];
            shortname = shortname ?? "";
            string counter = req.Query["counter"];
            counter = counter ?? "DL";

            if (!Base.ValidateIP(ClientIP))
            {
                if (Environment.GetEnvironmentVariable("EnforceGetURL") == "true")
                    return new OkObjectResult(false);
            }
            else
            {
            }

            if (string.IsNullOrEmpty(customerid))
            {
                //if (ClientIP.StartsWith("152.195.1"))
                //    return false;
                //if (ClientIP.StartsWith("152.199.1"))
                //    return false;
            }

            if (string.IsNullOrEmpty(shortname))
                return new OkObjectResult(false);
            else
            {
                try
                {
                    Message bMSG;
                    bMSG = new Message() { Label = "RuckZuck/WCF/downloaded/" + shortname, TimeToLive = new TimeSpan(24, 0, 0) };
                    bMSG.UserProperties.Add("ShortName", shortname);
                    bMSG.UserProperties.Add("ClientIP", ClientIP);
                    bMSG.UserProperties.Add("CustomerID", customerid);

                    if (!string.IsNullOrEmpty(sbconnection))
                    {
                        if (tcRuckZuck == null)
                        {
                            Console.WriteLine("SBConnection:" + sbconnection);
                            tcRuckZuck = new TopicClient(sbconnection, "RuckZuck", RetryPolicy.Default);
                        }
                    }
                    else
                        tcRuckZuck = null;

                    if (tcRuckZuck != null)
                        await tcRuckZuck.SendAsync(bMSG);

                    Base.WriteLog($"Content donwloaded: {shortname}", ClientIP, 1300, customerid);
                }
                catch { }

                return new OkObjectResult(Base.IncCounter(shortname, counter, customerid));
            }
        }

        [FunctionName("GetFile")]
        public static async Task<IActionResult> GetFile([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "GetFile/{contentid}/{filename}")] HttpRequest req, string contentid, string filename, ILogger log)
        {
            string ClientIP = req.HttpContext.Connection.RemoteIpAddress.ToString();

            string customerid = req.Query["customerid"];
            customerid = customerid ?? "";
            string shortname = req.Query["shortname"];
            shortname = shortname ?? "";
            //string contentid = req.Query["contentid"];
            //contentid = contentid ?? "";
            //string filename = req.Query["filename"];
            //filename = filename ?? "";

            if (!Base.ValidateIP(ClientIP))
            {
                if (Environment.GetEnvironmentVariable("EnforceGetURL") == "true")
                    return new NotFoundResult();
            }
            else
            {
            }

            string sPath = Path.Combine(contentid, filename);
            if (!string.IsNullOrEmpty(shortname))
                sPath = Path.Combine("proxy", shortname, contentid, filename);

            Base.WriteLog($"GetFile {sPath}", ClientIP, 1200, customerid);
            log.LogInformation($"GetFile: {sPath} CustomerID: {customerid}");

            return await Base.GetFile(sPath, customerid);
        }

        [FunctionName("UploadSoftware")]
        public static async Task<IActionResult> UploadSoftware([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req, ILogger log)
        {
            string ClientIP = req.HttpContext.Connection.RemoteIpAddress.ToString();

            string customerid = req.Query["customerid"];
            customerid = customerid ?? "";

            try
            {
                var oGet = new StreamReader(req.Body).ReadToEndAsync();
                string sJson = oGet.Result;
                if (sJson.TrimStart().StartsWith('['))
                    return new OkObjectResult(Base.UploadSoftwareWaiting(JArray.Parse(oGet.Result), customerid));
                else
                {
                    JArray jResult = new JArray();
                    jResult.Add(JObject.Parse(oGet.Result));
                    return new OkObjectResult(Base.UploadSoftwareWaiting(jResult, customerid));
                }
            }
            catch { }
            return new OkObjectResult(false);

        }

        [FunctionName("uploadswentry")]
        public static async Task<IActionResult> uploadswentry([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req, ILogger log)
        {
            string ClientIP = req.HttpContext.Connection.RemoteIpAddress.ToString();

            if (!Base.ValidateIP(ClientIP))
            {
                if (Environment.GetEnvironmentVariable("EnforceGetURL") == "true")
                    return new OkObjectResult(false);
            }

            string customerid = req.Query["customerid"];
            customerid = customerid ?? "";

            var oGet = new StreamReader(req.Body).ReadToEndAsync();
            string sJSON = oGet.Result;

            Base.WriteLog($"NEW SW is waiting for approval...", ClientIP, 1050, customerid);

            if (sJSON.TrimStart().StartsWith('['))
            {
                bool bRes = Base.UploadSoftwareWaiting(JArray.Parse(sJSON), customerid);
                return new OkObjectResult(bRes);
            }
            else
            {
                JArray jSW = new JArray();
                jSW.Add(JObject.Parse(sJSON));
                bool bRes = Base.UploadSoftwareWaiting(jSW, customerid);

                return new OkObjectResult(bRes); ;
            }
            return new OkObjectResult(false);

        }
    }
}
