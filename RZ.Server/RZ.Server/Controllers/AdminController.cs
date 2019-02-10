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

namespace RZ.Server.Controllers
{
    [Authorize]
    public class AdminController : Controller
    {
        public IConfiguration Configuration { get; }
        public IHostingEnvironment Env { get; }
        private readonly IHubContext<Default> _hubContext;
        private IMemoryCache _cache;

        public AdminController(IHostingEnvironment env, IConfiguration configuration, IHubContext<Default> hubContext, IMemoryCache memoryCache)
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
                    _hubContext.Clients.All.SendAsync("Append", "<li class=\"list-group-item list-group-item-warning\">%tt% - SW Approved: " + sApp + "</li>");
                    _hubContext.Clients.All.SendAsync("Reload");
                    Base.SendNotification("Software Approved:" + sApp, "");
                    Base.Approve(sApp);
                    Base.GetCatalog("", true);
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

#if DEBUG
        [AllowAnonymous]
#endif
        [Authorize]
        [Route("Admin/importlookup")]
        public void ImportLookup()
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();
            builder.DataSource = "xdiv5qhm5h.database.windows.net";
            builder.UserID = "rzander@xdiv5qhm5h";
            builder.Password = "Kerb7eros";
            builder.InitialCatalog = "RuckZuck_pub";

