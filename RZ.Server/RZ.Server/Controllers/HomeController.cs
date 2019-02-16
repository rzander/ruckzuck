using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using RZ.Server.Models;

namespace RZ.Server.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        [AllowAnonymous]
        public IActionResult Index()
        {
            ViewBag.appVersion = typeof(HomeController).Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>().Version;
            string sURL = Request.GetEncodedUrl().ToLower().TrimEnd('/');

            List<LatestItem> lSW = new List<LatestItem>();
            JArray oRes = Base.GetCatalog("", false);
            JArray jsorted = new JArray(oRes.OrderByDescending(x => (DateTimeOffset?)x["ModifyDate"]));
            JArray jTop = JArray.FromObject(jsorted.Take(5));
            foreach (JObject jSW in jTop)
            {
                LatestItem oSW = new LatestItem();
                oSW.Shortname = jSW["ShortName"].ToString();
                oSW.Version = jSW["ProductVersion"].ToString();
                if (jSW["IconHash"] != null)
                    oSW.IconURL = sURL + "/rest/v2/GetIcon?iconhash=" + jSW["IconHash"].ToString();
                else
                    oSW.IconURL = sURL + "/rest/v2/GetIcon?id=" + jSW["SWId"].ToString();
                oSW.Date = jSW["ModifyDate"].Value<DateTime>().ToString("yyyy-MM-dd");
                lSW.Add(oSW);
            }

            ViewData["LatestItems"] = lSW;
            return View();
        }

        [AllowAnonymous]
        public IActionResult About()
        {
            ViewBag.appVersion = typeof(HomeController).Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>().Version;
            ViewData["Message"] = "About RuckZuck.tools.";

            return View();
        }

        [AllowAnonymous]
        [Route("rss.aspx")]
        [Route("rss")]
        public IActionResult RSS()
        {
            Stream stream = new MemoryStream();
            XmlTextWriter feedWriter = new XmlTextWriter(stream, Encoding.ASCII);

            feedWriter.WriteStartDocument();
            // These are RSS Tags
            feedWriter.WriteStartElement("rss");
            feedWriter.WriteAttributeString("version", "2.0");
            feedWriter.WriteAttributeString("xmlns:atom", "http://www.w3.org/2005/Atom");
            feedWriter.WriteStartElement("channel");
            feedWriter.WriteElementString("title", "RuckZuck Software Package Manager");
            feedWriter.WriteElementString("link", "https://ruckzuck.tools");
            feedWriter.WriteElementString("description", "recent software repository changes");
            feedWriter.WriteElementString("language", "en-us");
            feedWriter.WriteElementString("copyright", "Copyright 2019 ruckzuck.tools. All rights reserved.");
            feedWriter.WriteElementString("lastBuildDate", DateTime.Now.ToString("R"));
            feedWriter.WriteRaw("<atom:link xmlns:atom=\"http://www.w3.org/2005/Atom\" href=\"https://ruckzuck.azureedge.net/rss.aspx\" rel=\"self\" type=\"application/rss+xml\" />");
            feedWriter.WriteElementString("ttl", "10");
            feedWriter.Flush();

            JArray oRes = Base.GetCatalog("", false);
            JArray jsorted = new JArray(oRes.OrderByDescending(x => (DateTimeOffset?)x["ModifyDate"]));
            JArray jTop = JArray.FromObject(jsorted.Take(30));
            foreach (JObject jSW in jTop)
            {
                string sImgUrl = string.Format("https://ruckzuck.azureedge.net/rest/v2/GetIcon?iconhash={0}", jSW["IconHash"].Value<string>());
                //string sImg = "&lt;img src=\"" + sImgUrl + "\" height=\"64\" width=\"64\" /&gt;";
                string sImg = "<img src=\"" + sImgUrl + "\" height=\"64\" width=\"64\" /></br>";
                //string sImg = "<![CDATA[<img src=\"" + sImgUrl + "\" height=\"64\" width=\"64\" /> ]]>";
                feedWriter.WriteStartElement("item");
                feedWriter.WriteElementString("title", jSW["ShortName"].Value<string>() + " " + jSW["ProductVersion"].Value<string>());
                //feedWriter.WriteCData(sImg);

                feedWriter.WriteElementString("description", sImg + jSW["Description"].Value<string>());
                feedWriter.WriteElementString("pubDate", jSW["Timestamp"].Value<DateTime>().ToString("R"));
                feedWriter.WriteElementString("link", "https://ruckzuck.tools");
                feedWriter.WriteElementString("guid", sImgUrl);
                feedWriter.WriteEndElement();
            }

            // Close all open tags tags
            feedWriter.WriteEndElement();
            feedWriter.WriteEndElement();
            feedWriter.WriteEndDocument();
            feedWriter.Flush();
            stream.Flush();
            stream.Position = 0;

            StreamReader reader = new StreamReader(stream);
            string sXML = reader.ReadToEnd();
            feedWriter.Close();

            return Content(sXML, "text/xml");

        }

        [AllowAnonymous]
        public IActionResult Repository()
        {
            ViewBag.appVersion = typeof(HomeController).Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>().Version;
            ViewData["Message"] = "RuckZuck Software Repository";

            string sURL = Request.GetEncodedUrl().ToLower().TrimEnd('/');
            sURL = sURL.Split("/home")[0];

            List<RepositoryItem> lSW = new List<RepositoryItem>();
            JArray oRes = Base.GetCatalog("", false);
            JArray jsorted = new JArray(oRes.OrderBy(x => (string)x["ShortName"]));
            //JArray jTop = JArray.FromObject(jsorted.Take(5));
            foreach (JObject jSW in jsorted)
            {
                try
                {
                    RepositoryItem oSW = new RepositoryItem();
                    oSW.Shortname = jSW["ShortName"].ToString();
                    oSW.Version = jSW["ProductVersion"].ToString();
                    if (!string.IsNullOrEmpty(jSW["IconHash"].Value<string>()))
                        oSW.IconURL = sURL + "/rest/v2/GetIcon?iconhash=" + jSW["IconHash"].ToString();
                    else
                    {
                        if (jSW["SWId"] != null)
                            oSW.IconURL = sURL + "/rest/v2/GetIcon?iconid=" + jSW["SWId"].ToString();
                        else
                            oSW.IconURL = sURL + "/rest/v2/GetIcon?iconid=" + jSW["IconId"].ToString();
                    }
                    //oSW.Date = jSW["ModifyDate"].Value<DateTime>().ToString("yyyy-MM-dd");
                    oSW.Manufacturer = jSW["Manufacturer"].ToString();
                    oSW.Description = jSW["Description"].ToString();
                    oSW.ProductURL = jSW["ProductURL"].ToString();
                    lSW.Add(oSW);
                }
                catch { }
            }

            ViewData["RepositoryItems"] = lSW;

            return View();
        }

        [AllowAnonymous]
        public IActionResult Support()
        {
            ViewBag.appVersion = typeof(HomeController).Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>().Version;

            return View();
        }

        [AllowAnonymous]
        public IActionResult Privacy()
        {
            ViewBag.appVersion = typeof(HomeController).Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>().Version;
            return View();
        }

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        public class LatestItem
        {
            public string Shortname { get; set; }
            public string Version { get; set; }
            public string IconURL { get; set; }

            public string Date { get; set; }
        }

        public class RepositoryItem
        {
            public string Shortname { get; set; }
            public string Version { get; set; }
            public string IconURL { get; set; }
            public string Date { get; set; }
            public string Manufacturer { get; set; }
            public string Description { get; set; }
            public string ProductURL { get; set; }
        }

        [AllowAnonymous]
        public ActionResult ReloadLatest()
        {
            string sURL = Request.GetEncodedUrl().ToLower().TrimEnd('/');
            sURL = sURL.Split("/home")[0];

            List<LatestItem> lSW = new List<LatestItem>();
            JArray oRes = Base.GetCatalog("", false);
            JArray jsorted = new JArray(oRes.OrderByDescending(x => (DateTimeOffset?)x["ModifyDate"]));
            JArray jTop = JArray.FromObject(jsorted.Take(5));
            foreach (JObject jSW in jTop)
            {
                LatestItem oSW = new LatestItem();
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

        [AllowAnonymous]
        public ActionResult ReloadCatalog()
        {
            string sURL = Request.GetEncodedUrl().ToLower().TrimEnd('/');
            sURL = sURL.Split("/home")[0];

            List<RepositoryItem> lSW = new List<RepositoryItem>();
            JArray oRes = Base.GetCatalog("", false);
            JArray jsorted = new JArray(oRes.OrderBy(x => (string)x["ShortName"]));
            //JArray jTop = JArray.FromObject(jsorted.Take(5));
            foreach (JObject jSW in jsorted)
            {
                RepositoryItem oSW = new RepositoryItem();
                oSW.Shortname = jSW["ShortName"].ToString();
                oSW.Version = jSW["ProductVersion"].ToString();
                if (jSW["IconHash"] != null)
                    oSW.IconURL = sURL + "/rest/v2/GetIcon?iconhash=" + jSW["IconHash"].ToString();
                else
                    oSW.IconURL = sURL + "/rest/v2/GetIcon?id=" + jSW["SWId"].ToString();
                oSW.Date = jSW["ModifyDate"].Value<DateTime>().ToString("yyyy-MM-dd");
                oSW.Manufacturer = jSW["Manufacturer"].ToString();
                oSW.Description = jSW["Description"].ToString();
                oSW.ProductURL = jSW["ProductURL"].ToString();
                lSW.Add(oSW);
            }

            return Json(lSW);
        }
    }
}
