using System;
using System.IO;
using System.Linq;
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
    public class RZv1Controller : Controller
    {
        private IMemoryCache _cache;
        public static string sbconnection = "";
        TopicClient tcRuckZuck;
        private readonly IHubContext<Default> _hubContext;

        public RZv1Controller(IMemoryCache memoryCache, IHubContext<Default> hubContext)
        {
            _cache = memoryCache;
            _hubContext = hubContext;

            if (!string.IsNullOrEmpty(sbconnection))
            {
                tcRuckZuck = new TopicClient(sbconnection, "RuckZuck", RetryPolicy.Default);
            }

        }

        [Route("rest")]
        public IActionResult Index()
        {
            return View();
        }

        [Route("rest/AuthenticateUser")]
        [Route("wcf/RZService.svc/rest/AuthenticateUser")]
        public ActionResult AuthenticateUser()
        {
            string Username = "FreeRZ"; //TBD
            string Password = "";

            if (string.IsNullOrEmpty(Username))
            {
                Username = Request.Headers["Username"];
                Password = Request.Headers["Password"];
            }

            // Create and store the AuthenticatedToken before returning it
            string token2 = Guid.NewGuid().ToString();

            return Content(token2, "application/json");

            if (string.IsNullOrEmpty(Username))
            {
                return Content("", "text/xml");
            }
            else
            {
                if (Username == "FreeRZ")
                {
                    try
                    {
                        byte[] data = Convert.FromBase64String(Password);
                        DateTime when = DateTime.FromBinary(BitConverter.ToInt64(data, 0));
                        if ((DateTime.UtcNow - when) <= new TimeSpan(2, 0, 10))
                        {
                            // Create and store the AuthenticatedToken before returning it
                            string token = Guid.NewGuid().ToString();

                            return Content(token, "text/xml");
                        }
                    }
                    catch { }

                    return Content("", "text/xml");
                }
                else
                {
                    //return Content(RuckZuck_WCF.RZRestProxy.GetAuthToken(Username, Password), "text/xml"); ;
                    return Content(""); //TBD
                }
            }

        }

        [HttpGet]
        [Route("rest/SWResults")]
        [Route("rest/SWResults/{search}")]
        [Route("wcf/RZService.svc/rest/SWResults")]
        [Route("wcf/RZService.svc/rest/SWResults/{search}")]
        public ActionResult SWResults(string search = "")
        {
            string sRes = "";
            if (string.IsNullOrEmpty(search))
            {
                _hubContext.Clients.All.SendAsync("Append", "<li class=\"list-group-item list-group-item-light\">%tt% - Get Catalog</li>");
                sRes = Base.GetCatalog("", false).ToString(Newtonsoft.Json.Formatting.None);
            }
            else
            {
                if (search.ToLower() == "--new--")
                {
                    JArray oRes = Base.GetCatalog("", true);
                    JArray jsorted = new JArray(oRes.OrderByDescending(x => (DateTimeOffset?)x["ModifyDate"]));
                    JArray jTop = JArray.FromObject(jsorted.Take(20));
                    return Content(jTop.ToString());
                }
                if (search.ToLower() == "--old--")
                {
                    JArray oRes = Base.GetCatalog("", true);
                    JArray jsorted = new JArray(oRes.OrderBy(x => (DateTimeOffset?)x["Timestamp"]));
                    JArray jTop = JArray.FromObject(jsorted.Take(20));
                    return Content(jTop.ToString());
                }
            }
            return Content(sRes);
        }

        [HttpGet]
        [Route("rest/GetIcon")]
        [Route("rest/GetIcon/{id}")]
        [Route("wcf/RZService.svc/rest/GetIcon")]
        [Route("wcf/RZService.svc/rest/GetIcon/{id}")]
        public Task<Stream> GetIcon(Int32 id = 575633)
        {
            return Base.GetIcon(id);
        }

        [HttpGet]
        [Route("rest/GetSWDefinition")]
        [Route("rest/GetSWDefinition/{name}/{ver}/{man}")]
        [Route("wcf/RZService.svc/rest/GetSWDefinition")]
        [Route("wcf/RZService.svc/rest/GetSWDefinition/{name}/{ver}/{man}")]
        public ActionResult GetSWDefinition(string name = "", string ver = "", string man = "")
        {
            if (string.IsNullOrEmpty(Base.localURL))
                Base.localURL = Request.GetEncodedUrl().ToLower().Split("/rest/getswdefinition")[0];

            JArray jRes = Base.GetSoftwares(name, ver, man);

            _hubContext.Clients.All.SendAsync("Append", "<li class=\"list-group-item list-group-item-light\">%tt% - get definition for '" + name + "'</li>");

            //Send Status
            try
            {
                Message bMSG = new Message() { Label = "RuckZuck/WCF/GetSWDefinitions/" + name, TimeToLive = new TimeSpan(4, 0, 0) };
                bMSG.UserProperties.Add("Count", jRes.Count);
                bMSG.UserProperties.Add("ProductName", name);
                bMSG.UserProperties.Add("ProductVersion", ver);
                bMSG.UserProperties.Add("Manufacturer", man);
                if(tcRuckZuck != null)
                    tcRuckZuck.SendAsync(bMSG);
            }
            catch { }

            return Content(jRes.ToString(Newtonsoft.Json.Formatting.None));
        }

        [HttpGet]
        [Route("rest/SWGetShort")]
        [Route("rest/SWGetShort/{name}")]
        [Route("wcf/RZService.svc/rest/SWGetShort")]
        [Route("wcf/RZService.svc/rest/SWGetShort/{name}")]
        public ActionResult SWGet(string name = "", string customerid = "")
        {
            return Content(Base.GetSoftwares(name, customerid).ToString());
        }

        [HttpPost]
        [Route("rest/CheckForUpdate")]
        [Route("wcf/RZService.svc/rest/CheckForUpdate")]
        public ActionResult CheckForUpdate()
        {
            DateTime dStart = DateTime.Now;
            var oGet = new StreamReader(Request.Body).ReadToEndAsync();
            JArray jItems = JArray.Parse(oGet.Result);
            string sResult = Base.CheckForUpdates(jItems).ToString();
            TimeSpan tDuration = DateTime.Now - dStart;
            _hubContext.Clients.All.SendAsync("Append", "<li class=\"list-group-item list-group-item-light\">%tt% - V1 API CheckForUpdates(items: " + jItems.Count +" , duration: " + Math.Round(tDuration.TotalSeconds).ToString() +"s) </li>");
            Console.WriteLine("UpdateCheck duration: " + tDuration.TotalMilliseconds.ToString() + "ms");
            return Content(sResult);
        }

        [HttpPost]
        [Route("rest/UploadSWEntry")]
        [Route("wcf/RZService.svc/rest/UploadSWEntry")]
        public bool UploadSWEntry()
        {
            var oGet = new StreamReader(Request.Body).ReadToEndAsync();
            string sJSON = oGet.Result;

            _hubContext.Clients.All.SendAsync("Append", "<li class=\"list-group-item list-group-item-warning\">%tt% - NEW SW is waiting for approval !!!</li>");

            if (sJSON.TrimStart().StartsWith('['))
            {
                bool bRes = Base.UploadSoftwareWaiting(JArray.Parse(sJSON));

                //if (bRes)
                //    Base.GetCatalog("", true); //reload Catalog

                return bRes;
            }
            else
            {
                JArray jSW = new JArray();
                jSW.Add(JObject.Parse(sJSON));
                bool bRes = Base.UploadSoftwareWaiting(jSW);

                //if (bRes)
                //    Base.GetCatalog("", true); //reload Catalog

                return bRes;
            }
        }

        [HttpPost]
        [Route("rest/UploadSWLookup")]
        [Route("wcf/RZService.svc/rest/UploadSWLookup")]
        public bool UploadSWLookup()
        {
            var oGet = new StreamReader(Request.Body).ReadToEndAsync();
            string sJSON = oGet.Result;
            JObject jObj = JObject.Parse(sJSON);
            _hubContext.Clients.All.SendAsync("Append", "<li class=\"list-group-item list-group-item-light\">%tt% - UploadSWLookup (" + jObj["ProductName"].ToString() + ")</li>");
            return Base.SetShortname(jObj["ProductName"].ToString(), jObj["ProductVersion"].ToString(), jObj["Manufacturer"].ToString());
        }

        [HttpGet]
        [Route("rest/TrackDownloadsNew")]
        [Route("wcf/RZService.svc/rest/TrackDownloadsNew")]
        //[Route("TrackDownloadsNew?SWId={SWId}&arch={Architecture}&shortname={Shortname}")]
        public bool IncCounter(string SWId = "", string arch = "", string shortname = "")
        {
            string sLabel = shortname;
            if (string.IsNullOrEmpty(shortname))
                sLabel = SWId;
            Message bMSG;
            bMSG = new Message() { Label = "RuckZuck/WCF/downloaded/" + sLabel, TimeToLive = new TimeSpan(24, 0, 0) };

            bMSG.UserProperties.Add("SWId", SWId);
            bMSG.UserProperties.Add("Architecture", arch);
            bMSG.UserProperties.Add("ShortName", shortname);
            if (tcRuckZuck != null)
                tcRuckZuck.SendAsync(bMSG);

            _hubContext.Clients.All.SendAsync("Append", "<li class=\"list-group-item list-group-item-info\">%tt% - content downloaded (" + sLabel + ")</li>");

            if (string.IsNullOrEmpty(shortname))
                return false;
            else
                return Base.IncCounter(shortname, "DL");
        }

        [HttpGet]
        [Route("rest/Feedback")]
        [Route("wcf/RZService.svc/rest/Feedback")]
        public void Feedback(string name, string ver, string man, string arch, string ok, string user, string text)
        {
            string Shortname = Base.GetShortname(name, ver, man);
            try
            {
                bool bWorking = false;
                try
                {
                    if (string.IsNullOrEmpty(ok))
                        ok = "false";

                    bool.TryParse(ok, out bWorking);

                    //Message bMSG;
                    //BrokeredMessage bMSG;
                    if (bWorking)
                    {
                        //bMSG = new Message() { Label = "RuckZuck/WCF/Feedback/success/" + name + ";" + ver, TimeToLive = new TimeSpan(24, 0, 0) };
                        _hubContext.Clients.All.SendAsync("Append", "<li class=\"list-group-item list-group-item-success\">%tt% - success (" + name+ ")</li>");
                    }
                    else
                    {
                        _hubContext.Clients.All.SendAsync("Append", "<li class=\"list-group-item list-group-item-danger\">%tt% - failed (" + name + ")</li>");
                        //bMSG = new Message() { Label = "RuckZuck/WCF/Feedback/failure/" + name + ";" + ver, TimeToLive = new TimeSpan(24, 0, 0) };
                    }

                    Base.StoreFeedback(name, ver, man, Shortname, text, user, !bWorking, "v1 API");
                }
                catch { }

                bool bWriteToDB = true;
                if (text.Contains("Product not detected after installation."))
                {
                    //We do not save status as it's from the upgrade process
                    //bWriteToDB = false;
                }

                if (text == "Requirements not valid.Installation will not start.")
                {
                    //We do not save status as it's from the upgrade process
                    //bWriteToDB = false;
                }

                //if (text == "ok")
                //    bWriteToDB = false;
                //if (text == "ok..")
                //    bWriteToDB = false;

                

                if (bWriteToDB)
                {
                    if (bWorking)
                        Base.IncCounter(Shortname, "SUCCESS");
                    else
                        Base.IncCounter(Shortname, "FAILURE");
                }

            }
            catch { }
        }

        //[HttpGet]
        //[Route("dl")]
        //[Route("dl/{contentid}/{filename}")]
        //public Stream Dl(string contentid, string filename)
        //{
        //    return Base.GetFile(Path.Combine(contentid,filename));
        //}
    }
}