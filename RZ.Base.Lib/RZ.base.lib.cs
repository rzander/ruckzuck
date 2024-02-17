using Microsoft.Win32;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Logging;

namespace RZ.Base.Lib
{
    public class RuckZuck
    {
        public string Customerid = "";
        internal string _URL = "";
        internal readonly HttpClient oClient = new HttpClient();
        internal readonly HttpClient bClient = new HttpClient();
        internal JArray _catalog = new JArray();
        internal bool _feedback = true;

        private readonly ILogger<RuckZuck> _logger;

        public JArray Catalog
        {
            get
            {
                if(_catalog.Count <= 0)
                    _catalog = GetCatalogAsync().Result;    

                return _catalog;
            }
            set
            {
                if (value == null)
                    _catalog = GetCatalogAsync().Result;
                else
                    _catalog = value;
            }
        }

        public RuckZuck(string customerid = "", string URL = "", bool SendFeedback = true, ILogger<RuckZuck> logger = null)
        {
            //specify to use TLS 1.2 as default connection
            System.Net.ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;


            _feedback = SendFeedback;

            if(logger == null)
                _logger = new LoggerFactory().CreateLogger<RuckZuck>();
            else
                _logger = logger;   

            if (string.IsNullOrEmpty(customerid))
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    try
                    {
                        string sCustID = (Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\RuckZuck", "CustomerID", "") as string) ?? "";
                        if (!string.IsNullOrEmpty(sCustID))
                        {
                            Customerid = sCustID; //Override CustomerID
                        }
                    }
                    catch { }


                    try
                    {
                        string sWebSVC = (Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\RuckZuck", "WebService", "") as string) ?? "";
                        if (!string.IsNullOrEmpty(sWebSVC))
                        {
                            if (sWebSVC.StartsWith("http", StringComparison.CurrentCultureIgnoreCase))
                            {
                                _URL = sWebSVC.TrimEnd('/');
                            }
                        }
                    }
                    catch { }
                }
            }
            else
            {
                Customerid = customerid;
            }

            CancellationTokenSource cts = new CancellationTokenSource(10000);

            if (string.IsNullOrEmpty(Customerid))
            {
                Customerid = oClient.GetStringAsync("https://ruckzuck.tools/rest/v2/getip", cts.Token).Result;
            }

            if (string.IsNullOrEmpty(_URL))
            {
                if (string.IsNullOrEmpty(Customerid))
                {
                    _URL = oClient.GetStringAsync("https://cdn.ruckzuck.tools/rest/v2/geturl", cts.Token).Result;
                }
                else
                    _URL = oClient.GetStringAsync("https://cdn.ruckzuck.tools/rest/v2/geturl?customerid=" + Customerid, cts.Token).Result;
            }

            if (string.IsNullOrEmpty(_URL))
            {
                _URL = "https://ruckzuck.azurewebsites.net";
            }

            _logger.LogDebug("CustomerId: {id} ; URL: {URL}",Customerid, _URL);
        }

