using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace RZ.SWLookup.Plugin
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

        public static JArray CheckForUpdates(JArray Softwares)
        {
            HttpContent oCont = new StringContent(Softwares.ToString(Formatting.None), Encoding.UTF8, "application/json");
            var tChek = oClient.PostAsync(sURL + "/rest/v2/checkforupdate", oCont);
            tChek.Wait(90000);
            if (tChek.IsCompleted)
            {
                return JArray.Parse(tChek.Result.Content.ReadAsStringAsync().Result);
            }
            else
                return new JArray();
        }

    }
}
