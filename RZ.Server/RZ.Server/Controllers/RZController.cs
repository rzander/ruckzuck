using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.ServiceBus;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json.Linq;


namespace RZ.Server.Controllers
{
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public class RZController : Controller
    {
        private readonly IHubContext<Default> _hubContext;
        private IMemoryCache _cache;
        private IHttpContextAccessor _accessor;
        public static string sbconnection = "";
        public static TopicClient tcRuckZuck = null;
        //public AzureLogAnalytics AzureLog = new AzureLogAnalytics("", "", "");

        public RZController(IMemoryCache memoryCache, IHubContext<Default> hubContext, IHttpContextAccessor accessor)
        {
            _cache = memoryCache;
            _hubContext = hubContext;
            _accessor = accessor;

            //if (string.IsNullOrEmpty(AzureLog.WorkspaceId))
            //{
            //    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("Log-WorkspaceID")) && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("Log-SharedKey")))
            //    {
            //        AzureLog = new AzureLogAnalytics(Environment.GetEnvironmentVariable("Log-WorkspaceID"), Environment.GetEnvironmentVariable("Log-SharedKey"), "RuckZuck");
            //        //AzureLog.PostAsync(new { Computer = Environment.MachineName, EventID = 0001, Description = "Controller started" });
            //    }
            //}


        }

        [Route("rest/v2")]
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        [Route("rest/v2/GetCatalog")]
        public ActionResult GetCatalog(string customerid = "", bool nocache = false)
        {
            string ClientIP = _accessor.HttpContext.Connection.RemoteIpAddress.ToString();

            if (!Base.ValidateIP(ClientIP))
            {
                if (Environment.GetEnvironmentVariable("EnforceGetURL") == "true")
                    return Content("[]");
            }
            else
            {
            }

            if (string.IsNullOrEmpty(Base.localURL))
                Base.localURL = Request.GetEncodedUrl().ToLower().Split("/rest/v2/getcatalog")[0];

            if (customerid.ToLower() == "--new--")
            {
                JArray oRes = Base.GetCatalog("", false);
                JArray jsorted = new JArray(oRes.OrderByDescending(x => (DateTimeOffset?)x["ModifyDate"]));
                JArray jTop = JArray.FromObject(jsorted.Take(30));
                return Content(jTop.ToString());
            }

            if (customerid.ToLower() == "--old--")
            {
                JArray oRes = Base.GetCatalog("", true);
                JArray jsorted = new JArray(oRes.OrderBy(x => (DateTimeOffset?)x["Timestamp"]));
                JArray jTop = JArray.FromObject(jsorted.Take(30));
                return Content(jTop.ToString());
            }

            _hubContext.Clients.All.SendAsync("Append", "<li class=\"list-group-item list-group-item-light\">%tt% - Get Catalog</li>");
            Base.WriteLog($"Get Catalog", ClientIP, 1200, customerid);

            JArray aRes = Base.GetCatalog(customerid, nocache);
            
            //Cleanup
            foreach(JObject jObj in aRes)
            {
                //remove quality
                if(jObj["Quality"] != null)
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

            return Content(aRes.ToString());
        }

        [HttpGet]
        [Route("rest/v2/GetIcon")]
        [Route("rest/v2/GetIcon/{shortname}")]
        [Route("wcf/RZService.svc/rest/v2/GetIcon")]
        public Task<Stream> GetIcon(string shortname = "", Int32 iconid = 0, string iconhash = "", string customerid = "" , int size = 0)
        {
            if (size > 256) //set max size 256
                size = 256;
            if (size < 0) //prevent negative numbers
                size = 0;

            if (!string.IsNullOrEmpty(shortname))
            {
                return Base.GetIcon(shortname, customerid, size);
            }

            if (!string.IsNullOrEmpty(iconhash))
            {
                return Base.GetIcon(0, iconhash, customerid, size);
            }

            if (iconid == 0)
                return null;

            //iconid is obsolete !!
            return Base.GetIcon(iconid, "", customerid, size);
        }


        [HttpGet]
        [Route("rest/v2/GetSoftwares")]
        [Route("rest/v2/GetSoftwares/{man}/{name}/{ver}")]
        public ActionResult GetSoftwares(string name = "", string ver = "", string man = "", string shortname = "", bool image = false, string customerid = "" )  //result = array
        {
            string ClientIP = _accessor.HttpContext.Connection.RemoteIpAddress.ToString();

            if (string.IsNullOrEmpty(Base.localURL))
                Base.localURL = Request.GetEncodedUrl().ToLower().Split("/rest/v2/getsoftwares")[0];

            JArray jSW;
            if (!string.IsNullOrEmpty(shortname))
            {
                if (!Base.ValidateIP(ClientIP))
                {
                    if (Environment.GetEnvironmentVariable("EnforceGetURL") == "true")
                        return Content("[]");
                }
                else
                {
                    _hubContext.Clients.All.SendAsync("Append", "<li class=\"list-group-item list-group-item-light\">%tt% - Get Definition for '" + shortname + "'</li>");
                    Base.WriteLog($"Get Definition for: {shortname}", ClientIP, 1500, customerid);
                }

                jSW = Base.GetSoftwares(shortname, customerid);
            }
            else
            {
                if (!Base.ValidateIP(ClientIP))
                {
                    if (Environment.GetEnvironmentVariable("EnforceGetURL") == "true")
                        return Content("[]");
                }
                else
                {
                    _hubContext.Clients.All.SendAsync("Append", "<li class=\"list-group-item list-group-item-light\">%tt% - Get Definition for '" + name + "'</li>");
                    Base.WriteLog($"Get Definition for: {name}", ClientIP, 1500, customerid);
                }

                jSW = Base.GetSoftwares(name, ver, man, customerid);
            }
            //Cleanup
            foreach (JObject jObj in jSW)
            {
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

                if(jObj["IconId"] != null)
                {
                    jObj.Remove("IconId"); //No IconID on V2!! only SWId
                }

                //rename Shortname to ShortName on V2
                if (jObj["Shortname"] != null)
                {
                    string sShortName = jObj["Shortname"].ToString();

                    jObj.Remove("Shortname");

                    if(jObj["ShortName"] == null)
                    {
                        jObj.Add("ShortName", sShortName);
                    }
                }

                if (jObj["SWId"] != null)
                {
                    //Get SWId from Catalog if missing
                    if(jObj["SWId"].ToString() == "0")
                    {
                        try
                        {
                            jObj["SWId"] = Base.GetCatalog().SelectToken("$..[?(@.ShortName =='" + jObj["ShortName"] + "')]")["SWId"];
                        }
                        catch { }
                    }
                }

                //remove Author as there are no RuckZuck users anymore
                if(jObj["Author"] != null)
                {
                    jObj.Remove("Author");
                }
            }

            if (jSW != null)
                return Content(jSW.ToString());
            else
                return Content("{[]}"); //return empty json array
        }

        [HttpGet]
        [Route("rest/v2/GetSoftwares/{shortname}")]
        public ActionResult GetSoftwares(string shortname = "", string customerid = "") //result = array
        {
            string ClientIP = _accessor.HttpContext.Connection.RemoteIpAddress.ToString();
            if (!Base.ValidateIP(ClientIP))
            {
                if(Environment.GetEnvironmentVariable("EnforceGetURL") == "true")
                    return Content("[]");
            }
            else
            {
                Base.WriteLog($"Get Softwares: {shortname}", ClientIP, 1400, customerid);
            }

            if (string.IsNullOrEmpty(Base.localURL))
                Base.localURL = Request.GetEncodedUrl().ToLower().Split("/rest/v2/getsoftwares")[0];

            if (string.IsNullOrEmpty(shortname))
                return Content("[]");

            return Content(RZ.Server.Base.GetSoftwares(shortname, customerid).ToString());
        }

        [HttpGet]
        [Route("rest/v2/GetUpdate/{man}/{name}/{ver}")]
        public ActionResult GetUpdate(string name = "", string ver = "", string man = "", string customerid = "")
        {
            return null;
        }

        [HttpGet]
        [Route("rest/v2/IncCounter")]
        [Route("rest/v2/IncCounter/{shortname}/{counter}")]
        public bool IncCounter(string shortname = "", string counter = "DL", string customerid = "")
        {
            string ClientIP = _accessor.HttpContext.Connection.RemoteIpAddress.ToString();

            if (!Base.ValidateIP(ClientIP))
            {
                if (Environment.GetEnvironmentVariable("EnforceGetURL") == "true")
                    return false;
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
                return false;
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
                        tcRuckZuck.SendAsync(bMSG);

                    _hubContext.Clients.All.SendAsync("Append", "<li class=\"list-group-item list-group-item-info\">%tt% - content downloaded (" + shortname + ")</li>");

                    Base.WriteLog($"Content donwloaded: {shortname}", ClientIP, 1300, customerid);
                }
                catch { }

                return Base.IncCounter(shortname, counter, customerid);
            }
        }

        [HttpPost]
        [Route("rest/v2/UploadSoftware")]
        public bool UploadSoftware(string customerid = "")
        {
            try
            {
                var oGet = new StreamReader(Request.Body).ReadToEndAsync();
                string sJson = oGet.Result;
                if(sJson.TrimStart().StartsWith('['))
                    return Base.UploadSoftwareWaiting(JArray.Parse(oGet.Result), customerid);
                else
                {
                    JArray jResult = new JArray();
                    jResult.Add(JObject.Parse(oGet.Result));
                    return Base.UploadSoftwareWaiting(jResult, customerid);
                }
            }
            catch { }
            return false;
        }

        [HttpGet]
        [Route("rest/v2/GetFile")]
        [Route("rest/v2/GetFile/{contentid}/{filename}")]
        [Route("rest/v2/GetFile/proxy/{shortname}/{contentid}/{filename}")]
        [Route("wcf/RZService.svc/rest/v2/GetFile/{contentid}/{filename}")]
        public async Task<IActionResult> GetFile(string contentid, string filename, string shortname = "", string customerid = "")
        {
            string ClientIP = _accessor.HttpContext.Connection.RemoteIpAddress.ToString();

            if (!Base.ValidateIP(ClientIP))
            {
                if (Environment.GetEnvironmentVariable("EnforceGetURL") == "true")
                    return null;
            }
            else
            {
            }

            string sPath = Path.Combine(contentid, filename);
            if (!string.IsNullOrEmpty(shortname))
                sPath = Path.Combine("proxy", shortname, contentid, filename);

            Base.WriteLog($"GetFile {sPath}", ClientIP, 1200, customerid);

            return await Base.GetFile(sPath, customerid);
        }

        [HttpPost]
        [Route("rest/v2/checkforupdate")]
        [Route("rest/v2/checkforupdate/{updateshash}")]
        public ActionResult CheckForUpdate(string customerid = "", string updateshash = "")
        {
            string ClientIP = _accessor.HttpContext.Connection.RemoteIpAddress.ToString();

            if (!Base.ValidateIP(ClientIP))
            {
                if (Environment.GetEnvironmentVariable("EnforceGetURL") == "true")
                    return Content((new JArray()).ToString());
            }
            else
            {
            }

            DateTime dStart = DateTime.Now;
            var oGet = new StreamReader(Request.Body).ReadToEndAsync();
            JArray jItems = JArray.Parse(oGet.Result);
            if (jItems.Count > 0)
            {
                if(!string.IsNullOrEmpty(updateshash)) //still in use?
                {
                    if (updateshash != Hash.CalculateMD5HashString(oGet.Result))
                        return Content((new JArray()).ToString());
                    else
                        Console.WriteLine("CheckForUpdates Hash Error !");
                }

                string sResult = Base.CheckForUpdates(jItems, customerid).ToString();
                TimeSpan tDuration = DateTime.Now - dStart;
                _hubContext.Clients.All.SendAsync("Append", "<li class=\"list-group-item list-group-item-light\">%tt% - CheckForUpdates(items: " + jItems.Count + " , duration: " + Math.Round(tDuration.TotalSeconds).ToString() + "s) </li>");
                Console.WriteLine("V2 UpdateCheck duration: " + tDuration.TotalMilliseconds.ToString() + "ms");
                Base.WriteLog("V2 UpdateCheck duration: " + Math.Round(tDuration.TotalSeconds).ToString() + "s", ClientIP, 1100, customerid);
                return Content(sResult);
            }
            else
                return Content((new JArray()).ToString());
        }
        
        [HttpGet]
        [Route("rest/v2/GetPendingApproval")]
        public ActionResult GetPendingApproval(string customerid = "")
        {
            return Json(Base.GetPendingApproval(customerid));
        }

        [HttpGet]
        [Route("rest/v2/geturl")]
        public ActionResult GetURL(string customerid = "")
        {
            try
            {
                string ClientIP = _accessor.HttpContext.Connection.RemoteIpAddress.ToString();
                Base.SetValidIP(ClientIP);
                Base.WriteLog("Get URL", ClientIP, 1000, customerid);
                return Content(Base.GetURL(customerid, ClientIP), "text/html");
            }
            catch
            {
                //return Content("https://cdn.ruckzuck.tools", "text/html");
                return Content("https://ruckzuck.tools", "text/html");
            }
        }

        [HttpGet]
        [Route("rest/v2/getip")]
        public ActionResult GetIP()
        {
            string ClientIP = "unknown";
            try
            {
                ClientIP = _accessor.HttpContext.Connection.RemoteIpAddress.ToString();
            }
            catch { }

            return Content(ClientIP, "text/html");
        }

        [HttpGet]
        [Route("rest/v2/feedback")]
        public void Feedback(string name, string ver, string man, string ok, string user, string text, string customerid = "")
        {
            string ClientIP = _accessor.HttpContext.Connection.RemoteIpAddress.ToString();

            if (!Base.ValidateIP(ClientIP))
            {
                if (Environment.GetEnvironmentVariable("EnforceGetURL") == "true")
                    return;
            }
            else
            {
            }

            if (string.IsNullOrEmpty(customerid))
            {
                return; //Skip Feedback from old Clients
            }
            else
            {
                //if (customerid.StartsWith("212.25.2.73"))
                //    return;
                if (customerid.StartsWith("81.246.0.34"))
                    return;
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
                                _hubContext.Clients.All.SendAsync("Append", "<li class=\"list-group-item list-group-item-success\">%tt% - success (" + name + ")</li>");
                                Base.WriteLog($"{Shortname} : {text}", ClientIP, 2000, customerid);
                            }
                            else
                            {
                                _hubContext.Clients.All.SendAsync("Append", "<li class=\"list-group-item list-group-item-danger\">%tt% - failed (" + name + ")</li>");
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
        }

        [HttpPost]
        [Route("rest/v2/uploadswentry")]
        public bool UploadSWEntry(string customerid = "")
        {
            string ClientIP = _accessor.HttpContext.Connection.RemoteIpAddress.ToString();
            if (!Base.ValidateIP(ClientIP))
            {
                if (Environment.GetEnvironmentVariable("EnforceGetURL") == "true")
                    return false;
            }
            else
            {
            }

            var oGet = new StreamReader(Request.Body).ReadToEndAsync();
            string sJSON = oGet.Result;

            _hubContext.Clients.All.SendAsync("Append", "<li class=\"list-group-item list-group-item-warning\">%tt% - NEW SW is waiting for approval !!!</li>");


            Base.WriteLog($"NEW SW is waiting for approval...", ClientIP, 1050, customerid);

            if (sJSON.TrimStart().StartsWith('['))
            {
                bool bRes = Base.UploadSoftwareWaiting(JArray.Parse(sJSON), customerid);
                return bRes;
            }
            else
            {
                JArray jSW = new JArray();
                jSW.Add(JObject.Parse(sJSON));
                bool bRes = Base.UploadSoftwareWaiting(jSW, customerid);

                return bRes;
            }
        }

    }
}