        internal async Task<JArray> GetCatalogAsync()
        {
            if (!Customerid.StartsWith("--"))
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

                            JArray jaRes = JArray.Parse(jRes);

                            _logger.LogDebug("Catalog loaded from cache. Items: {count}", jaRes.Count);

                            return jaRes;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Error: E506: GetCatalog: {ex}", ex.Message);
                    }
                }
            }

            try
            {
                CancellationTokenSource cts = new CancellationTokenSource(10000);
                string response;
                if (string.IsNullOrEmpty(Customerid) || Customerid.Count(t => t == '.') == 3)
                    response = await oClient.GetStringAsync(_URL + "/rest/v2/GetCatalog", cts.Token); //add cts in .NET 6
                else
                    response = await oClient.GetStringAsync(_URL + "/rest/v2/GetCatalog?customerid=" + Customerid, cts.Token); //add cts in .NET 6

                if (!string.IsNullOrEmpty(response) && !cts.Token.IsCancellationRequested)
                {
                    JArray jaRes = JArray.Parse(response);

                    if (jaRes.Count > 500 && !Customerid.StartsWith("--"))
                    {
                        _logger.LogDebug("Catalog loaded from Web. Items: {count}", jaRes.Count);
                        try
                        {
                            File.WriteAllText(Path.Combine(Environment.ExpandEnvironmentVariables("%TEMP%"), "rzcat.json"), response);
                        }
                        catch { }
                    }

                    return jaRes;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error: E539: GetCatalog: {ex}", ex.Message);
            }

            return new JArray();
        }

        public List<string> GetCategories()
        {
            List<string> lResult = new List<string>();

            Catalog.SelectTokens("$..Categories").ToList().ForEach(t => lResult.AddRange(t.Values<string>()));

            return lResult.Distinct().OrderBy(t => t).ToList();
        }

        public async Task<JArray> GetSoftwares(string shortname, string customerid = "")
        {
            if(string.IsNullOrEmpty(shortname))
                return new JArray();

            _logger.LogInformation("GetSoftwares: {shortname}", shortname);

            if (File.Exists(Path.Combine(Environment.ExpandEnvironmentVariables("%TEMP%"), shortname + ".json"))) //Cached content exists
            {
                try
                {
                    //return cached Content
                    string jRes = File.ReadAllText(Path.Combine(Environment.ExpandEnvironmentVariables("%TEMP%"), shortname + ".json"));
                    if (jRes.Trim().StartsWith("[") && jRes.EndsWith("]"))
                    {
                        JArray jaRes = JArray.Parse(jRes);
                        _logger.LogDebug("Software loaded from cache file. File: {file} ", Path.Combine(Environment.ExpandEnvironmentVariables("%TEMP%"), shortname + ".json"));
                        return jaRes;
                    }

                    if (jRes.Trim().StartsWith("{") && jRes.EndsWith("}"))
                    {
                        JObject joRes = JObject.Parse(jRes);
                        _logger.LogDebug("Software loaded from cache file. File: {file} ", Path.Combine(Environment.ExpandEnvironmentVariables("%TEMP%"), shortname + ".json"));
                        return new JArray() { joRes };
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError("Error: E598: GetSoftwares: {ex}", ex.Message);
                }
            }

            JToken? jCatItem = Catalog.FirstOrDefault(t => (t["ShortName"]?.Value<string>() ?? "").Equals(shortname, StringComparison.CurrentCultureIgnoreCase));
            if (jCatItem != null)
            {
                return await GetSoftwares(jCatItem["ProductName"]?.Value<string>() ?? "", jCatItem["ProductVersion"]?.Value<string>() ?? "", jCatItem["Manufacturer"]?.Value<string>() ?? "", customerid);
            }

            _logger.LogWarning("GetSoftwares: {shortname} not found", shortname);
            return new JArray();
        }
        public async Task<JArray> GetSoftwares(string productName, string productVersion, string manufacturer, string customerid = "")
        {
            _logger.LogDebug("GetSoftwares: {productName} ; {productVersion} ; {manufacturer}", productName, productVersion, manufacturer);
            try
            {
                string response;

                if (string.IsNullOrEmpty(customerid))
                    response = await oClient.GetStringAsync(_URL + "/rest/v2/GetSoftwares?name=" + WebUtility.UrlEncode(productName) + "&ver=" + WebUtility.UrlEncode(productVersion) + "&man=" + WebUtility.UrlEncode(manufacturer) + "&apikey=" + DateTime.Now.Ticks);
                else
                    response = await oClient.GetStringAsync(_URL + "/rest/v2/GetSoftwares?name=" + WebUtility.UrlEncode(productName) + "&ver=" + WebUtility.UrlEncode(productVersion) + "&man=" + WebUtility.UrlEncode(manufacturer) + "&customerid=" + WebUtility.UrlEncode(customerid) + "&apikey=" + DateTime.Now.Ticks);

                if (!string.IsNullOrEmpty(response))
                {
                    JArray lRes = JArray.Parse(response);
                    return lRes;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("E1" + ex.Message, "GetSoftwares");
            }

            _logger.LogInformation("GetSoftwares: {productName} ; {productVersion} ; {manufacturer} not found", productName, productVersion, manufacturer);
            return new JArray();
        }

        public async Task<bool> Download(JArray Software, bool includeDependencies = true)
        {
            bool bRes = true;
            foreach (JObject jSW in Software)
            {
                if (jSW["PSPreReq"] != null && jSW["PSPreReq"]?.Value<string>() != "")
                {
                    string sPreReq = RunPS(jSW["PSPreReq"]?.Value<string>() ?? "");    
                    if(!string.IsNullOrEmpty(sPreReq) && sPreReq.ToLower().Contains("true"))
                    {
                        _logger.LogDebug("PreReq passed: {SW}; PS:{ps}", jSW["ShortName"]?.Value<string>() ?? "", jSW["PSPreReq"]?.Value<string>());
                        if(!await Download(jSW, includeDependencies))
                            bRes = false;
                        return bRes; 
                    }
                    else
                    {
                        _logger.LogWarning("PreReq failed: {SW}", jSW["ShortName"]?.Value<string>() ?? "");
                        continue;
                    }
                }
            }

            return bRes;
        }
        public async Task<bool> Download(JObject Software, bool includeDependencies = true)
        {
            _logger.LogDebug("Checking Content for: {Software}", Software["ShortName"]?.Value<string>() ?? "");

            bool bRes = true;

            //Download PreRequisites
            if (includeDependencies && Software["PreRequisites"] != null && Software["PreRequisites"].HasValues)
            {
                foreach (string? sPreReq in Software["PreRequisites"]?.Values<string>() ?? new List<string>())
                {
                    if (string.IsNullOrEmpty(sPreReq))
                    {
                        JArray oPreSW = await GetSoftwares(sPreReq ?? "", Customerid);
                        if (oPreSW.Count > 0)
                        {
                            if (!await Download(oPreSW))
                                bRes = false;
                        }
                    }
                }
            }

            if (Software["Files"] != null && Software["Files"]?.Count() > 0)
            {
                foreach (JObject jSW in Software["Files"])
                {
                    try
                    {
                        if(jSW["URL"] == null || string.IsNullOrEmpty(jSW["URL"]?.Value<string>()))
                        {
                            continue;
                        }
                        string sURL = jSW["URL"]?.Value<string>() ?? "";
                        string sFile = jSW["FileName"]?.Value<string>() ?? "";
                        string sContentID = Software["ContentID"]?.Value<string>() ?? "";

                        string sPath = Path.Join(Environment.ExpandEnvironmentVariables("%TEMP%"), sContentID);
                        if (File.Exists(Path.Join(sPath, sFile)))
                        {
                            _logger.LogInformation("File already exists: {contentid}\\{file}", sContentID, sFile);
                            if ((jSW["HashType"]?.Value<string>() ?? "") == "X509")
                            {
                                if (!AuthenticodeTools.IsTrusted(Path.Join(sPath, sFile)))
                                {
                                    _logger.LogWarning("File not trusted: {file}", Path.Join(sPath, sFile));
                                    File.Delete(Path.Join(sPath, sFile));
                                }
                                else
                                {
                                    _logger.LogDebug("File has valid X509 Signature: {file}", sFile);
                                    continue;
                                }
                            }
                            if ((jSW["HashType"]?.Value<string>() ?? "") == "MD5")
                            {
                                if (!AuthenticodeTools.CheckFileMd5(Path.Join(sPath, sFile), jSW["FileHash"]?.Value<string>() ?? ""))
                                {
                                    _logger.LogWarning("File MD5 Hash not valid: {file}", Path.Join(sPath, sFile));
                                    File.Delete(Path.Join(sPath, sFile));
                                }
                                else
                                {
                                    _logger.LogDebug("File Hash is valid: {file} , Hash: {md5}", sFile, jSW["FileHash"]?.Value<string>() ?? "");
                                    continue;
                                }
                            }
                            if ((jSW["HashType"]?.Value<string>() ?? "") == "SHA1")
                            {
                                if (!AuthenticodeTools.CheckFileSHA1(Path.Join(sPath, sFile), jSW["FileHash"]?.Value<string>() ?? ""))
                                {
                                    _logger.LogWarning("File SHA1 Hash not valid: {file}", Path.Join(sPath, sFile));
                                    File.Delete(Path.Join(sPath, sFile));
                                }
                                else
                                {
                                    _logger.LogDebug("File Hash is valid: {file} , Hash: {sha1}", sFile, jSW["FileHash"]?.Value<string>() ?? "");
                                    continue;
                                }
                            }
                            if ((jSW["HashType"]?.Value<string>() ?? "") == "SHA256")
                            {
                                if (!AuthenticodeTools.CheckFileSHA256(Path.Join(sPath, sFile), jSW["FileHash"]?.Value<string>() ?? ""))
                                {
                                    _logger.LogWarning("File SHA256 Hash not valid: {file}", Path.Join(sPath, sFile));
                                    File.Delete(Path.Join(sPath, sFile));
                                }
                                else
                                {
                                    _logger.LogDebug("File Hash is valid: {file} , Hash: {sha256}", sFile, jSW["FileHash"]?.Value<string>() ?? "");
                                    continue;
                                }
                            }

                        }
                        else
                        {
                            Directory.CreateDirectory(sPath);
                        }

                        if (!sURL.StartsWith("http", StringComparison.CurrentCultureIgnoreCase))
                        {
                            _logger.LogDebug("Run PowerShell to get URL: {ps}", sURL);
                            //implement PowerShell Download
                            string sRes = RunPS(sURL);
                            if (sRes != null && sRes.StartsWith("http", StringComparison.CurrentCultureIgnoreCase))
                            {
                                sURL = sRes;
                                _logger.LogDebug("URL: {URL}", sURL);
                            }
                            else
                            {
                                if (!(sRes ?? "").ToLower().Contains("<skip>"))
                                {
                                    _logger.LogError("Error: 613: nor valid URL: {url}, for File: {file}", sURL, sFile);
                                    bRes = false;
                                }
                                continue;
                            }
                        }

                        if (!string.IsNullOrEmpty(sURL) && !string.IsNullOrEmpty(sFile))
                        {
                            try
                            {
                                CancellationTokenSource cts = new CancellationTokenSource(new TimeSpan(0,15,0)); //15min Timeout

                                _logger.LogInformation("Downloading {file} from {URL}", sFile, sURL);

                                byte[] bFile = await bClient.GetByteArrayAsync(sURL, cts.Token);

                                if (bFile.Length > 0)
                                {
                                    File.WriteAllBytes(Path.Join(sPath, sFile), bFile);
                                }

                                _logger.LogInformation("File downloaded: {contentid}\\{file} , Size: {size}MB", sContentID, sFile, bFile.LongLength / 1024 / 1024);
                                await IncCounter(Software["ShortName"]?.ToString() ?? "", "DL", Customerid);

                                if ((jSW["HashType"]?.Value<string>() ?? "") == "X509")
                                {
                                    if (!AuthenticodeTools.IsTrusted(Path.Join(sPath, sFile)))
                                    {
                                        _logger.LogWarning("File not trusted: {file}", Path.Join(sPath, sFile));
                                        _ = await SendFeedback(Software, "false", System.Reflection.Assembly.GetExecutingAssembly()?.GetName().Name ?? "RZ.Base", "Signature mismatch", Customerid);
                                        File.Delete(Path.Join(sPath, sFile));
                                        bRes = false;
                                    }
                                    else
                                    {
                                        _logger.LogDebug("File has valid X509 Signature: {file}", sFile);
                                    }
                                }
                                if ((jSW["HashType"]?.Value<string>() ?? "") == "MD5")
                                {
                                    if (!AuthenticodeTools.CheckFileMd5(Path.Join(sPath, sFile), jSW["FileHash"]?.Value<string>() ?? ""))
                                    {
                                        _logger.LogWarning("File Hash not valid: {file}", Path.Join(sPath, sFile));
                                        _ = await SendFeedback(Software, "false", System.Reflection.Assembly.GetExecutingAssembly()?.GetName().Name ?? "RZ.Base", "Signature mismatch", Customerid);
                                        File.Delete(Path.Join(sPath, sFile));
                                    }
                                    else
                                    {
                                        _logger.LogDebug("File MD5 Hash is valid: {file} , Hash: {md5}", sFile, jSW["FileHash"]?.Value<string>() ?? "");
                                        continue;
                                    }
                                }
                                if ((jSW["HashType"]?.Value<string>() ?? "") == "SHA1")
                                {
                                    if (!AuthenticodeTools.CheckFileSHA1(Path.Join(sPath, sFile), jSW["FileHash"]?.Value<string>() ?? ""))
                                    {
                                        _logger.LogWarning("File SHA1 Hash not valid: {file}", Path.Join(sPath, sFile));
                                        _ = await SendFeedback(Software, "false", System.Reflection.Assembly.GetExecutingAssembly()?.GetName().Name ?? "RZ.Base", "Signature mismatch", Customerid);
                                        File.Delete(Path.Join(sPath, sFile));
                                    }
                                    else
                                    {
                                        _logger.LogDebug("File Hash is valid: {file} , Hash: {sha1}", sFile, jSW["FileHash"]?.Value<string>() ?? "");
                                        continue;
                                    }
                                }
                                if ((jSW["HashType"]?.Value<string>() ?? "") == "SHA256")
                                {
                                    if (!AuthenticodeTools.CheckFileSHA256(Path.Join(sPath, sFile), jSW["FileHash"]?.Value<string>() ?? ""))
                                    {
                                        _logger.LogWarning("File SHA256 Hash not valid: {file}", Path.Join(sPath, sFile));
                                        _ = await SendFeedback(Software, "false", System.Reflection.Assembly.GetExecutingAssembly()?.GetName().Name ?? "RZ.Base", "Signature mismatch", Customerid);
                                        File.Delete(Path.Join(sPath, sFile));
                                    }
                                    else
                                    {
                                        _logger.LogDebug("File Hash is valid: {file} , Hash: {sha256}", sFile, jSW["FileHash"]?.Value<string>() ?? "");
                                        continue;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError("Error: 788: Download: {ex}", ex.Message);
                                bRes = false;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Error:795: Download: {ex}", ex.Message);
                        bRes = false;
                    }
                }
            }

            return bRes;
        }

        public async Task<bool> Install(JArray Software, bool includeDependencies = true)
        {
            bool bRes = false;

            foreach (JObject jSW in Software)
            {
                if (await Install(jSW, includeDependencies))
                {
                    return true;
                }
            }

            return bRes;
        }

        public async Task<bool> Install(JObject Software, bool includeDependencies = true, bool ignoreDependencyFailure = false)
        {
            if (Software == null)
                return false;

            if (Software["PSPreReq"] != null && Software["PSPreReq"]?.Value<string>() != "")
            {
                string sPreReq = RunPS(Software["PSPreReq"]?.Value<string>() ?? "");
                if (!string.IsNullOrEmpty(sPreReq) && sPreReq.ToLower().Contains("true"))
                {
                    _logger.LogDebug("PreReq passed: {SW}; PS:{ps}", Software["ShortName"]?.Value<string>() ?? "", Software["PSPreReq"]?.Value<string>());
                }
                else
                {
                    _logger.LogWarning("PreReq failed: {SW}", Software["ShortName"]?.Value<string>() ?? "");
                    return false;
                }
            }

            //Download PreRequisites
            if (includeDependencies && Software["PreRequisites"] != null && Software["PreRequisites"].HasValues)
            {
                foreach (string? sPreReq in Software["PreRequisites"]?.Values<string>() ?? new List<string>())
                {
                    if (!string.IsNullOrEmpty(sPreReq))
                    {
                        JArray oPreSW = await GetSoftwares(sPreReq ?? "", Customerid);
                        if (oPreSW.Count > 0)
                        {
                            if (!await Install(oPreSW))
                            {
                                if (ignoreDependencyFailure)
                                {
                                    _logger.LogDebug("Dependency failed, but we ignore it... {SW}", sPreReq);
                                }
                                else
                                {
                                    _logger.LogWarning("Dependency failed: {SW}", sPreReq);
                                    return false;
                                }
                            }
                        }
                    }
                }
            }

            bool bRes = false;

            if (Software != null && Software["PSDetection"] != null)
            {
                string sRes = RunPS(Software["PSDetection"]?.Value<string>() ?? "", ""); //Do not cache result as we need to run the PS again
                if (!string.IsNullOrEmpty(sRes) && sRes.ToLower().Contains("true"))
                {
                    _logger.LogDebug("PSDetection passed: {SW}; PS:{ps}", Software["ShortName"]?.Value<string>() ?? "", Software["PSDetection"]?.Value<string>());
                    _logger.LogInformation("Installation successful: {SW}", Software["ShortName"]?.Value<string>() ?? "");
                    return true;
                }
                else
                {
                    _logger.LogDebug("PSDetection failed: {SW}", Software["ShortName"]?.Value<string>() ?? "");
                }
            }

            if (!await Download(Software))
            {
                _logger.LogWarning("Download failed: {SW}", Software["ShortName"]?.Value<string>() ?? "");
                _ = await SendFeedback(Software, "false", System.Reflection.Assembly.GetExecutingAssembly()?.GetName().Name ?? "RZ.Base", "download failed", Customerid);
                return false;
            }
            else 
            {
                //_ = await SendFeedback(Software, "true", System.Reflection.Assembly.GetExecutingAssembly()?.GetName().Name ?? "RZ.Base", "download failed", Customerid);
            }

            while (InstallRunning())
            {
                Thread.Sleep(3000);
            }

            _logger.LogDebug("Installing: {Software}", Software["ShortName"]?.Value<string>() ?? "");

            if (Software != null && Software["PSInstall"] != null)
            {
                string sPS = (Software["PSPreInstall"]?.Value<string>() ?? "") + ";\n\r" + (Software["PSInstall"]?.Value<string>() ?? "") + ";\n\r" + (Software["PSPostInstall"]?.Value<string>() ?? "");
                sPS = sPS.TrimStart(';').TrimEnd(';');

                string sContentID = Software["ContentID"]?.Value<string>() ?? "";
                string sPath = Path.Join(Environment.ExpandEnvironmentVariables("%TEMP%"), sContentID);
                Directory.CreateDirectory(sPath); //Create Folder if not exists

                string sRes = RunPS(sPS, sPath);
                sRes.ToString();
            }

            if (Software != null && Software["PSDetection"] != null)
            {
                string sRes = RunPS(Software["PSDetection"]?.Value<string>() ?? "");
                if (!string.IsNullOrEmpty(sRes) && sRes.ToLower().Contains("true"))
                {
                    _logger.LogDebug("PSDetection passed: {SW}; PS:{ps}", Software["ShortName"]?.Value<string>() ?? "", Software["PSDetection"]?.Value<string>());
                    _logger.LogInformation("Installation successful: {SW}", Software["ShortName"]?.Value<string>() ?? "");
                    return true;
                }
                else
                {
                    _logger.LogWarning("PSDetection failed: {SW}", Software["ShortName"]?.Value<string>() ?? ""); 
                    _ = await SendFeedback(Software, "false", System.Reflection.Assembly.GetExecutingAssembly()?.GetName().Name ?? "RZ.Base", "Product not detected after installation.", Customerid);
                    return false;
                }
            }

            return bRes;
        }

        public async Task<string> SendFeedback(JObject Software, string working, string userKey, string feedback, string customerid = "", CancellationToken? ct = null)
        {
            if (_feedback && !string.IsNullOrEmpty(feedback))
            {
                if(string.IsNullOrEmpty(customerid))
                    customerid = Customerid;

                string productName = Software["ProductName"]?.ToString() ?? "";
                string productVersion = Software["ProductVersion"]?.ToString() ?? "";
                string manufacturer = Software["Manufacturer"]?.ToString() ?? "";

                try
                {
                    if (ct == null)
                        ct = new CancellationTokenSource(30000).Token; //30s TimeOut

                    var oRes = await oClient.GetStringAsync(new Uri(_URL + "/rest/v2/feedback?name=" + WebUtility.UrlEncode(productName) + "&ver=" + WebUtility.UrlEncode(productVersion) + "&man=" + WebUtility.UrlEncode(manufacturer) + "&ok=" + working + "&user=" + WebUtility.UrlEncode(userKey) + "&text=" + WebUtility.UrlEncode(feedback) + "&customerid=" + WebUtility.UrlEncode(customerid)), (CancellationToken)ct);
                    _logger.LogDebug("sending Feedback: {productName} ; {productVersion} ; {manufacturer} ; {working} ; {userKey} ; {feedback} ; {customerid}", productName, productVersion, manufacturer, working, userKey, feedback, customerid);

                    return oRes;
                }
                catch { }
            }
            else
            {
                _logger.LogDebug("sending Feedback skipped...");
            }

            return "";
        }

        internal async Task IncCounter(string shortname, string counter = "DL", string customerid = "")
        {
            if(!_feedback)
                return;
            if(string.IsNullOrEmpty(shortname))
                return; 
            if(string.IsNullOrEmpty(customerid))
                customerid = Customerid;    
            _ = await oClient.GetStringAsync(_URL + "/rest/v2/IncCounter?shortname=" + WebUtility.UrlEncode(shortname) + "&customerid=" + WebUtility.UrlEncode(customerid), new CancellationTokenSource(5000).Token);
        }

        internal string RunPS(string PSScript, string WorkingDir = "")
        {
            if (string.IsNullOrEmpty(WorkingDir))
                WorkingDir = Environment.ExpandEnvironmentVariables("%TEMP%");


            try
            {
                var plainTextBytes = System.Text.Encoding.Unicode.GetBytes(PSScript);
                string sBase64 = Convert.ToBase64String(plainTextBytes);

                // Create a new process
                using (Process process = new Process())
                {
                    // Set the filename of the process to PowerShell
                    process.StartInfo.FileName = "powershell.exe";

                    // Add any additional arguments (e.g., script execution policy)
                    process.StartInfo.Arguments = "-ExecutionPolicy Bypass -NoProfile -NoLogo -encodedCommand " + sBase64;

                    // Redirect standard output and error streams so we can capture them
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;
                    //process.StartInfo.RedirectStandardInput = true;

                    // Enable shell execute
                    process.StartInfo.UseShellExecute = false;

                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.WorkingDirectory = WorkingDir;

                    // Start the process
                    process.Start();

                    // Optionally, you can send commands to PowerShell
                    // For example, let's say you want to run a simple command
                    //process.StandardInput.WriteLine(PSScript);
                    //process.StandardInput.WriteLine("Get-Date");

                    // Read the output and error streams
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();

                    // Wait for the process to exit
                    process.WaitForExit();

                    return output;

                    //// Output the results
                    //Console.WriteLine("Output:");
                    //Console.WriteLine(output);
                    //Console.WriteLine("Error:");
                    //Console.WriteLine(error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error: 908: RunPS: {ex}", ex.Message);
            }

            return "";
        }

        internal bool InstallRunning()
        {

            bool bRes = false;
            try
            {
                //Check if MSI is running...
                using (var mutex = Mutex.OpenExisting(@"Global\_MSIExecute"))
                {
                    _logger.LogWarning("Warning: Windows-Installer setup is already running!... waiting...");
                    bRes = true;
                }

                //Check if RuckZuck is running...
                using (var mutex = Mutex.OpenExisting(@"Global\RuckZuck"))
                {
                    _logger.LogWarning("Warning: RuckZuck setup is already running!... waiting...");
                    bRes = true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error: 741: InstallRunning: {ex}", ex.Message);
            }

            return bRes;
        }
    }

    public static class AuthenticodeTools
    {
        //Source: https://stackoverflow.com/questions/6596327/how-to-check-if-a-file-is-signed-in-c

        [DllImport("Wintrust.dll", PreserveSig = true, SetLastError = false)]
        private static extern uint WinVerifyTrust(IntPtr hWnd, IntPtr pgActionID, IntPtr pWinTrustData);

        private static uint WinVerifyTrust(string fileName)
        {
            Guid wintrust_action_generic_verify_v2 = new Guid("{00AAC56B-CD44-11d0-8CC2-00C04FC295EE}");
            uint result = 0;
            using (WINTRUST_FILE_INFO fileInfo = new WINTRUST_FILE_INFO(fileName, Guid.Empty))
            using (WINTRUST_DATA.UnmanagedPointer guidPtr = new WINTRUST_DATA.UnmanagedPointer(Marshal.AllocHGlobal(Marshal.SizeOf(typeof(Guid))), AllocMethod.HGlobal))
            using (WINTRUST_DATA.UnmanagedPointer wvtDataPtr = new WINTRUST_DATA.UnmanagedPointer(Marshal.AllocHGlobal(Marshal.SizeOf(typeof(WINTRUST_DATA))), AllocMethod.HGlobal))
            {
                WINTRUST_DATA data = new WINTRUST_DATA(fileInfo);
                IntPtr pGuid = guidPtr;
                IntPtr pData = wvtDataPtr;
                Marshal.StructureToPtr(wintrust_action_generic_verify_v2, pGuid, true);
                Marshal.StructureToPtr(data, pData, true);
                result = WinVerifyTrust(IntPtr.Zero, pGuid, pData);
            }
            return result;
        }

        public static bool IsTrusted(string fileName)
        {
            return WinVerifyTrust(fileName) == 0;
        }

        public struct WINTRUST_FILE_INFO : IDisposable
        {
            public WINTRUST_FILE_INFO(string fileName, Guid subject)
            {
                cbStruct = (uint)Marshal.SizeOf(typeof(WINTRUST_FILE_INFO));
                pcwszFilePath = fileName;

                if (subject != Guid.Empty)
                {
                    pgKnownSubject = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(Guid)));
                    Marshal.StructureToPtr(subject, pgKnownSubject, true);
                }
                else
                {
                    pgKnownSubject = IntPtr.Zero;
                }

                hFile = IntPtr.Zero;
            }

            public uint cbStruct;

            [MarshalAs(UnmanagedType.LPTStr)]
            public string pcwszFilePath;

            public IntPtr hFile;
            public IntPtr pgKnownSubject;

            #region IDisposable Members

            public void Dispose()
            {
                Dispose(true);
            }

            private void Dispose(bool disposing)
            {
                if (pgKnownSubject != IntPtr.Zero)
                {
                    Marshal.DestroyStructure(this.pgKnownSubject, typeof(Guid));
                    Marshal.FreeHGlobal(this.pgKnownSubject);
                }
            }

            #endregion IDisposable Members
        }

        public enum AllocMethod
        {
            HGlobal,
            CoTaskMem
        };

        public enum UnionChoice
        {
            File = 1,
            Catalog,
            Blob,
            Signer,
            Cert
        };

        public enum UiChoice
        {
            All = 1,
            NoUI,
            NoBad,
            NoGood
        };

        public enum RevocationCheckFlags
        {
            None = 0,
            WholeChain
        };

        public enum StateAction
        {
            Ignore = 0,
            Verify,
            Close,
            AutoCache,
            AutoCacheFlush
        };

        public enum TrustProviderFlags
        {
            UseIE4Trust = 1,
            NoIE4Chain = 2,
            NoPolicyUsage = 4,
            RevocationCheckNone = 16,
            RevocationCheckEndCert = 32,
            RevocationCheckChain = 64,
            RecovationCheckChainExcludeRoot = 128,
            Safer = 256,
            HashOnly = 512,
            UseDefaultOSVerCheck = 1024,
            LifetimeSigning = 2048
        };

        public enum UIContext
        {
            Execute = 0,
            Install
        };

        [StructLayout(LayoutKind.Sequential)]
        public struct WINTRUST_DATA : IDisposable
        {
            public WINTRUST_DATA(WINTRUST_FILE_INFO fileInfo)
            {
                this.cbStruct = (uint)Marshal.SizeOf(typeof(WINTRUST_DATA));
                pInfoStruct = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(WINTRUST_FILE_INFO)));
                Marshal.StructureToPtr(fileInfo, pInfoStruct, false);
                this.dwUnionChoice = UnionChoice.File;

                pPolicyCallbackData = IntPtr.Zero;
                pSIPCallbackData = IntPtr.Zero;
                dwUIChoice = UiChoice.NoUI;
                fdwRevocationChecks = RevocationCheckFlags.None;
                dwStateAction = StateAction.Ignore;
                hWVTStateData = IntPtr.Zero;
                pwszURLReference = IntPtr.Zero;
                dwProvFlags = TrustProviderFlags.HashOnly;
                dwUIContext = UIContext.Execute;
            }

            public uint cbStruct;

            public IntPtr pPolicyCallbackData;

            public IntPtr pSIPCallbackData;

            public UiChoice dwUIChoice;

            public RevocationCheckFlags fdwRevocationChecks;

            public UnionChoice dwUnionChoice;

            public IntPtr pInfoStruct;

            public StateAction dwStateAction;

            public IntPtr hWVTStateData;

            private IntPtr pwszURLReference;

            public TrustProviderFlags dwProvFlags;

            public UIContext dwUIContext;

            #region IDisposable Members

            public void Dispose()
            {
                Dispose(true);
            }

            private void Dispose(bool disposing)
            {
                if (dwUnionChoice == UnionChoice.File)
                {
                    WINTRUST_FILE_INFO info = new WINTRUST_FILE_INFO();
                    Marshal.PtrToStructure(pInfoStruct, info);
                    info.Dispose();
                    Marshal.DestroyStructure(pInfoStruct, typeof(WINTRUST_FILE_INFO));
                }

                Marshal.FreeHGlobal(pInfoStruct);
            }

            #endregion IDisposable Members

            internal sealed class UnmanagedPointer : IDisposable
            {
                private IntPtr m_ptr;
                private AllocMethod m_meth;

                internal UnmanagedPointer(IntPtr ptr, AllocMethod method)
                {
                    m_meth = method;
                    m_ptr = ptr;
                }

                ~UnmanagedPointer()
                {
                    Dispose(false);
                }

                #region IDisposable Members

                private void Dispose(bool disposing)
                {
                    if (m_ptr != IntPtr.Zero)
                    {
                        if (m_meth == AllocMethod.HGlobal)
                        {
                            Marshal.FreeHGlobal(m_ptr);
                        }
                        else if (m_meth == AllocMethod.CoTaskMem)
                        {
                            Marshal.FreeCoTaskMem(m_ptr);
                        }

                        m_ptr = IntPtr.Zero;
                    }

                    if (disposing)
                    {
                        GC.SuppressFinalize(this);
                    }
                }

                public void Dispose()
                {
                    Dispose(true);
                }

                #endregion IDisposable Members

                public static implicit operator IntPtr(UnmanagedPointer ptr)
                {
                    return ptr.m_ptr;
                }
            }
        }

        public static bool CheckFileMd5(string FilePath, string MD5)
        {
            try
            {
                using (var md5 = System.Security.Cryptography.MD5.Create())
                {
                    using (var stream = File.OpenRead(FilePath))
                    {
                        if (MD5.ToLower() != BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "").ToLower())
                            return false;
                        else
                            return true;
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        public static bool CheckFileSHA1(string FilePath, string SHA1)
        {
            try
            {
                using (var sha1 = System.Security.Cryptography.SHA1.Create())
                {
                    using (var stream = File.OpenRead(FilePath))
                    {
                        if (SHA1.ToLower() != BitConverter.ToString(sha1.ComputeHash(stream)).Replace("-", "").ToLower())
                            return false;
                        else
                            return true;
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        public static bool CheckFileSHA256(string FilePath, string SHA256)
        {
            try
            {
                using (var sha256 = System.Security.Cryptography.SHA256.Create())
                {
                    using (var stream = File.OpenRead(FilePath))
                    {
                        if (SHA256.ToLower() != BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", "").ToLower())
                            return false;
                        else
                            return true;
                    }
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
