using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Linq;
using System.Globalization;
using System.Net.Http;
using System.Security.Cryptography;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;
using System.Runtime.Caching;

namespace RZ.ServerFN
{
    public static class IP2Location
    {
        private static MemoryCache memoryCache = MemoryCache.Default;

        public static Dictionary<string, string> Settings { get; set; }

        [FunctionName("IP2Location")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            //log.LogInformation("C# HTTP trigger function processed a request.");

            string ip = req.Query["ip"];
            string sLong = req.Query["long"];
            string sLat = req.Query["lat"];
            string NoCache = req.Query["nocache"];
            bool bNoCache = false;

            if (NoCache != null && NoCache.ToLower() == "true")
                bNoCache = true;

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            ip = ip ?? data?.ip ?? GetIpFromRequestHeaders(req);

            if (!string.IsNullOrEmpty(sLong + sLat))
            {
                try
                {
                    sLong = sLong.Replace(',', '.');
                    sLat = sLat.Replace(',', '.');
                    var Locs = GetLocations(double.Parse(sLong), double.Parse(sLat), bNoCache);

                    if (Locs.Count > 0)
                    {
                        if (!string.IsNullOrEmpty(Locs[0].Location))
                        {
                            string sresult = JsonConvert.SerializeObject(Locs.FirstOrDefault());

                            if (!string.IsNullOrEmpty(sresult))
                            {
                                memoryCache.Set(ip + sLong + sLat, sresult, new CacheItemPolicy() { AbsoluteExpiration = new DateTimeOffset().AddHours(1) });

                                return ip != null
                                    ? (ActionResult)new OkObjectResult(sresult)
                                    : new BadRequestObjectResult("Please pass an IP on the query string or in the request body");
                            }
                        }
                        else
                        {
                            //string sresult = JsonConvert.SerializeObject(Locs.FirstOrDefault());

                            //if (!string.IsNullOrEmpty(sresult))
                            //{
                            //    memoryCache.Set(ip, sresult, new CacheItemPolicy() { SlidingExpiration = new TimeSpan(8, 0, 0) });
                            //}
                        }

                    }
                }
                catch { }
            }


            DateTime dStart = DateTime.Now;
            string result = "";
            if (!string.IsNullOrEmpty(ip))
            {
                if(!bNoCache)
                    result = memoryCache.Get(ip + sLong + sLat) as string; // memoryCache[ip] as string;
                
                if (string.IsNullOrEmpty(result))
                {
                    long longip = IP2Long(ip);
                    var Locs = GetLocations(longip);
                    var oFirst = Locs.Where(t => t.loIP <= longip & t.hiIP >= longip);
                    result = JsonConvert.SerializeObject(oFirst.FirstOrDefault());

                    memoryCache.Set(ip + sLong + sLat, result, new CacheItemPolicy() { AbsoluteExpiration = new DateTimeOffset().AddHours(1) });
                    //memoryCache.Set(ip, result, DateTimeOffset.Now.AddHours(4));

                }

                log.LogInformation($"IP lookup duration for {ip} was {Math.Round((DateTime.Now - dStart).TotalMilliseconds, 0, MidpointRounding.AwayFromZero)}ms");
            }

            return ip != null
                ? (ActionResult)new OkObjectResult(result)
                : new BadRequestObjectResult("Please pass an IP on the query string or in the request body");
        }

