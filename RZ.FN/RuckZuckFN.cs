using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using RZ.ServerFN;
using Serilog;
using Serilog.Events;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using static RZ.Server.RuckZuckFN;

[assembly: FunctionsStartup(typeof(Startup))]

namespace RZ.Server
{
    public static class RuckZuckFN
    {
        public static DateTime tLoadTime = new DateTime();
        public static bool bOverload = false;
        public static long lCount = 0;
        private static HttpClient oClient = new HttpClient();

        public class Startup : FunctionsStartup
        {
            //private ILoggerFactory _loggerFactory;
            //private IKeyVaultClient _keyVaultClient;

            public override void Configure(IFunctionsHostBuilder builder)
            {
                var config = new ConfigurationBuilder()
                    .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                    .AddEnvironmentVariables()
                    .Build();

                ConfigureServices(builder, config);
            }

            //public override void ConfigureAppConfiguration(IFunctionsConfigurationBuilder builder)
            //{
            //    //var azureServiceTokenProvider = new AzureServiceTokenProvider();
            //    var builtConfig = builder.ConfigurationBuilder.Build();
            //}

            public void ConfigureServices(IFunctionsHostBuilder builder, IConfiguration config)
            {
                //_loggerFactory = new LoggerFactory();
                //var logger = _loggerFactory.CreateLogger("Startup");

                if (Base._cache == null)
                {
                    Base._cache = new MemoryCache(new MemoryCacheOptions());
                }

                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Verbose()
                    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                    .MinimumLevel.Override("Function", LogEventLevel.Warning)
                    .MinimumLevel.Override("Host", LogEventLevel.Warning)
                    .Enrich.FromLogContext()
                    .WriteTo.Console()
                    .WriteTo.Seq(Environment.GetEnvironmentVariable("SeqUri"), apiKey: Environment.GetEnvironmentVariable("SeqAPI"))
                    .Enrich.WithProperty("host", Environment.MachineName)
                    .CreateLogger();

                //https://ruckzuckseq.azurewebsites.net/
                //builder.Services.AddSingleton<ILoggerProvider>(sp => new SerilogLoggerProvider(Log.Logger, true));

                builder.Services.AddLogging(lb =>
                {
                    //lb.ClearProviders();
                    lb.AddSerilog(Log.Logger);
                });

                SecretClientOptions options = new SecretClientOptions()
                {
                    Retry =
                        {
                            Delay= TimeSpan.FromSeconds(2),
                            MaxDelay = TimeSpan.FromSeconds(16),
                            MaxRetries = 5,
                            Mode = RetryMode.Exponential
                        }
                };
                string vaultBaseUrl = Environment.GetEnvironmentVariable("VaultUri");
                var _keyVaultClient = new SecretClient(new Uri(vaultBaseUrl), new DefaultAzureCredential(), options);

                try
                {
                    //Get Settings from KeyVault
                    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SAS:Cat")))
                        Environment.SetEnvironmentVariable("SAS:Cat", _keyVaultClient.GetSecret("cat").Value.Value.ToString());
                    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SAS:Cont")))
                        Environment.SetEnvironmentVariable("SAS:Cont", _keyVaultClient.GetSecret("cont").Value.Value.ToString());
                    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SAS:Icon")))
                        Environment.SetEnvironmentVariable("SAS:Icon", _keyVaultClient.GetSecret("icon").Value.Value.ToString());
                    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SAS:Repo")))
                        Environment.SetEnvironmentVariable("SAS:Repo", _keyVaultClient.GetSecret("repo").Value.Value.ToString());
                    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SAS:Wait")))
                        Environment.SetEnvironmentVariable("SAS:Wait", _keyVaultClient.GetSecret("wait").Value.Value.ToString());
                    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SAS:Look")))
                        Environment.SetEnvironmentVariable("SAS:Look", _keyVaultClient.GetSecret("look").Value.Value.ToString());
                    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SAS:Map")))
                        Environment.SetEnvironmentVariable("SAS:Map", _keyVaultClient.GetSecret("map").Value.Value.ToString());
                    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SAS:Feedback")))
                        Environment.SetEnvironmentVariable("SAS:Feedback", _keyVaultClient.GetSecret("feedback").Value.Value.ToString());
                    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SAS:Dlq")))
                        Environment.SetEnvironmentVariable("SAS:Dlq", _keyVaultClient.GetSecret("dlq").Value.Value.ToString());
                    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SAS:Suq")))
                        Environment.SetEnvironmentVariable("SAS:Suq", _keyVaultClient.GetSecret("suq").Value.Value.ToString());
                    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SAS:Faq")))
                        Environment.SetEnvironmentVariable("SAS:Faq", _keyVaultClient.GetSecret("faq").Value.Value.ToString());
                    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SAS:Swq")))
                        Environment.SetEnvironmentVariable("SAS:Swq", _keyVaultClient.GetSecret("swq").Value.Value.ToString());
                    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SAS:Ip")))
                        Environment.SetEnvironmentVariable("SAS:Ip", _keyVaultClient.GetSecret("ip").Value.Value.ToString());
                    //if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("sbConnection")))
                    //    Environment.SetEnvironmentVariable("sbConnection", _keyVaultClient.GetSecret("sbConnection").Value.Value.ToString());

                    Log.Verbose("Secrets loaded without error...");
                }
                catch (Exception ex)
                {
                    Log.ForContext("URL", vaultBaseUrl).Error("Error loading Secrets {ex}", ex.Message);
                    ex.Message.ToString();
                }

                var binDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string ResourcePath = Path.GetFullPath(Path.Combine(binDirectory, ".."));

                //Console.WriteLine("loading RZ.Software-Providers:");
                //Log.Verbose("loading RZ.Software-Providers from {path}", Path.Combine(Path.Combine(binDirectory, "wwwroot"), "plugins"));
                //Plugins.loadPlugins(Path.Combine(Path.Combine(binDirectory, "wwwroot"), "plugins"));

                Log.Verbose("loading RZ.Software-Providers from {path}", binDirectory);
                Plugins.loadPlugins(binDirectory);

                //Console.Write("loading SW-Catalog...");
                Log.Verbose("loading SW-Catalog...");
                Base.GetCatalog("", true);

                RZ.ServerFN.IP2Location.Settings = Plugins.dSettings;
                RZ.ServerFN.UpdateCounters.Settings = Plugins.dSettings;
                //sbconnection = Environment.GetEnvironmentVariable("sbConnection");
            }
        }

