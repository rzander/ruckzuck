using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Data.SqlClient;
using System.Text;
using Newtonsoft.Json.Linq;
using System.Net;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using static RZ.Server.Controllers.HomeController;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Authorization;
using System.Reflection;
using Microsoft.Extensions.Hosting;

namespace RZ.Server.Controllers
{
    [Authorize]
    public class AdminController : Controller
    {
        public IConfiguration Configuration { get; }
        public IHostEnvironment Env { get; }
        private readonly IHubContext<Default> _hubContext;
        private IMemoryCache _cache;

        public AdminController(IHostEnvironment env, IConfiguration configuration, IHubContext<Default> hubContext, IMemoryCache memoryCache)
        {
            Configuration = configuration;
            Env = env;
            _hubContext = hubContext;
            _cache = memoryCache;
        }

#if DEBUG
        [AllowAnonymous]
#endif
        [Authorize]
        [Route("Admin")]
        [Route("Admin/Index")]
        public IActionResult Index()
        {
            ViewBag.appVersion = typeof(AdminController).Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>().Version;
            List<string> pendingApps = Base.GetPendingApproval();

            ViewBag.ApplicationTypes = pendingApps;
            return View();
        }

#if DEBUG
        [AllowAnonymous]
#endif
        [Authorize]
        [Route("Admin/Approve")]
        [HttpPost]
        public ActionResult Approve(IFormCollection formcollection)
        {
            if (formcollection["approve"].ToString() == "Approve")
            {
                string sApp = formcollection["ApplicationType"].ToString();
                if (!string.IsNullOrEmpty(sApp))
                {

                    bool bResult = Base.Approve(sApp);
                    if(bResult)
                    {
                        _hubContext.Clients.All.SendAsync("Append", "<li class=\"list-group-item list-group-item-warning\">%tt% - SW Approved: " + sApp + "</li>");
                        _hubContext.Clients.All.SendAsync("Reload");
                        Base.ResetMemoryCache();
                        Base.SendNotification("Software Approved:" + sApp, "");
                        Base.GetCatalog("", true);
                        _hubContext.Clients.All.SendAsync("Reload");
                    }

                }
            }

            if (formcollection["decline"].ToString() == "Decline")
            {
                string sApp = formcollection["ApplicationType"].ToString();
                _hubContext.Clients.All.SendAsync("Append", "<li class=\"list-group-item list-group-item-danger\">%tt% - SW Declined: " + sApp + "</li>");
                if (!string.IsNullOrEmpty(sApp))
                {
                    Base.Decline(sApp);
                }
            }

            if (formcollection["show"].ToString() == "show JSON")
            {
                string sApp = formcollection["ApplicationType"].ToString();
                string sJSON = Base.GetPending(sApp);

                return Content(sJSON);
            }

            return RedirectToAction("Index");
        }

#if DEBUG
        [AllowAnonymous]
#endif
        [Authorize]
        [HttpPost]
        [Route("Admin/UploadSWEntry")]
        public bool UploadSWEntry()
        {
            var oGet = new StreamReader(Request.Body).ReadToEndAsync();
            string sJSON = oGet.Result;
            if (sJSON.TrimStart().StartsWith('['))
            {
                bool bRes = Base.UploadSoftware(JArray.Parse(sJSON));

                if (bRes)
                    Base.GetCatalog("", true); //reload Catalog

                return bRes;
            }
            else
            {
                JArray jSW = new JArray();
                jSW.Add(JObject.Parse(sJSON));
                bool bRes = Base.UploadSoftware(jSW);

                if (bRes)
                    Base.GetCatalog("", true); //reload Catalog

                return bRes;
            }
        }

        public string GetDownloadURL(string url, string Customer, string RowKey)
        {
            try
            {
                string sasToken = url.Substring(url.IndexOf("?"));
                string sURL = url.Substring(0, url.IndexOf("?"));

                var request = (HttpWebRequest)WebRequest.Create(sURL + "(PartitionKey='" + Customer + "',RowKey='" + RowKey + "')" + sasToken);

                request.Method = "GET";
                request.Headers.Add("x-ms-version", "2017-04-17");
                request.Headers.Add("x-ms-date", DateTime.Now.ToUniversalTime().ToString("R"));
                request.Accept = "application/json;odata=fullmetadata";
                //request.UserAgent = ""; // RequestConstants.UserAgentValue;
                //request.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                var content = string.Empty;

                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    using (var stream = response.GetResponseStream())
                    {
                        using (var sr = new StreamReader(stream))
                        {
                            content = sr.ReadToEnd();
                        }
                    }
                }

                var jres = JObject.Parse(content);

                return jres["DownloadURL"].Value<string>();
            }
            catch { }

            return "";
        }

        public void InsertEntityAsync(string url, string PartitionKey, string RowKey, string JSON)
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

        public void UpdateEntityAsync(string url, string PartitionKey, string RowKey, string JSON)
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
                    //jObj.Add("PartitionKey", PartitionKey);
                    //jObj.Add("RowKey", RowKey);
                    using (HttpClient oClient = new HttpClient())
                    {
                        oClient.DefaultRequestHeaders.Accept.Clear();
                        oClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                        //oClient.DefaultRequestHeaders.Add("If-Match", "*");
                        HttpContent oCont = new StringContent(jObj.ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8, "application/json");
                        oCont.Headers.Add("x-ms-version", "2017-04-17");
                        //oCont.Headers.Add("Prefer", "return-no-content");
                        //oCont.Headers.Add("If-Match", "*");
                        oCont.Headers.Add("x-ms-date", DateTime.Now.ToUniversalTime().ToString("R"));
                        var oRes = oClient.PutAsync(url, oCont);
                        oRes.Wait();
                    }

                }
                catch (Exception ex)
                {
                    ex.Message.ToString();
                }
            });

        }

        private void MergeEntityAsync(string url, string PartitionKey, string RowKey, string JSON)
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
                        oClient.DefaultRequestHeaders.Add("If-Match", "*");
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

#if DEBUG
        [AllowAnonymous]
#endif
        [Authorize]
        public ActionResult Refresh()
        {
            Plugins.loadPlugins(Path.Combine(Path.Combine(Env.ContentRootPath, "wwwroot"), "plugins"));
            Base.GetCatalog("", true);

            _hubContext.Clients.All.SendAsync("Reload");

            return RedirectToAction("Index");
        }

#if DEBUG
        [AllowAnonymous]
#endif
        [Authorize]
        public ActionResult ShowJson(IFormCollection formcollection)
        {
            string sApp = formcollection["ApplicationType"].ToString();
            return RedirectToAction("Index");
        }
    }
}