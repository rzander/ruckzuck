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
using System.Runtime.Caching;

namespace IP2Location
{
    public static class IP2Location
    {
        static MemoryCache memoryCache = MemoryCache.Default;

        [FunctionName("IP2Location")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            //log.LogInformation("C# HTTP trigger function processed a request.");

            string ip = req.Query["ip"];

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            ip = ip ?? data?.ip ?? GetIpFromRequestHeaders(req);

            DateTime dStart = DateTime.Now;
            string result = "";
            if (!string.IsNullOrEmpty(ip))
            {
                result = memoryCache[ip] as string;
                if (string.IsNullOrEmpty(result))
                {
                    long longip = IP2Long(ip);
                    var Locs = GetLocations(longip);
                    var oFirst = Locs.Where(t => t.loIP <= longip & t.hiIP >= longip);
                    result = JsonConvert.SerializeObject(oFirst.FirstOrDefault());

                    memoryCache.Set(ip, result, DateTimeOffset.Now.AddHours(4));

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
        private static List<location> ReadCSVBlob(string Filename)
        {
            string BlobURL = Environment.GetEnvironmentVariable("BlobURL");
            string SASToken = Environment.GetEnvironmentVariable("BlobToken").TrimStart('?');

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