        [FunctionName("GetCatalog")]
        public static async Task<IActionResult> GetCatalog([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req, Microsoft.Extensions.Logging.ILogger log)
        {
            string customerid = req.Query["customerid"];
            customerid = customerid ?? "";
            string snocache = req.Query["nocache"];
            bool nocache = false;
            if ((snocache ?? "").ToLower() == "true")
                nocache = true;

            string ClientIP = req.HttpContext.Connection.RemoteIpAddress.ToString();

            if ((DateTime.Now - tLoadTime).TotalSeconds >= 60)
            {
                if (lCount > 60)
                    bOverload = true;
                else
                    bOverload = false;

                lCount = 0;
                tLoadTime = DateTime.Now;
            }
            else
            {
                lCount++;
            }

            //if (!Base.ValidateIP(ClientIP))
            //{
            //    if (Environment.GetEnvironmentVariable("EnforceGetURL") == "true")
            //        return new OkObjectResult("[]");
            //}

            if (string.IsNullOrEmpty(Base.localURL))
            {
                Base.localURL = req.GetEncodedUrl().ToLower().Split("/rest/v2/getcatalog")[0];
                Log.Information("Set local URL to {url}", Base.localURL);
            }

            if (customerid.ToLower() == "--new--")
            {
                JArray oRes = Base.GetCatalog("", false);
                JArray jsorted = new JArray(oRes.OrderByDescending(x => (DateTimeOffset?)x["ModifyDate"]));
                JArray jTop = JArray.FromObject(jsorted.Take(30));
                Log.ForContext("IP", ClientIP).ForContext("CustomerID", customerid).Information("Get --new--");
                return new OkObjectResult(jTop);
            }

            if (customerid.ToLower() == "--old--")
            {
                Base.ResetMemoryCache();
                JArray oRes = Base.GetCatalog("", true);
                JArray jsorted = new JArray(oRes.OrderBy(x => (DateTimeOffset?)x["Timestamp"]));
                JArray jTop = JArray.FromObject(jsorted.Take(30));
                Log.ForContext("IP", ClientIP).ForContext("CustomerID", customerid).Information("Get --old--");
                return new OkObjectResult(jTop);
            }

            if (!bOverload)
                _ = SendStatusAsync("", 0, "Get Catalog");

            //Base.WriteLog($"Get Catalog", ClientIP, 1200, customerid);
            //log.LogInformation("GetCatalog from ClientIP: " + ClientIP + " CustomerID: " + customerid);

            JArray aRes = new JArray();

            //only forward customerid if it's not an ipv4 address...
            if (customerid.Count(t => (t == '.')) != 3)
            {
                aRes = Base.GetCatalog(customerid, nocache);
                if (aRes.Count < 500)
                    aRes = Base.GetCatalog(customerid, true);
            }
            else
            {
                aRes = Base.GetCatalog("", nocache);
                if (aRes.Count < 500)
                    aRes = Base.GetCatalog("", true);
            }

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

            Log.ForContext("IP", ClientIP).ForContext("CustomerID", customerid).Information("Get Catalog: {count} SW Items", aRes.Count);

            await Task.CompletedTask;
            return new OkObjectResult(aRes);
        }

        [FunctionName("GetIcon")]
        public static async Task<IActionResult> GetIcon([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req, Microsoft.Extensions.Logging.ILogger log)
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
                //Log.ForContext("CustomerID", customerid).Verbose("GetIcon {shortname}{size}", shortname,  size);
                return new OkObjectResult(Base.GetIcon(shortname, customerid, size).Result);
            }

            if (!string.IsNullOrEmpty(iconhash))
            {
                //Log.ForContext("CustomerID", customerid).Verbose("GetIcon {iconhash}{size}", iconhash, size);
                return new OkObjectResult(Base.GetIcon(0, iconhash, customerid, size).Result);
            }

            if (iconid == 0)
                return null;

            await Task.CompletedTask;
            return null;
        }