        private static List<location> GetLocations(long IP)
        {
            string lIP = IP.ToString("D10");
            int iFolder = int.Parse(lIP.Substring(0, 2));
            int iPrefix = int.Parse(lIP.Substring(2, 3));
            string sFile = iPrefix.ToString("D3") + "_IPLocation.csv";
            string sPath = @"";


            string sCSV = Path.Combine(sPath, iFolder.ToString("D2"), sFile).Replace('\\', '/');

            List<location> Loc = ReadCSVBlob(sCSV);
            if (Loc.Count(t => t.loIP <= IP & t.hiIP >= IP) == 0)
            {
                Loc.Clear();
            }
            while (Loc.Count == 0)
            {
                iPrefix--;
                sCSV = Path.Combine(sPath, iFolder.ToString("D2"), iPrefix.ToString("D3") + "_IPLocation.csv").Replace('\\', '/');
                Loc = ReadCSVBlob(sCSV);
                if (Loc.Count(t => t.loIP <= IP & t.hiIP >= IP) == 0)
                {
                    Loc.Clear();
                }
            }


            return Loc;
        }
        private static List<location> GetLocations(double longitude, double latitude, bool nocache)
        {
            List<location> lResult = new List<location>();
            location Loc = new location();
            int decimals = 3;
            string TableURL = Environment.GetEnvironmentVariable("TableURL");
            string TableToken = Environment.GetEnvironmentVariable("TableToken").TrimStart('?');
            try
            {
                if (longitude > 180 || latitude > 180)
                    return lResult;

                string sLong = Math.Round(longitude, decimals).ToString();
                string sLat = Math.Round(latitude, decimals).ToString();
                JArray aLoc = null;
                if (!nocache)
                {
                    aLoc = new JArray();
                    aLoc = memoryCache.Get(sLong + sLat) as JArray;
                }

                if (aLoc == null)
                {
                    aLoc = getEntities(TableURL + "?" + TableToken, sLat, sLong);
                    if (aLoc == null)
                        aLoc = new JArray();
                    memoryCache.Set(sLong + sLat, aLoc, DateTimeOffset.Now.AddHours(4));
                }
                while ((aLoc == null ||!aLoc.HasValues) && decimals > 0)
                {
                    decimals--;
                    sLong = Math.Round(longitude, decimals).ToString();
                    sLat = Math.Round(latitude, decimals).ToString();
                    
                    if(!nocache)
                        aLoc = memoryCache.Get(sLong + sLat) as JArray;

                    if (aLoc == null || !aLoc.HasValues)
                    {
                        aLoc = getEntities(TableURL + "?" + TableToken, sLat, sLong);
                        if (decimals <= 2) {
                            if (aLoc == null || !aLoc.HasValues)
                            {
                                InsertEntityAsync(TableURL + "?" + TableToken, sLat, sLong, "{ \"Country\":\"\", \"ISO\":\"\", \"Location\":\"\", \"State\":\"\", \"TimeZone\":\"\", \"ZIP\":\"\"}");
                            }
                        }
                        memoryCache.Set(sLong + sLat, aLoc, DateTimeOffset.Now.AddHours(4));
                    }
                }

                if (aLoc.HasValues)
                {
                    var oFirst = aLoc[0];
                    Loc.Country = oFirst["Country"].Value<string>();
                    Loc.ISO = oFirst["ISO"].Value<string>();
                    Loc.Location = oFirst["Location"].Value<string>();
                    Loc.State = oFirst["State"].Value<string>();
                    Loc.TimeZone = oFirst["TimeZone"].Value<string>();
                    Loc.ZIP = oFirst["ZIP"].Value<string>();
                    Loc.Long = sLong;
                    Loc.Lat = sLat;

                    lResult.Add(Loc);
                }
            }
            catch { }
            return lResult;
        }
        private static List<location> ReadCSVBlob(string Filename)
        {
            string BlobURL = Environment.GetEnvironmentVariable("BlobURL");
            string SASToken = Environment.GetEnvironmentVariable("BlobToken").TrimStart('?');

            //string BlobURL = Settings["ipURL"];
            //string SASToken = Settings["ipSAS"].TrimStart('?');

            List<location> Locations = new List<location>();

            if (string.IsNullOrEmpty(BlobURL)) //exit if no BlobURL is defined
                return Locations;

            try
            {
                var csvStream = new MemoryStream(new WebClient().DownloadData(BlobURL + "/" + Filename + "?" + SASToken));

                csvStream.Position = 0; // Rewind!
                List<string> rows = new List<string>();

                using (var reader = new StreamReader(csvStream, Encoding.UTF8))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        var values = line.Split("\",\"");

                        Locations.Add(new location() { loIP = long.Parse(values[0].TrimStart('"')), hiIP = long.Parse(values[1]), ISO = values[2], Country = values[3], State = values[4], Location = values[5], Long = values[7], Lat = values[6], ZIP = values[8], TimeZone = values[9].TrimEnd('"') });
                    }
                }
            }
            catch (Exception ex)
            {
                ex.Message.ToString();
            }
            return Locations;

        }

        private static HttpClient oClient = new HttpClient();
        public static JArray getEntities(string url, string partitionkey, string rowkey = "", string query = "")
        {
            try
            {
                string nextPart = "";
                string nextRow = "";
                string sasToken = url.Substring(url.IndexOf("?") + 1);
                string sURL = url.Substring(0, url.IndexOf("?"));
                HttpWebRequest request = null;

                if (string.IsNullOrEmpty(query))
                {
                    if (string.IsNullOrEmpty(rowkey))
                        request = (HttpWebRequest)WebRequest.Create(sURL + "()?$filter=PartitionKey eq '" + partitionkey + "'&" + sasToken);

                    if (string.IsNullOrEmpty(partitionkey))
                        request = (HttpWebRequest)WebRequest.Create(sURL + "()?$filter=RowKey eq '" + rowkey + "'&" + sasToken);

                    if (!string.IsNullOrEmpty(partitionkey) && !string.IsNullOrEmpty(rowkey))
                        request = (HttpWebRequest)WebRequest.Create(sURL + $"(PartitionKey='{ partitionkey }',RowKey='{ rowkey }')?{ sasToken }");
                }
                else
                {
                    if (query.Length > 1)
                        request = (HttpWebRequest)WebRequest.Create(sURL + "()" + query + "&" + sasToken);
                    else
                        request = (HttpWebRequest)WebRequest.Create(sURL + "()" + "?" + sasToken);
                }
                request.Method = "GET";
                request.Headers.Add("x-ms-version", "2017-04-17");
                request.Headers.Add("x-ms-date", DateTime.Now.ToUniversalTime().ToString("R"));
                request.Accept = "application/json;odata=nometadata";
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                var content = string.Empty;

                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    nextPart = response.Headers["x-ms-continuation-NextPartitionKey"];
                    nextRow = response.Headers["x-ms-continuation-NextRowKey"];
                    using (var stream = response.GetResponseStream())
                    {
                        using (var sr = new StreamReader(stream))
                        {
                            content = sr.ReadToEnd();
                        }
                    }
                }

                var jres = JObject.Parse(content);

                JArray jResult = new JArray();
                jResult.Add(jres);
                //JArray jResult = jres["value"] as JArray;

                return jResult;
            }
            catch (Exception ex)
            {
                ex.Message.ToString();
            }

            return null;
        }
        public static void InsertEntityAsync(string url, string PartitionKey, string RowKey, string JSON)
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

        /// <summary>
        /// IP2Lon from http://geekswithblogs.net/rgupta/archive/2009/04/29/convert-ip-to-long-and-vice-versa-c.aspx
        /// </summary>
        /// <param name="ip"></param>
        /// <returns></returns>
        private static long IP2Long(string ip)
        {
            string[] ipBytes;
            double num = 0;
            if (!string.IsNullOrEmpty(ip))
            {
                ipBytes = ip.Trim().Split('.');
                for (int i = ipBytes.Length - 1; i >= 0; i--)
                {
                    num += ((int.Parse(ipBytes[i]) % 256) * Math.Pow(256, (3 - i)));
                }
            }
            return (long)num;
        }

        private static string GetIpFromRequestHeaders(HttpRequest request)
        {
            return (request.Headers["X-Forwarded-For"].FirstOrDefault() ?? "").Split(new char[] { ':' }).FirstOrDefault();
        }
    }

    class location
    {
        public long loIP;
        public long hiIP;
        public string ISO;
        public string Country;
        public string State;
        public string Location;
        public string Long;
        public string Lat;
        public string ZIP;
        public string TimeZone;
    }
}
