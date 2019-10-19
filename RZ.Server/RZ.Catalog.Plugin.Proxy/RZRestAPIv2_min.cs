using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace RZ.Plugin.Catlog.Proxy
{
    class RZRestAPIv2
    {
        private static string _sURL = "";
        private static HttpClient oClient = new HttpClient(); //thx https://aspnetmonsters.com/2016/08/2016-08-27-httpclientwrong/
        public static string CustomerID = "";

        public static string sURL
        {
            get
            {
                if (!string.IsNullOrEmpty(_sURL))
                    return _sURL;

                    _sURL = GetURL("");
                    return _sURL;
            }
            set
            {
                _sURL = value;
            }
        }

        public static string GetURL(string customerid)
        {
            using (HttpClient hClient = new HttpClient())
            {
                try
                {
                    Task<string> tReq;

                    if (string.IsNullOrEmpty(CustomerID))
                    {
                        using (HttpClient qClient = new HttpClient())
                        {
                            CustomerID = hClient.GetStringAsync("https://ruckzuck.tools/rest/v2/getip").Result;
                            customerid = CustomerID.ToString();
                        }
                    }


                    if (string.IsNullOrEmpty(customerid))
                    {
                        tReq = hClient.GetStringAsync("https://ruckzuck.tools/rest/v2/geturl");
                    }
                    else
                        tReq = hClient.GetStringAsync("https://ruckzuck.tools/rest/v2/geturl?customerid=" + customerid);



                    tReq.Wait(5000); //wait max 5s

                    if (tReq.IsCompleted)
                    {
                        _sURL = tReq.Result;
                        return _sURL;
                    }
                    else
                    {
                        _sURL = "https://ruckzuck.azurewebsites.net";
                        return _sURL;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("ERROR 145: " + ex.Message);
                }

                return "https://ruckzuck.azurewebsites.net";
            }
        }

        public static JArray GetCatalog(string customerid, string wwwpath)
        {
            if (string.IsNullOrEmpty(customerid))
            {
                if (File.Exists(Path.Combine(Environment.ExpandEnvironmentVariables(wwwpath), "rzcat.json"))) //Cached content exists
                {
                    try
                    {
                        DateTime dCreationDate = File.GetLastWriteTime(Path.Combine(Environment.ExpandEnvironmentVariables(wwwpath), "rzcat.json"));
                        if ((DateTime.Now - dCreationDate) < new TimeSpan(0, 30, 0)) //Cache for 30min
                        {
                            //return cached Content
                            string jRes = File.ReadAllText(Path.Combine(Environment.ExpandEnvironmentVariables(wwwpath), "rzcat.json"));
                            return JArray.Parse(jRes); ;
                        }
                    }
                    catch (Exception ex)
                    {
                        //Debug.WriteLine("E1" + ex.Message, "GetCatalog");
                    }
                }
            }

            try
            {
                Task<string> response;
                if (string.IsNullOrEmpty(customerid))
                    response = oClient.GetStringAsync(sURL + "/rest/v2/GetCatalog");
                else
                    response = oClient.GetStringAsync(sURL + "/rest/v2/GetCatalog?customerid=" + customerid);

                response.Wait(60000); //60s max

                if (response.IsCompleted)
                {
                    JArray lRes = JArray.Parse(response.Result);

                    if (string.IsNullOrEmpty(customerid) && lRes.Count > 400)
                    {
                        File.WriteAllText(Path.Combine(Environment.ExpandEnvironmentVariables(wwwpath), "rzcat.json"), response.Result);
                    }

                    return lRes;
                }
            }
            catch (Exception ex)
            {
                //Debug.WriteLine("E2" + ex.Message, "GetCatalog");
            }

            return new JArray();
        }

        public class GetSoftware
        {
            public string ProductName { get; set; }

            public string Manufacturer { get; set; }

            public string Description { get; set; }

            public string ShortName { get; set; }

            public string ProductURL { get; set; }

            public string ProductVersion { get; set; }

            public Int32? Downloads { get; set; }

            public List<string> Categories { get; set; }

            public long SWId { get; set; }

            public string IconHash { get; set; }

            public bool isInstalled { get; set; }


            public string IconURL
            {
                get
                {
                    //Support new V2 REST API
                    if (!string.IsNullOrEmpty(IconHash))
                    {
                        return RZRestAPIv2.sURL + "/rest/v2/GetIcon?iconhash=" + IconHash;
                    }

                    if (SWId > 0)
                    {
                        return RZRestAPIv2.sURL + "/rest/GetIcon?id=" + SWId.ToString();
                    }

                    return "";
                }
            }

        }
    }
}
