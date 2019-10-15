using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Web;
using System.Xml;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace UpdateCounters
{
    public static class Function1
    {
        [FunctionName("UpdateCounters")]
        public static void Run([TimerTrigger("0 */5 * * * *")]TimerInfo myTimer, ILogger log)
        {
            log.LogInformation($"UpdateCounters started at: {DateTime.Now}");

            ProcessDLQueue(log);
            ProcessFailureQueue(log);
            ProcessSuccessQueue(log);
        }

        private static void ProcessDLQueue(ILogger log)
        {
            string sURL = Environment.GetEnvironmentVariable("dlSAS").Split('?')[0];
            string sasToken = Environment.GetEnvironmentVariable("dlSAS").Substring(sURL.Length + 1);

            List<string> DLQueue = new List<string>();
            Dictionary<string, string> IDQueue = new Dictionary<string, string>();
            int iMessageCount = 33;

            //Get bulk of 32 Messages
            while (iMessageCount >= 32)
            {
                using (HttpClient oClient = new HttpClient())
                {
                    string url = $"{sURL}/messages?numofmessages=32&{sasToken}";
                    var oRes = oClient.GetStringAsync(url);
                    oRes.Wait();
                    string sXML = oRes.Result.ToString();
                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.LoadXml(sXML);
                    iMessageCount = xmlDoc.SelectNodes("QueueMessagesList/QueueMessage").Count;

                    foreach (XmlNode xNode in xmlDoc.SelectNodes("QueueMessagesList/QueueMessage"))
                    {
                        string sMessageID = xNode["MessageId"].InnerText;
                        string ShortName = xNode["MessageText"].InnerText;
                        string sPopReceipt = xNode["PopReceipt"].InnerText;
                        DLQueue.Add(ShortName);
                        IDQueue.Add(sMessageID, sPopReceipt);
                    }
                }
            }

            var counts = DLQueue.GroupBy(x => x)
                 .ToDictionary(g => g.Key, g => g.Count());

            foreach (var item in counts)
            {
                log.LogInformation($"Downloads: {item.Key} + {item.Value} : {DateTime.Now}");
                Inc(item.Key, item.Value, "known", "Downloads");
            }

            foreach (var sID in IDQueue)
            {
                using (HttpClient oClient = new HttpClient())
                {
                    string url = $"{sURL}/messages/{sID.Key}?{sasToken}&popreceipt={HttpUtility.UrlEncode(sID.Value)}";
                    var oRes = oClient.DeleteAsync(url);
                    oRes.Wait();
                    oRes.Result.ToString();
                }
            }
        }

        private static void ProcessFailureQueue(ILogger log)
        {
            string sURL = Environment.GetEnvironmentVariable("faSAS").Split('?')[0];
            string sasToken = Environment.GetEnvironmentVariable("faSAS").Substring(sURL.Length + 1);

            List<string> DLQueue = new List<string>();
            Dictionary<string, string> IDQueue = new Dictionary<string, string>();
            int iMessageCount = 33;

            //Get bulk of 32 Messages
            while (iMessageCount >= 32)
            {
                using (HttpClient oClient = new HttpClient())
                {
                    string url = $"{sURL}/messages?numofmessages=32&{sasToken}";
                    var oRes = oClient.GetStringAsync(url);
                    oRes.Wait();
                    string sXML = oRes.Result.ToString();
                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.LoadXml(sXML);
                    iMessageCount = xmlDoc.SelectNodes("QueueMessagesList/QueueMessage").Count;

                    foreach (XmlNode xNode in xmlDoc.SelectNodes("QueueMessagesList/QueueMessage"))
                    {
                        string sMessageID = xNode["MessageId"].InnerText;
                        string ShortName = xNode["MessageText"].InnerText;
                        string sPopReceipt = xNode["PopReceipt"].InnerText;
                        DLQueue.Add(ShortName);
                        IDQueue.Add(sMessageID, sPopReceipt);
                    }
                }
            }

            var counts = DLQueue.GroupBy(x => x)
                 .ToDictionary(g => g.Key, g => g.Count());

            foreach (var item in counts)
            {
                log.LogInformation($"Failures: {item.Key} + {item.Value} : {DateTime.Now}");
                Inc(item.Key, item.Value, "known", "Failures");
            }

            foreach (var sID in IDQueue)
            {
                using (HttpClient oClient = new HttpClient())
                {
                    string url = $"{sURL}/messages/{sID.Key}?{sasToken}&popreceipt={HttpUtility.UrlEncode(sID.Value)}";
                    var oRes = oClient.DeleteAsync(url);
                    oRes.Wait();
                    oRes.Result.ToString();
                }
            }
        }

        private static void ProcessSuccessQueue(ILogger log)
        {
            string sURL = Environment.GetEnvironmentVariable("suSAS").Split('?')[0];
            string sasToken = Environment.GetEnvironmentVariable("suSAS").Substring(sURL.Length + 1);

            List<string> DLQueue = new List<string>();
            Dictionary<string, string> IDQueue = new Dictionary<string, string>();
            int iMessageCount = 33;

            //Get bulk of 32 Messages
            while (iMessageCount >= 32)
            {
                using (HttpClient oClient = new HttpClient())
                {
                    string url = $"{sURL}/messages?numofmessages=32&{sasToken}";
                    var oRes = oClient.GetStringAsync(url);
                    oRes.Wait();
                    string sXML = oRes.Result.ToString();
                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.LoadXml(sXML);
                    iMessageCount = xmlDoc.SelectNodes("QueueMessagesList/QueueMessage").Count;

                    foreach (XmlNode xNode in xmlDoc.SelectNodes("QueueMessagesList/QueueMessage"))
                    {
                        string sMessageID = xNode["MessageId"].InnerText;
                        string ShortName = xNode["MessageText"].InnerText;
                        string sPopReceipt = xNode["PopReceipt"].InnerText;
                        DLQueue.Add(ShortName);
                        IDQueue.Add(sMessageID, sPopReceipt);
                    }
                }
            }

            var counts = DLQueue.GroupBy(x => x)
                 .ToDictionary(g => g.Key, g => g.Count());

            foreach (var item in counts)
            {
                log.LogInformation($"Success: {item.Key} + {item.Value} : {DateTime.Now}");
                Inc(item.Key, item.Value, "known", "Success");
            }

            foreach (var sID in IDQueue)
            {
                using (HttpClient oClient = new HttpClient())
                {
                    string url = $"{sURL}/messages/{sID.Key}?{sasToken}&popreceipt={HttpUtility.UrlEncode(sID.Value)}";
                    var oRes = oClient.DeleteAsync(url);
                    oRes.Wait();
                    oRes.Result.ToString();
                }
            }
        }

        private static void Inc(string ShortName, int count, string PartKey = "known", string AttributeName = "Downloads")
        {
            string sURL = Environment.GetEnvironmentVariable("catSAS").Split('?')[0];
            string sasToken = Environment.GetEnvironmentVariable("catSAS").Substring(sURL.Length + 1);
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(sURL + "()?$filter=PartitionKey eq '" + PartKey + "' and shortname eq '" + WebUtility.UrlEncode(ShortName.ToLower()) + "' and IsLatest eq true&$select=PartitionKey,RowKey," + AttributeName + "&" + sasToken);

                request.Method = "GET";
                request.Headers.Add("x-ms-version", "2017-04-17");
                request.Headers.Add("x-ms-date", DateTime.Now.ToUniversalTime().ToString("R"));
                request.Accept = "application/json";
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                var content = string.Empty;
                string ETag = "";
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

                JArray jResult = jres["value"] as JArray;
                ETag = jResult[0]["odata.etag"].ToString();
                string PartitionKey = jResult[0]["PartitionKey"].ToString();
                string RowKey = jResult[0]["RowKey"].ToString();
                int iCount = jResult[0][AttributeName].Value<int>();
                iCount = iCount+count;
                JObject jUpd = new JObject();
                jUpd.Add(AttributeName, iCount);

                MergeEntityAsync(Environment.GetEnvironmentVariable("catSAS"), PartitionKey, RowKey, jUpd.ToString(), ETag);
            }
            catch { }

        }

        private static void MergeEntityAsync(string url, string PartitionKey, string RowKey, string JSON, string ETag = "*")
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
                    oClient.DefaultRequestHeaders.Add("If-Match", ETag);
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
        }
    }
}