            using (SqlConnection connection = new SqlConnection(builder.ConnectionString))
            {
                Console.WriteLine("\nQuery data example:");
                Console.WriteLine("=========================================\n");

                connection.Open();
                StringBuilder sb = new StringBuilder();
                sb.Append("SELECT [Manufacturer],[ProductName],[Version],[ShortName] ");
                sb.Append("  FROM [dbo].[SWVersions] WHERE Shortname is not null and Shortname != '' and (IsLatest != 1 or IsLatest is null)");

                String sql = sb.ToString();

                using (SqlCommand command = new SqlCommand(sql, connection))
                {
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            Console.WriteLine("{0} {1} {2} {3}", reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3));
                            Base.SetShortname(reader.GetString(1), reader.GetString(2), reader.GetString(0), reader.GetString(3));
                        }
                    }
                }
            }
        }

        //oboslete
        //[Route("Admin/importlatest")]
        //public void ImportLatest()
        //{
        //    SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();
        //    builder.DataSource = "xdiv5qhm5h.database.windows.net";
        //    builder.UserID = "rzander@xdiv5qhm5h";
        //    builder.Password = "Kerb7eros";
        //    builder.InitialCatalog = "RuckZuck_pub";

        //    using (SqlConnection connection = new SqlConnection(builder.ConnectionString))
        //    {
        //        Console.WriteLine("\nQuery data example:");
        //        Console.WriteLine("=========================================\n");

        //        connection.Open();
        //        StringBuilder sb = new StringBuilder();
        //        sb.Append("SELECT [Id],[ShortName] ");
        //        sb.Append("  FROM [dbo].[SWVersions] WHERE IsLatest = 1 ORDER BY [LastModified] DESC");

        //        String sql = sb.ToString();

        //        var SWList = new List<KeyValuePair<string, string>>();

        //        using (SqlCommand command = new SqlCommand(sql, connection))
        //        {
        //            using (SqlDataReader reader = command.ExecuteReader())
        //            {
        //                while (reader.Read())
        //                {
        //                    SWList.Add(new KeyValuePair<string, string>(reader.GetInt64(0).ToString(), reader.GetString(1)));
        //                }
        //            }
        //        }

        //        foreach (var Item in SWList)
        //        {
        //            JArray jResult = new JArray();
        //            sb = new StringBuilder();
        //            sb.Append("SELECT [Definition] ");
        //            sb.Append("  FROM [dbo].[SWDetails] WHERE SWId = " + Item.Key);

        //            using (SqlCommand command = new SqlCommand(sb.ToString(), connection))
        //            {
        //                using (SqlDataReader reader = command.ExecuteReader())
        //                {
        //                    while (reader.Read())
        //                    {
        //                        Console.WriteLine("{0} {1}", Item.Key, Item.Value);
        //                        jResult.Add(JObject.Parse(reader.GetString(0)));
        //                    }
        //                }
        //            }

        //            bool bRes = Base.UploadSoftware(jResult);
        //        }
        //    }
        //}

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
        [Route("Admin/importcatalog")]
        public void importcatalog()
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();
            builder.DataSource = "xdiv5qhm5h.database.windows.net";
            builder.UserID = "rzander@xdiv5qhm5h";
            builder.Password = "Kerb7eros";
            builder.InitialCatalog = "RuckZuck_pub";

            using (SqlConnection connection = new SqlConnection(builder.ConnectionString))
            {
                Console.WriteLine("\nQuery data example:");
                Console.WriteLine("=========================================\n");

                connection.Open();
                StringBuilder sb = new StringBuilder();
                sb.Append(" SELECT * FROM [dbo].[v_SWVersionsLatest] ORDER BY [LastModified] DESC");
                //sb.Append("  FROM [dbo].[SWVersions] WHERE Shortname is not null and Shortname != '' and (IsLatest != 1 or IsLatest is null)");

                String sql = sb.ToString();

                using (SqlCommand command = new SqlCommand(sql, connection))
                {
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            try
                            {
                                JObject jEntity = new JObject();
                                jEntity.Add("Manufacturer", Base.clean(reader["Manufacturer"] as string)); //Azure Table is case sensitive
                                jEntity.Add("ProductName", Base.clean(reader["ProductName"] as string)); //Azure Table is case sensitive
                                jEntity.Add("ProductVersion", Base.clean(reader["Version"] as string)); //Azure Table is case sensitive

                                jEntity.Add("ShortName", (reader["ShortName"] as string));
                                jEntity.Add("shortname", (reader["ShortName"] as string).ToLower()); //Azure Table is case sensitive
                                jEntity.Add("Description", reader["ProductDescription"] as string);
                                jEntity.Add("ProductURL", reader["ProjectURL"] as string);
                                jEntity.Add("Category", reader["Category"] as string);
                                jEntity.Add("IconId", reader["Id"] as long?);
                                jEntity.Add("Downloads", reader["Downloads"] as long?);
                                jEntity.Add("Success", reader["Success"] as long?);
                                jEntity.Add("Failures", reader["Failures"] as long?);

                                jEntity.Add("IsLatest", reader["IsLatest"] as bool?);

                                if (jEntity["Category"].Value<string>() == null)
                                {
                                    jEntity["Category"] = "Other";
                                }

                                string sID = (jEntity["Manufacturer"].ToString().ToLower() + jEntity["ProductName"].ToString().ToLower() + jEntity["ProductVersion"].ToString().ToLower()).Trim();
                                Console.WriteLine(sID);

                                string sRowKey = Hash.CalculateMD5HashString(sID);

                                //InsertEntityAsync("https://ruckzuck.table.core.windows.net/catalog?st=2019-02-08T19%3A20%3A22Z&se=2019-03-31T19%3A20%3A00Z&sp=raud&sv=2018-03-28&tn=catalog&sig=cQpC0tsLFkz6A%2BOs%2FqG4JAyHLTIYG6%2Fw9quNNEc9xUM%3D", "known", sRowKey, jEntity.ToString());
                                MergeEntityAsync("https://ruckzuck.table.core.windows.net/catalog?st=2019-02-08T19%3A20%3A22Z&se=2019-03-31T19%3A20%3A00Z&sp=raud&sv=2018-03-28&tn=catalog&sig=cQpC0tsLFkz6A%2BOs%2FqG4JAyHLTIYG6%2Fw9quNNEc9xUM%3D", "known", sRowKey, jEntity.ToString());

                                //Base.SetShortname(reader.GetString(1), reader.GetString(2), reader.GetString(0), reader.GetString(3));
                            }
                            catch (Exception ex)
                            {
                                ex.Message.ToString();
                            }
                        }
                    }
                }
            }


            //Cleanup old isLatest TBD: PartitionKey eq 'known' and Timestamp lt datetime'2019-01-12T14:18:38.639Z' and IsLatest eq true
        }

