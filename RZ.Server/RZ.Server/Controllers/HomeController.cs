using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using RZ.Server.Models;

namespace RZ.Server.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            string sURL = Request.GetEncodedUrl().ToLower().TrimEnd('/');

            List<LatestItems> lSW = new List<LatestItems>();
            JArray oRes = Base.GetCatalog("", false);
            JArray jsorted = new JArray(oRes.OrderByDescending(x => (DateTimeOffset?)x["ModifyDate"]));
            JArray jTop = JArray.FromObject(jsorted.Take(5));
            foreach(JObject jSW in jTop)
            {
                LatestItems oSW = new LatestItems();
                oSW.Shortname = jSW["ShortName"].ToString();
                oSW.Version = jSW["ProductVersion"].ToString();
                if(jSW["IconHash"] != null)
                    oSW.IconURL = sURL + "/rest/v2/GetIcon?iconhash=" + jSW["IconHash"].ToString();
                else
                    oSW.IconURL = sURL + "/rest/v2/GetIcon?id=" + jSW["SWId"].ToString();
                oSW.Date = jSW["ModifyDate"].Value<DateTime>().ToString("yyyy-MM-dd");
                lSW.Add(oSW);
            }

            ViewData["LatestItems"] = lSW;
            return View();
        }

        public IActionResult About()
        {
            ViewData["Message"] = "Your application description page.";

            return View();
        }

        public IActionResult Contact()
        {
            ViewData["Message"] = "Your contact page.";

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        public class LatestItems
        {
            public string Shortname { get; set; }
            public string Version { get; set; }
            public string IconURL{ get; set; }

            public string Date { get; set; }
        }

        public ActionResult ReloadLatest()
        {
            string sURL = Request.GetEncodedUrl().ToLower().TrimEnd('/');
            sURL = sURL.Split("/home")[0];

            List<LatestItems> lSW = new List<LatestItems>();
            JArray oRes = Base.GetCatalog("", false);
            JArray jsorted = new JArray(oRes.OrderByDescending(x => (DateTimeOffset?)x["ModifyDate"]));
            JArray jTop = JArray.FromObject(jsorted.Take(5));
            foreach (JObject jSW in jTop)
            {
                LatestItems oSW = new LatestItems();
                oSW.Shortname = jSW["ShortName"].ToString();
                oSW.Version = jSW["ProductVersion"].ToString();
                if (jSW["IconHash"] != null)
                    oSW.IconURL = sURL + "/rest/v2/GetIcon?iconhash=" + jSW["IconHash"].ToString();
                else
                    oSW.IconURL = sURL + "/rest/v2/GetIcon?id=" + jSW["SWId"].ToString();
                oSW.Date = jSW["ModifyDate"].Value<DateTime>().ToString("yyyy-MM-dd");
                lSW.Add(oSW);
            }

            return Json(lSW);
        }
    }
}
