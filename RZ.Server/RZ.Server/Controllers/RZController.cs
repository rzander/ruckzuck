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
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json.Linq;

namespace RZ.Server.Controllers
{
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public class RZController : Controller
    {
        private readonly IHubContext<Default> _hubContext;
        private IMemoryCache _cache;

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
            _hubContext.Clients.All.SendAsync("Append", "<li class=\"list-group-item list-group-item-light\">%tt% - Get Catalog</li>");
            return Content(Base.GetCatalog(customerid, nocache).ToString());
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

            return Base.GetIcon(iconid);
        }


        [HttpGet]
        [Route("rest/v2/GetSoftwares")]
        [Route("rest/v2/GetSoftwares/{man}/{name}/{ver}")]
        public ActionResult GetSoftwares(string name = "", string ver = "", string man = "", string shortname = "")  //result = array
        {
            if (string.IsNullOrEmpty(Base.localURL))
                Base.localURL = Request.GetEncodedUrl().ToLower().Split("/rest/v2/getsoftwares")[0];

            if (!string.IsNullOrEmpty(shortname))
            {
                _hubContext.Clients.All.SendAsync("Append", "<li class=\"list-group-item list-group-item-light\">%tt% - Get Definition for '" + shortname + "'</li>");
                return Content(Base.GetSoftwares(shortname).ToString());
            }

            _hubContext.Clients.All.SendAsync("Append", "<li class=\"list-group-item list-group-item-light\">%tt% - Get Definition for '" + name + "'</li>");
            return Content(Base.GetSoftwares(name, ver, man).ToString());
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
            return Base.IncCounter(shortname, counter);
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
        [Route("rest/v2/CheckForUpdate")]
        public ActionResult CheckForUpdate()
        {
            var oGet = new StreamReader(Request.Body).ReadToEndAsync();
            return Content(Base.CheckForUpdates(JArray.Parse(oGet.Result)).ToString());
        }

        [HttpPost]
        [Route("rest/v2/UploadSWLookup")]
        public bool UploadSWLookup() //only need to forward requests from V1 REST API,  otherwise SWLookup entries will be created from CheckForUpdate
        {
            var oGet = new StreamReader(Request.Body).ReadToEndAsync();
            string sJSON = oGet.Result;
            JObject jObj = JObject.Parse(sJSON);
            _hubContext.Clients.All.SendAsync("Append", "<li class=\"list-group-item list-group-item-light\">%tt% - UploadSWLookup (" + jObj["ProductName"].ToString() + ")</li>");
            return Base.SetShortname(jObj["ProductName"].ToString(), jObj["ProductVersion"].ToString(), jObj["Manufacturer"].ToString());
        }

    }
}