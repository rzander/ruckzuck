using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.ServiceBus;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json.Linq;

namespace RZ.Server.Controllers
{
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public class RZController : Controller
    {
        private readonly IHubContext<Default> _hubContext;
        private IMemoryCache _cache;
        static string sbconnection = "Endpoint=sb://ruckzuck.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=LtCxU2rKG6D9j/LQaqQWwkE2wU2hbV1y5RNzw8qcFlA=";
        TopicClient tcRuckZuck = new TopicClient(sbconnection, "RuckZuck", RetryPolicy.Default);

        public RZController(IMemoryCache memoryCache, IHubContext<Default> hubContext)
        {
            _cache = memoryCache;
            _hubContext = hubContext;
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
        public Task<Stream> GetIcon(string shortname = "", Int32 iconid = 0, string iconhash = "")
        {
            if (!string.IsNullOrEmpty(shortname))
            {
                return Base.GetIcon(shortname);
            }

            if (!string.IsNullOrEmpty(iconhash))
            {
                return Base.GetIcon(0, iconhash);
            }

            //iconid is obsolete !!
            return Base.GetIcon(iconid);
        }


        [HttpGet]
        [Route("rest/v2/GetSoftwares")]
        [Route("rest/v2/GetSoftwares/{man}/{name}/{ver}")]
        public ActionResult GetSoftwares(string name = "", string ver = "", string man = "", string shortname = "", bool image = false )  //result = array
        {
            if (string.IsNullOrEmpty(Base.localURL))
                Base.localURL = Request.GetEncodedUrl().ToLower().Split("/rest/v2/getsoftwares")[0];

            JArray jSW;
            if (!string.IsNullOrEmpty(shortname))
            {
                _hubContext.Clients.All.SendAsync("Append", "<li class=\"list-group-item list-group-item-light\">%tt% - Get Definition for '" + shortname + "'</li>");

                jSW = Base.GetSoftwares(shortname);
            }
            else
            {
                _hubContext.Clients.All.SendAsync("Append", "<li class=\"list-group-item list-group-item-light\">%tt% - Get Definition for '" + name + "'</li>");

                jSW = Base.GetSoftwares(name, ver, man);
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
        public ActionResult GetSoftwares(string shortname = "") //result = array
        {
            if (string.IsNullOrEmpty(Base.localURL))
                Base.localURL = Request.GetEncodedUrl().ToLower().Split("/rest/v2/getsoftwares")[0];

            if (string.IsNullOrEmpty(shortname))
                return Content("[]");

            return Content(RZ.Server.Base.GetSoftwares(shortname).ToString());
        }

        [HttpGet]
        [Route("rest/v2/GetUpdate/{man}/{name}/{ver}")]
        public ActionResult GetUpdate(string name = "", string ver = "", string man = "")
        {
            return null;
        }

        [HttpGet]
        [Route("rest/v2/IncCounter")]
        [Route("rest/v2/IncCounter/{shortname}/{counter}")]
        public bool IncCounter(string shortname = "", string counter = "DL")
        {
            if (string.IsNullOrEmpty(shortname))
                return false;
            else
            {
                Message bMSG;
                bMSG = new Message() { Label = "RuckZuck/WCF/downloaded/" + shortname, TimeToLive = new TimeSpan(24, 0, 0) };
                bMSG.UserProperties.Add("ShortName", shortname);
                tcRuckZuck.SendAsync(bMSG);

                _hubContext.Clients.All.SendAsync("Append", "<li class=\"list-group-item list-group-item-info\">%tt% - content downloaded (" + shortname + ")</li>");

                return Base.IncCounter(shortname, counter);
            }
        }

        [HttpPost]
        [Route("rest/v2/UploadSoftware")]
        public bool UploadSoftware()
        {
            try
            {
                var oGet = new StreamReader(Request.Body).ReadToEndAsync();
                string sJson = oGet.Result;
                if(sJson.TrimStart().StartsWith('['))
                    return Base.UploadSoftware(JArray.Parse(oGet.Result));
                else
                {
                    JArray jResult = new JArray();
                    jResult.Add(JObject.Parse(oGet.Result));
                    return Base.UploadSoftware(jResult);
                }
            }
            catch { }
            return false;
        }

        [HttpGet]
        [Route("rest/v2/GetFile")]
        [Route("rest/v2/GetFile/{contentid}/{filename}")]
        [Route("wcf/RZService.svc/rest/v2/GetFile/{contentid}/{filename}")]
        public async Task<IActionResult> GetFile(string contentid, string filename)
        {
            string sPath = Path.Combine(contentid, filename);
            return File(await Base.GetFile(sPath), "application/octet-stream");
        }

        [HttpPost]
        [Route("rest/v2/checkforupdate")]
        public ActionResult CheckForUpdate()
        {
            var oGet = new StreamReader(Request.Body).ReadToEndAsync();
            return Content(Base.CheckForUpdates(JArray.Parse(oGet.Result)).ToString());
        }

        //[HttpPost]
        //[Route("rest/v2/UploadSWLookup")]
        //public bool UploadSWLookup() //only need to forward requests from V1 REST API,  otherwise SWLookup entries will be created from CheckForUpdate
        //{
        //    var oGet = new StreamReader(Request.Body).ReadToEndAsync();
        //    string sJSON = oGet.Result;
        //    JObject jObj = JObject.Parse(sJSON);
        //    _hubContext.Clients.All.SendAsync("Append", "<li class=\"list-group-item list-group-item-light\">%tt% - UploadSWLookup (" + jObj["ProductName"].ToString() + ")</li>");
        //    return Base.SetShortname(jObj["ProductName"].ToString(), jObj["ProductVersion"].ToString(), jObj["Manufacturer"].ToString());
        //}

        [HttpGet]
        [Route("rest/v2/GetPendingApproval")]
        public ActionResult GetPendingApproval()
        {
            return Json(Base.GetPendingApproval());
        }

        [HttpGet]
        [Route("rest/v2/geturl")]
        public ActionResult GetURL(string customerid = "")
        {
            if (customerid == "swtesting")
                return Content("https://ruckzuck.azurewebsites.net", "text/html");

            return Content("https://cdn.ruckzuck.tools", "text/html");
        }

        [HttpGet]
        [Route("rest/v2/feedback")]
        public void Feedback(string name, string ver, string man, string ok, string user, string text)
        {
            string Shortname = Base.GetShortname(name, ver, man);

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

                        if (bWorking)
                        {
                            _hubContext.Clients.All.SendAsync("Append", "<li class=\"list-group-item list-group-item-success\">%tt% - success (" + name + ")</li>");
                        }
                        else
                        {
                            _hubContext.Clients.All.SendAsync("Append", "<li class=\"list-group-item list-group-item-danger\">%tt% - failed (" + name + ")</li>");
                        }

                        Base.StoreFeedback(name, ver, man, Shortname, text, user, !bWorking);
                    }
                    catch { }


                    if (bWorking)
                        Base.IncCounter(Shortname, "SUCCESS");
                    else
                        Base.IncCounter(Shortname, "FAILURE");

                }
                catch { }
            }
        }

        [HttpPost]
        [Route("rest/v2/uploadswentry")]
        public bool UploadSWEntry()
        {
            var oGet = new StreamReader(Request.Body).ReadToEndAsync();
            string sJSON = oGet.Result;

            _hubContext.Clients.All.SendAsync("Append", "<li class=\"list-group-item list-group-item-warning\">%tt% - NEW SW is waiting for approval !!!</li>");

            if (sJSON.TrimStart().StartsWith('['))
            {
                bool bRes = Base.UploadSoftwareWaiting(JArray.Parse(sJSON));
                return bRes;
            }
            else
            {
                JArray jSW = new JArray();
                jSW.Add(JObject.Parse(sJSON));
                bool bRes = Base.UploadSoftwareWaiting(jSW);

                return bRes;
            }
        }

    }
}