        [FunctionName("GetSoftwares")]
        public static async Task<IActionResult> GetSoftwares([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req, Microsoft.Extensions.Logging.ILogger log)
        {
            string apikey = req.Query["apikey"];
            apikey = apikey ?? "";
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

            //if (ClientIP == "88.157.220.241" && string.IsNullOrEmpty(customerid))6
            //    return Content("[]");

            if (string.IsNullOrEmpty(Base.localURL))
            {
                Base.localURL = req.GetEncodedUrl().ToLower().Split("/rest/v2/getsoftwares")[0];
                //Base.WriteLog($"Set localURL: {Base.localURL}", ClientIP, 1000, customerid);
                Log.Information("Set local URL to {url}", Base.localURL);
            }

            Log.ForContext("CustomerID", customerid).Verbose("shortname: {shortname}, name: {name}", shortname, name);

            JArray jSW;
            if (!string.IsNullOrEmpty(shortname))
            {
                if (Environment.GetEnvironmentVariable("EnforceGetURL") == "true" && string.IsNullOrEmpty(customerid + apikey))
                {
                    Log.ForContext("IP", ClientIP).ForContext("CustomerID", customerid).ForContext("shortname", shortname).Warning("No Customer or ApiKey. Blocking Request.");
                    return new UnauthorizedResult();
                }

                if (!string.IsNullOrEmpty(apikey))
                {
                    if (ApiKey.ApiKeyIsValid(apikey))
                    {
                        Log.Verbose("ApiKey is valid: {key}", apikey);
                        if (string.IsNullOrEmpty(customerid))
                            customerid = apikey;
                    }
                    else
                    {
                        string sCustomer = ApiKey.GetCustomer(apikey);
                        Log.Verbose("ApiKey is NOT valid: {key} Customer: {customer}", apikey, sCustomer);

                        if (Environment.GetEnvironmentVariable("EnforceGetURL") == "true" && string.IsNullOrEmpty(customerid))
                        {
                            Log.ForContext("IP", ClientIP).Warning("Blocking request");
                            return new UnauthorizedResult();
                        }
                    }
                }


                Log.ForContext("IP", ClientIP).ForContext("CustomerID", customerid).Information("Get Definition for: {shortname}", shortname);
                jSW = Base.GetSoftwares(shortname, customerid);
            }
            else
            {

                if (Environment.GetEnvironmentVariable("EnforceGetURL") == "true" && string.IsNullOrEmpty(customerid + apikey))
                {
                    Log.ForContext("IP", ClientIP).ForContext("CustomerID", customerid).ForContext("shortname", shortname).Warning("No Customer or ApiKey. Blocking Request.");
                    return new UnauthorizedResult();
                }

                if (!string.IsNullOrEmpty(apikey))
                {
                    if (ApiKey.ApiKeyIsValid(apikey))
                    {
                        Log.Verbose("ApiKey is valid: {key}", apikey);
                        if(string.IsNullOrEmpty(customerid))
                            customerid = apikey;
                    }
                    else
                    {
                        string sCustomer = ApiKey.GetCustomer(apikey);
                        Log.Verbose("ApiKey is NOT valid: {key} Customer: {customer}", apikey, sCustomer);

                        if (Environment.GetEnvironmentVariable("EnforceGetURL") == "true" && string.IsNullOrEmpty(customerid))
                        {
                            Log.ForContext("IP", ClientIP).Verbose("Blocking request");
                            return new UnauthorizedResult();
                            //return new OkObjectResult("[]");
                        }
                    }
                }

                Log.ForContext("IP", ClientIP).ForContext("CustomerID", customerid).Information("Get Definition for: {name} {ver} {man}", name, ver, man);
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
                catch (Exception ex)
                {
                    Log.Error("Error: {ex}", ex.Message);
                }
            }

            //Log.ForContext("IP", ClientIP).ForContext("CustomerID", customerid).Verbose("Result {sw}", jSW.ToString());
            await Task.CompletedTask;

            if (jSW != null)
                return new OkObjectResult(jSW);
            else
                return new OkObjectResult("{[]}"); //return empty json array
        }

        [FunctionName("checkforupdate")]
        public static async Task<IActionResult> checkforupdate([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req, Microsoft.Extensions.Logging.ILogger log)
        {
            string customerid = req.Query["customerid"];
            customerid = customerid ?? "";

            string updateshash = req.Query["updateshash"];
            updateshash = updateshash ?? "";

            string ClientIP = req.HttpContext.Connection.RemoteIpAddress.ToString();

            //if (!Base.ValidateIP(ClientIP))
            //{
            //    if (Environment.GetEnvironmentVariable("EnforceGetURL") == "true")
            //        return new OkObjectResult(new JArray().ToString());
            //}
            //else
            //{
            //}

            if ((DateTime.Now - tLoadTime).TotalSeconds >= 60)
            {
                if (lCount > 60)
                    bOverload = true;
                else
                    bOverload = false;

                lCount = 0;
                tLoadTime = DateTime.Now;
            }
            else
            {
                lCount++;
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
                        Log.ForContext("IP", ClientIP).ForContext("CustomerID", customerid).Error("CheckForUpdates Hash Error !");
                }

                string sResult = Base.CheckForUpdates(jItems, customerid).ToString();
                TimeSpan tDuration = DateTime.Now - dStart;
                Log.ForContext("CustomerID", customerid).ForContext("IP", ClientIP).ForContext("count", jItems.Count).Verbose("V2 UpdateCheck duration: {duration}ms", Math.Round(tDuration.TotalMilliseconds).ToString());

                if (!bOverload)
                    _ = SendStatusAsync("", 0, "CheckForUpdates(items: " + jItems.Count + " , duration: " + Math.Round(tDuration.TotalSeconds).ToString() + "s) ");

                //Base.WriteLog("V2 UpdateCheck duration: " + Math.Round(tDuration.TotalSeconds).ToString() + "s", ClientIP, 1100, customerid);
                await Task.CompletedTask;
                return new OkObjectResult(sResult);
            }
            else
                return new OkObjectResult((new JArray()).ToString());
        }

        [FunctionName("geturl")]
        public static async Task<IActionResult> geturl([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req, Microsoft.Extensions.Logging.ILogger log)
        {
            string customerid = req.Query["customerid"];
            customerid = customerid ?? "";
            string apikey = req.Query["apikey"];
            apikey = apikey ?? "";

            if (string.IsNullOrEmpty(customerid))
                customerid = apikey;

            string ClientIP = req.HttpContext.Connection.RemoteIpAddress.ToString();
            //Base.SetValidIP(ClientIP);
            //Base.WriteLog("Get URL", ClientIP, 1000, customerid);

            string sURL = Base.GetURL(customerid, ClientIP);
            Log.ForContext("IP", ClientIP).ForContext("CustomerID", customerid).Information("Get URL: {url}", sURL);
            await Task.CompletedTask;
            return new OkObjectResult(sURL);
        }

        [FunctionName("getip")]
        public static async Task<IActionResult> getip([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req, Microsoft.Extensions.Logging.ILogger log)
        {
            string ClientIP = "unknown";
            try
            {
                ClientIP = req.HttpContext.Connection.RemoteIpAddress.ToString();
            }
            catch { }
            Log.Verbose("Get IP: {ip}", ClientIP);
            await Task.CompletedTask;
            return new OkObjectResult(ClientIP);
        }

        [FunctionName("feedback")]
        public static async Task<IActionResult> feedback([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req, Microsoft.Extensions.Logging.ILogger log)
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

            if ((DateTime.Now - tLoadTime).TotalSeconds >= 60)
            {
                if (lCount > 60)
                    bOverload = true;
                else
                    bOverload = false;

                lCount = 0;
                tLoadTime = DateTime.Now;
            }
            else
            {
                lCount++;
            }

            //if (!Base.ValidateIP(ClientIP))
            //{
            //    if (Environment.GetEnvironmentVariable("EnforceGetURL") == "true")
            //        return null;
            //}

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
                                //Base.WriteLog($"{Shortname} : {text}", ClientIP, 2000, customerid);
                                Log.ForContext("IP", ClientIP).ForContext("CustomerID", customerid).Information("success ({shortname})", Shortname);
                                _ = SendStatusAsync("", 3, "success (" + name + ")");
                            }
                            else
                            {
                                //Base.WriteLog($"{Shortname} : {text}", ClientIP, 2001, customerid);
                                Log.ForContext("IP", ClientIP).ForContext("CustomerID", customerid).Information("failed ({shortname}) {ok} {user} {text}", Shortname, ok, user, text);
                                _ = SendStatusAsync("", 2, "failed (" + name + ")");
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
                //Base.WriteLog($"{man} {name} {ver} : {text}", ClientIP, 2001, customerid);
            }

            await Task.CompletedTask;
            return new OkResult();
        }

        [FunctionName("IncCounter")]
        public static async Task<IActionResult> IncCounter([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req, Microsoft.Extensions.Logging.ILogger log)
        {
            string ClientIP = req.HttpContext.Connection.RemoteIpAddress.ToString();

            string customerid = req.Query["customerid"];
            customerid = customerid ?? "";
            string shortname = req.Query["shortname"];
            shortname = shortname ?? "";
            string counter = req.Query["counter"];
            counter = counter ?? "DL";

            //if (!Base.ValidateIP(ClientIP))
            //{
            //    if (Environment.GetEnvironmentVariable("EnforceGetURL") == "true")
            //        return new OkObjectResult(false);
            //}
            //else
            //{
            //}

            if ((DateTime.Now - tLoadTime).TotalSeconds >= 60)
            {
                if (lCount > 60)
                    bOverload = true;
                else
                    bOverload = false;

                lCount = 0;
                tLoadTime = DateTime.Now;
            }
            else
            {
                lCount++;
            }

            if (string.IsNullOrEmpty(shortname))
                return new OkObjectResult(false);
            else
            {
                try
                {
                    Log.ForContext("IP", ClientIP).ForContext("CustomerID", customerid).ForContext("counter", counter).Information("increment ({ct}) count for: {shortname}", counter, shortname);

                    if (!bOverload)
                        _ = SendStatusAsync("", 4, "content downloaded (" + shortname + ")");

                    //Base.WriteLog($"Content donwloaded: {shortname}", ClientIP, 1300, customerid);
                }
                catch { }

                await Task.CompletedTask;

                return new OkObjectResult(Base.IncCounter(shortname, counter, customerid));
            }
        }

        [FunctionName("GetFile")]
        public static async Task<IActionResult> GetFile([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "GetFile/{contentid}/{filename}")] HttpRequest req, string contentid, string filename, Microsoft.Extensions.Logging.ILogger log)
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

            //if (!Base.ValidateIP(ClientIP))
            //{
            //    if (Environment.GetEnvironmentVariable("EnforceGetURL") == "true")
            //        return new NotFoundResult();
            //}
            //else
            //{
            //}

            string sPath = Path.Combine(contentid, filename);
            if (!string.IsNullOrEmpty(shortname))
                sPath = Path.Combine("proxy", shortname, contentid, filename);

            //Base.WriteLog($"GetFile {sPath}", ClientIP, 1200, customerid);
            //log.LogInformation($"GetFile: {sPath} CustomerID: {customerid}");
            Log.ForContext("IP", ClientIP).ForContext("CustomerID", customerid).ForContext("shortname", shortname).Information("GetFile: {path}", sPath);

            return await Base.GetFile(sPath, customerid);
        }

        [FunctionName("UploadSoftware")]
        public static async Task<IActionResult> UploadSoftware([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req, Microsoft.Extensions.Logging.ILogger log)
        {
            string ClientIP = req.HttpContext.Connection.RemoteIpAddress.ToString();

            string customerid = req.Query["customerid"];
            customerid = customerid ?? "";

            try
            {
                var oGet = new StreamReader(req.Body).ReadToEndAsync();
                string sJson = oGet.Result;

                Log.ForContext("IP", ClientIP).ForContext("CustomerID", customerid).ForContext("json", sJson).Verbose("Uploading software");

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

            await Task.CompletedTask;
            return new OkObjectResult(false);
        }

        [FunctionName("uploadswentry")]
        public static async Task<IActionResult> uploadswentry([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req, Microsoft.Extensions.Logging.ILogger log)
        {
            string ClientIP = req.HttpContext.Connection.RemoteIpAddress.ToString();
            await Task.CompletedTask;

            //if (!Base.ValidateIP(ClientIP))
            //{
            //    if (Environment.GetEnvironmentVariable("EnforceGetURL") == "true")
            //        return new OkObjectResult(false);
            //}

            string customerid = req.Query["customerid"];
            customerid = customerid ?? "";

            var oGet = new StreamReader(req.Body).ReadToEndAsync();
            string sJSON = oGet.Result;

            //Base.WriteLog($"NEW SW is waiting for approval...", ClientIP, 1050, customerid);
            Log.ForContext("IP", ClientIP).ForContext("CustomerID", customerid).ForContext("json", sJSON).Verbose("New SW is waiting for approval.");

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
        }

        private static async Task SendStatusAsync(string code = "", int mType = 0, string statustext = "")
        {
            try
            {
                if (string.IsNullOrEmpty(code))
                    code = Environment.GetEnvironmentVariable("StatusCode");

                string sURL = Environment.GetEnvironmentVariable("RZMainPageURL");
                if (string.IsNullOrEmpty(sURL))
                    sURL = "https://ruckzuck.tools";

                using (HttpClient wClient = new HttpClient())
                {
                    string url = $"{sURL}/rest/v2/showstatus?code={code}&mtype={mType}&statustext={statustext}";
                    StringContent oCon = new StringContent("");
                    var oRes = await wClient.PostAsync(url, oCon);
                    oRes.StatusCode.ToString();
                }

                Log.ForContext("url", sURL).ForContext("code", code).Verbose("SendStatus: {text} , type: {type}", statustext, mType);
            }
            catch(Exception ex)
            {
                Log.Error("SendStatus Error 880: {ex}", ex.Message);
            }
        }
    }
}