#if DEBUG
        [AllowAnonymous]
#endif
        [Authorize]
        public ActionResult Refresh()
        {
            Plugins.loadPlugins(Path.Combine(Env.WebRootPath, "plugins"));
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

        //REMOVE
        //public void importlookup()
        //{
        //    SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();
        //    builder.DataSource = "xdiv5qhm5h.database.windows.net";
        //    builder.UserID = "rzander@xdiv5qhm5h";
        //    builder.Password = "Kerb7eros";
        //    builder.InitialCatalog = "RuckZuck_pub";

        //    using (SqlConnection connection = new SqlConnection(builder.ConnectionString))
        //    {
        //        Console.WriteLine("\nQuery data example:");
        //        Console.WriteLine("=========================================\n");

        //        connection.Open();
        //        StringBuilder sb = new StringBuilder();
        //        //sb.Append(" SELECT * FROM [dbo].[SWVersions] WHERE (IsLatest is NULL or IsLatest = '') AND Shortname = ''");
        //        //sb.Append(" SELECT * FROM [dbo].[SWVersions] WHERE Shortname is not null and Shortname != '' and (IsLatest != 1 or IsLatest is null)");
        //        sb.Append(" SELECT * FROM [dbo].[SWVersions] WHERE Shortname != '' and (IsLatest != 1 or IsLatest is null)");

        //        String sql = sb.ToString();

        //        using (SqlCommand command = new SqlCommand(sql, connection))
        //        {
        //            using (SqlDataReader reader = command.ExecuteReader())
        //            {
        //                while (reader.Read())
        //                {
        //                    JObject jEntity = new JObject();
        //                    jEntity.Add("Manufacturer", Base.clean(reader["Manufacturer"] as string));
        //                    jEntity.Add("ProductName", Base.clean(reader["ProductName"] as string));
        //                    jEntity.Add("ProductVersion", Base.clean(reader["Version"] as string));

        //                    string shortname = reader["ShortName"] as string;
        //                    if(!string.IsNullOrEmpty(shortname))
        //                    {
        //                        jEntity.Add("ShortName", shortname.ToLower());
        //                    }

        //                    if (jEntity["ShortName"] == null)
        //                        jEntity["ShortName"] = "";

        //                    string sID = (jEntity["Manufacturer"].ToString().ToLower() + jEntity["ProductName"].ToString().ToLower() + jEntity["ProductVersion"].ToString().ToLower()).Trim();
        //                    Console.WriteLine(sID);

        //                    string sRowKey = Hash.CalculateMD5HashString(sID);

        //                    if(string.IsNullOrEmpty(shortname))
        //                        InsertEntityAsync("https://ruckzuck.table.core.windows.net/SWLookup?st=2019-01-11T20%3A21%3A50Z&se=2019-01-31T20%3A21%3A00Z&sp=raud&sv=2018-03-28&tn=swlookup&sig=qruiRc4nT3vgtQMzgHsuy0pxsHU8jC%2FwpNswoFm%2FJfE%3D", "lookup", sRowKey, jEntity.ToString());
        //                    else
        //                        UpdateEntityAsync("https://ruckzuck.table.core.windows.net/SWLookup?st=2019-01-11T20%3A21%3A50Z&se=2019-01-31T20%3A21%3A00Z&sp=raud&sv=2018-03-28&tn=swlookup&sig=qruiRc4nT3vgtQMzgHsuy0pxsHU8jC%2FwpNswoFm%2FJfE%3D", "lookup", sRowKey, jEntity.ToString());

        //                    //Base.SetShortname(reader.GetString(1), reader.GetString(2), reader.GetString(0), reader.GetString(3));
        //                }
        //            }
        //        }
        //    }
        //}
    }
}