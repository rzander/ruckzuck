using Microsoft.Win32;
using RuckZuck_WCF;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace RuckZuck.Base
{
    class RZRestAPIv2
    {
        private static string _sURL = "UDP";
        public static bool DisableBroadcast = false;

        private static HttpClient oClient = new HttpClient(); //thx https://aspnetmonsters.com/2016/08/2016-08-27-httpclientwrong/

        public static string sURL
        {
            get
            {
                if (DisableBroadcast)
                    _sURL = "";

                string sWebSVC = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\RuckZuck", "WebService", "") as string;
                if (!string.IsNullOrEmpty(sWebSVC))
                {
                    if (sWebSVC.StartsWith("http", StringComparison.CurrentCultureIgnoreCase))
                    {
                        _sURL = sWebSVC.TrimEnd('/');
                    }
                }

                if (_sURL == "UDP" && !DisableBroadcast)
                {
                    try
                    {
                        using (var Client = new UdpClient())
                        {
                            Client.Client.SendTimeout = 1000;
                            Client.Client.ReceiveTimeout = 1000;
                            var RequestData = Encoding.ASCII.GetBytes(Environment.MachineName);
                            var ServerEp = new IPEndPoint(IPAddress.Any, 0);

                            Client.EnableBroadcast = true;
                            Client.Send(RequestData, RequestData.Length, new IPEndPoint(IPAddress.Broadcast, 5001));

                            var ServerResponseData = Client.Receive(ref ServerEp);
                            var ServerResponse = Encoding.ASCII.GetString(ServerResponseData);
                            Console.WriteLine("Recived {0} from {1}", ServerResponse, ServerEp.Address.ToString());
                            if (ServerResponse.StartsWith("http"))
                                _sURL = ServerResponse;
                            Client.Close();
                        }
                    }
                    catch { _sURL = ""; }
                }

                if (string.IsNullOrEmpty(_sURL))
                {
                    return "https://ruckzuck.azureedge.net"; //CDN Version -> otherwise use "https://ruckzuck.azurewebsites.net/"
                }
                else
                    return _sURL;
            }
            set
            {
                _sURL = value;
            }
        }


        public static List<GetSoftware> GetCatalog(string customerid = "")
        {
            if (string.IsNullOrEmpty(customerid))
            {
                if (File.Exists(Path.Combine(Environment.ExpandEnvironmentVariables("%TEMP%"), "rzcat.json"))) //Cached content exists
                {
                    try
                    {
                        DateTime dCreationDate = File.GetLastWriteTime(Path.Combine(Environment.ExpandEnvironmentVariables("%TEMP%"), "rzcat.json"));
                        if ((DateTime.Now - dCreationDate) < new TimeSpan(0, 30, 0)) //Cache for 30min
                        {
                            //return cached Content
                            string jRes = File.ReadAllText(Path.Combine(Environment.ExpandEnvironmentVariables("%TEMP%"), "rzcat.json"));
                            JavaScriptSerializer ser = new JavaScriptSerializer();
                            List<GetSoftware> lRes = ser.Deserialize<List<GetSoftware>>(jRes);
                            return lRes;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("E1" + ex.Message, "GetCatalog");
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

                response.Wait(30000); //30s max

                if (response.IsCompleted)
                {
                    JavaScriptSerializer ser = new JavaScriptSerializer();
                    List<GetSoftware> lRes = ser.Deserialize<List<GetSoftware>>(response.Result);


                    if (!string.IsNullOrEmpty(customerid) && lRes.Count > 400)
                    {
                        File.WriteAllText(Path.Combine(Environment.ExpandEnvironmentVariables("%TEMP%"), "rzcat.json"), response.Result);
                    }

                    return lRes;
                }
            }
            catch(Exception ex)
            {
                Debug.WriteLine("E2" + ex.Message, "GetCatalog");
            }

            return new List<GetSoftware>();
        }

        public static List<AddSoftware> GetSoftwares(string productName, string productVersion, string manufacturer)
        {

            try
            {
                Task<string> response = oClient.GetStringAsync(sURL + "/rest/v2/GetSoftwares?name=" + WebUtility.UrlEncode(productName) + "&ver=" + WebUtility.UrlEncode(productVersion) + "&man=" + WebUtility.UrlEncode(manufacturer));
                response.Wait(20000);
                if (response.IsCompleted)
                {
                    JavaScriptSerializer ser = new JavaScriptSerializer();
                    List<AddSoftware> lRes = ser.Deserialize<List<AddSoftware>>(response.Result);
                    return lRes;
                }

            }
            catch(Exception ex)
            {
                Debug.WriteLine("E1" + ex.Message, "GetSoftwares");
            }

            return new List<AddSoftware>();

        }

        public static byte[] GetIcon(long iconid = 0, string iconhash = "")
        {
            Task<Stream> response;
            if(string.IsNullOrEmpty(iconhash))
                response = oClient.GetStreamAsync(sURL + "rest/v2/GetIcon?iconid=" + iconid);
            else
                response = oClient.GetStreamAsync(sURL + "rest/v2/GetIcon?iconhash=" + iconhash);

            response.Wait(10000);

            if (response.IsCompleted)
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    response.Result.CopyTo(ms);
                    byte[] bRes = ms.ToArray();
                    return bRes;
                }
            }

            return null;
        }
    }
}
