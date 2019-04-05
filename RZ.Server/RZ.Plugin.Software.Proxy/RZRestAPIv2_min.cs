using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Plugin_Software
{
    class RZRestAPIv2
    {
        private static string _sURL = "";
        private static HttpClient oClient = new HttpClient(); //thx https://aspnetmonsters.com/2016/08/2016-08-27-httpclientwrong/

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
                Task<string> tReq;
                if (string.IsNullOrEmpty(customerid))
                    tReq = hClient.GetStringAsync("https://ruckzuck.tools/rest/v2/geturl");
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
        }

        public static Task<Stream> GetIcon(Int32 iconid = 0, string iconhash = "")
        {
            string IcoURL = sURL + "/rest/v2/geticon?iconhash=" + iconhash;

            if (string.IsNullOrEmpty(iconhash))
                IcoURL = sURL + "/rest/v2/geticon?iconid=" + iconid.ToString();

            return oClient.GetStreamAsync(IcoURL);

        }

        public static JArray GetSoftwares(string shortname, string customerid)
        {
            string sRes = oClient.GetStringAsync(sURL + "/rest/v2/getsoftwares?shortname=" + WebUtility.UrlEncode(shortname) + "&customerid=" + WebUtility.UrlEncode(customerid)).Result;
            return JArray.Parse(sRes);
        }

        public static JArray GetSoftwares(string name = "", string ver = "", string man = "_unknown", string customerid = "")
        {
            string sRes = oClient.GetStringAsync(sURL + "/rest/v2/getsoftwares?name=" + WebUtility.UrlEncode(name) + "&ver=" + WebUtility.UrlEncode(ver) + "&man=" + WebUtility.UrlEncode(man)).Result;
            return JArray.Parse(sRes);
        }

        public static bool UploadSoftware(JArray Software)
        {
            HttpContent oCont = new StringContent(Software.ToString(Formatting.None));

            var oStat = oClient.PutAsync(sURL + "/rest/v2/uploadsoftware", oCont);
            oStat.Wait(10000);

            if (oStat.IsCompleted)
                return true;
            else
                return false;
        }

        public static bool IncCounter(string shortname = "", string counter = "DL")
        {
            var oStat = oClient.GetAsync(sURL + "/rest/v2/IncCounter/" + WebUtility.UrlEncode(shortname) + "/" + WebUtility.UrlEncode(counter));
            oStat.Wait(10000);

            if (oStat.IsCompleted)
                return true;
            else
                return false;
        }
    }
}
