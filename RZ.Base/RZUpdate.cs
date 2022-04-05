using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.IO;
using System.Management.Automation;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using RuckZuck.Base;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using Newtonsoft.Json;


namespace RZUpdate
{
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

            #endregion
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
                dwProvFlags = TrustProviderFlags.Safer;
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
            #endregion

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

                #endregion

                public static implicit operator IntPtr(UnmanagedPointer ptr)
                {
                    return ptr.m_ptr;
                }
            }
        }
    }

    /// <summary>
    /// Updater Class
    /// </summary>
    public class RZUpdater
    {
        /// <summary>
        /// Access to the SWUpdate
        /// </summary>
        public SWUpdate SoftwareUpdate;

        /// <summary>
        /// Constructor
        /// </summary>
        public RZUpdater()
        {
            AddSoftware oSW = new AddSoftware();
            SoftwareUpdate = new SWUpdate(oSW);
            RZRestAPIv2.sURL.ToString();
        }

        public RZUpdater(string sSWFile)
        {
            if (sSWFile.EndsWith(".json", StringComparison.CurrentCultureIgnoreCase))
            {
                SoftwareUpdate = new SWUpdate(ParseJSON(sSWFile));
            }

            if (!File.Exists(sSWFile))
            {
                SoftwareUpdate = new SWUpdate(Parse(sSWFile));
            }

        }

        /// <summary>
        /// Check if there are Updates for a Software
        /// </summary>
        /// <param name="ProductName">Name of the Software Product (must be in the RuckZuck Repository !)</param>
        /// <param name="Version">>Current Version of the Software</param>
        /// <returns>SWUpdate if an Update is available otherwise null</returns>
        public async Task<SWUpdate> CheckForUpdateAsync(string ProductName, string Version, string Manufacturer = "")
        {
            var tRes = Task.Run(() =>
            {
                try
                {
                    AddSoftware oSW = new AddSoftware();

                    oSW.ProductName = ProductName;
                    oSW.ProductVersion = Version;
                    oSW.Manufacturer = Manufacturer ?? "";

                    List<AddSoftware> oResult = (RZRestAPIv2.CheckForUpdateAsync(new List<AddSoftware>() { oSW })).Result.ToList();

                    if (oResult.Count > 0)
                    {
                        foreach (AddSoftware SW in oResult)
                        {
                            if (SW.PSPreReq == null)
                            {
                                //Load all MetaData for the specific SW
                                foreach (AddSoftware SWCheck in RZRestAPIv2.GetSoftwares(SW.ProductName, SW.ProductVersion, SW.Manufacturer, RZRestAPIv2.CustomerID))
                                {
                                    if (string.IsNullOrEmpty(SW.PSPreReq))
                                        SW.PSPreReq = "$true; ";

                                    var pRes = SWUpdate._RunPS(SWCheck.PSPreReq);
                                    if (pRes.Count > 0)
                                    {
                                        try
                                        {
                                            //Check PreReq for all Installation-types of the Software
                                            if ((bool)pRes[0].BaseObject)
                                            {
                                                SoftwareUpdate = new SWUpdate(SWCheck);
                                                return SoftwareUpdate;
                                            }
                                        }
                                        catch
                                        {
                                            continue;
                                        }
                                    }
                                    else
                                    {
                                        continue;
                                    }
                                }
                            }

                            if ((bool)SWUpdate._RunPS(SW.PSPreReq).Last().BaseObject)
                            {
                                SoftwareUpdate = new SWUpdate(SW);
                                return SoftwareUpdate;
                            }
                        }
                    }

                    return null;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                    return null;
                }
            });

            return await tRes;
        }

        internal static string _getTimeToken()
        {
            byte[] time = BitConverter.GetBytes(DateTime.UtcNow.ToBinary());
            byte[] key = Guid.NewGuid().ToByteArray();
            return Convert.ToBase64String(time.Concat(key).ToArray());
        }

        internal static AddSoftware Parse(string sJSON)
        {
            try
            {
                AddSoftware lRes = JsonConvert.DeserializeObject<AddSoftware>(sJSON);
                lRes.PreRequisites = lRes.PreRequisites.Where(x => !string.IsNullOrEmpty(x)).ToArray();
                return lRes;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }



            return new AddSoftware();
        }

        internal static AddSoftware ParseJSON(string sFile)
        {
            if (File.Exists(sFile))
            {
                try
                {
                    string sJson = File.ReadAllText(sFile);
                    AddSoftware lRes;

                    //Check if it's an Arrya (new in V2)
                    if (sJson.TrimStart().StartsWith("["))
                    {
                        List<AddSoftware> lItems = JsonConvert.DeserializeObject<List<AddSoftware>>(sJson);
                        lRes = lItems[0];
                    }
                    else
                    {
                        lRes = JsonConvert.DeserializeObject<AddSoftware>(sJson);
                    }

                    if (lRes.PreRequisites != null)
                    {
                        lRes.PreRequisites = lRes.PreRequisites.Where(x => !string.IsNullOrEmpty(x)).ToArray();
                    }
                    else
                        lRes.PreRequisites = new string[0];
                    return lRes;
                }
                catch { }
            }

            return new AddSoftware();
        }
    }

    /// <summary>
    /// SWUpdate Class
    /// </summary>
    public class SWUpdate
    {
        public string ContentPath = "";
        public bool SendFeedback = true;
        public AddSoftware SW;
        internal DLTask downloadTask;

        private ReaderWriterLockSlim UILock = new ReaderWriterLockSlim();

        //Constructor
        public SWUpdate(AddSoftware Software)
        {
            SW = Software;
            //downloadTask = new DLTask();
            downloadTask = new DLTask() { ProductName = SW.ProductName, ProductVersion = SW.ProductVersion, Manufacturer = SW.Manufacturer, ShortName = SW.ShortName, Files = SW.Files, UnInstalled = false, Installed = false, Installing = false, IconURL = SW.IconURL };
            downloadTask.SWUpd = this;

            if (SW.Files == null)
                SW.Files = new List<contentFiles>();
            if (SW.PreRequisites == null)
                SW.PreRequisites = new string[0];

            foreach (contentFiles vFile in SW.Files)
            {
                if (string.IsNullOrEmpty(vFile.HashType))
                    vFile.HashType = "MD5";
            }
        }

        //Constructor
        public SWUpdate(string ProductName, string ProductVersion, string Manufacturer, bool NoPreReqCheck = false)
        {
            SW = null;
            SW = new AddSoftware();

            SW.ProductName = ProductName;
            SW.ProductVersion = ProductVersion;
            SW.Manufacturer = Manufacturer;

            downloadTask = new DLTask() { ProductName = SW.ProductName, ProductVersion = SW.ProductVersion, Manufacturer = SW.Manufacturer, UnInstalled = false, Installed = false };
            downloadTask.SWUpd = this;


            //Get Install-type
            if (!GetInstallType(NoPreReqCheck))
            {
                SW = null;
                return;
            }

            downloadTask = new DLTask() { ProductName = SW.ProductName, ProductVersion = SW.ProductVersion, Manufacturer = SW.Manufacturer, ShortName = SW.ShortName, IconURL = SW.IconURL, Files = SW.Files, UnInstalled = false, Installed = false };

            if (SW == null)
            {
                //Load all MetaData for the specific SW
                foreach (AddSoftware SWCheck in RZRestAPIv2.GetSoftwares(SW.ProductName, SW.ProductVersion, SW.Manufacturer, RZRestAPIv2.CustomerID))
                {
                    if (string.IsNullOrEmpty(SWCheck.PSPreReq))
                        SWCheck.PSPreReq = "$true; ";

                    //Check PreReq for all Installation-types of the Software
                    if ((bool)SWUpdate._RunPS(SWCheck.PSPreReq)[0].BaseObject)
                    {
                        SW = SWCheck;
                        break;
                    }
                }

                //SW = RZRestAPIv2.GetSoftwares(ProductName, ProductVersion, Manufacturer, RZRestAPIv2.CustomerID).FirstOrDefault();

                if (SW.Files == null)
                    SW.Files = new List<contentFiles>();

                if (string.IsNullOrEmpty(SW.PSPreReq))
                    SW.PSPreReq = "$true; ";
            }

            if (SW.Files != null)
            {
                foreach (contentFiles vFile in SW.Files)
                {
                    if (string.IsNullOrEmpty(vFile.HashType))
                        vFile.HashType = "MD5";
                }
            }

            if (SW.PreRequisites == null)
                SW.PreRequisites = new string[0];

        }

        public SWUpdate(string ShortName)
        {
            SW = null;
            downloadTask = new DLTask();
            downloadTask.SWUpd = this;
            downloadTask.ShortName = ShortName;

            try
            {

                SW = new AddSoftware();

                string sBaseDir = AppDomain.CurrentDomain.BaseDirectory;

                //Always use local JSON-File if exists
                if (File.Exists(Path.Combine(sBaseDir, ShortName + ".json")))
                {
                    string sSWFile = Path.Combine(sBaseDir, ShortName + ".json");
                    SW = new SWUpdate(RZUpdater.ParseJSON(sSWFile)).SW;
                }
                else
                {
                    //Always use local JSON-File if exists
                    if (File.Exists(Path.Combine(Environment.ExpandEnvironmentVariables("%TEMP%"), ShortName + ".json")))
                    {
                        string sSWFile = Path.Combine(Environment.ExpandEnvironmentVariables("%TEMP%"), ShortName + ".json");
                        SW = new SWUpdate(RZUpdater.ParseJSON(sSWFile)).SW;
                    }
                    else
                    {
                        var oGetSW = RZRestAPIv2.GetCatalog(RZRestAPIv2.CustomerID).Where(t => t.ShortName.ToLower() == ShortName.ToLower()).FirstOrDefault(); // RZRestAPI.SWGet(ShortName).FirstOrDefault();
                        if (oGetSW != null)
                        {
                            SW.ProductName = oGetSW.ProductName;
                            SW.ProductVersion = oGetSW.ProductVersion;
                            SW.Manufacturer = oGetSW.Manufacturer;
                            SW.ShortName = ShortName;

                            if (SW.Architecture == null)
                            {
                                //Load all MetaData for the specific SW
                                foreach (AddSoftware SWCheck in RZRestAPIv2.GetSoftwares(SW.ProductName, SW.ProductVersion, SW.Manufacturer, RZRestAPIv2.CustomerID))
                                {
                                    if (string.IsNullOrEmpty(SWCheck.PSPreReq))
                                        SWCheck.PSPreReq = "$true; ";

                                    //Check PreReq for all Installation-types of the Software
                                    if ((bool)SWUpdate._RunPS(SWCheck.PSPreReq)[0].BaseObject)
                                    {
                                        SW = SWCheck;
                                        break;
                                    }
                                }

                                //SW = RZRestAPIv2.GetSoftwares(oGetSW.ProductName, oGetSW.ProductVersion, oGetSW.Manufacturer, RZRestAPIv2.CustomerID).FirstOrDefault();
                                if (SW == null) { Console.WriteLine("No SW"); }
                                SW.ShortName = ShortName;

                                if (SW.Files == null)
                                    SW.Files = new List<contentFiles>();
                                if (string.IsNullOrEmpty(SW.PSPreReq))
                                    SW.PSPreReq = "$true; ";
                            }
                        }

                        if (string.IsNullOrEmpty(SW.PSInstall))
                            return;

                        //Get Install-type
                        GetInstallType();
                    }
                }

                downloadTask = new DLTask() { ProductName = SW.ProductName, ProductVersion = SW.ProductVersion, Manufacturer = SW.Manufacturer, ShortName = SW.ShortName, IconURL = SW.IconURL, Files = SW.Files };
                foreach (contentFiles vFile in SW.Files)
                {
                    if (string.IsNullOrEmpty(vFile.HashType))
                        vFile.HashType = "MD5";
                }
                if (SW.PreRequisites == null)
                    SW.PreRequisites = new string[0];
            }
            catch { }
        }

        public delegate void ChangedEventHandler(object sender, EventArgs e);
        public event ChangedEventHandler Downloaded;
        public event EventHandler ProgressDetails = delegate { };

        private static event EventHandler DLProgress = delegate { };
        /// <summary>
        /// Run PowerShell
        /// </summary>
        /// <param name="PSScript">PowerShell Script</param>
        /// <returns></returns>
        public static PSDataCollection<PSObject> _RunPS(string PSScript, string WorkingDir = "", TimeSpan? Timeout = null)
        {
            TimeSpan timeout = new TimeSpan(0, 15, 0); //default timeout = 15min

            if (Timeout != null)
                timeout = (TimeSpan)Timeout;

            DateTime dStart = DateTime.Now;
            TimeSpan dDuration = DateTime.Now - dStart;
            using (PowerShell PowerShellInstance = PowerShell.Create())
            {
                if (!string.IsNullOrEmpty(WorkingDir))
                {
                    WorkingDir = Path.GetDirectoryName(WorkingDir);
                    PSScript = "Set-Location -Path '" + WorkingDir + "';" + PSScript;
                }

                PowerShellInstance.AddScript(PSScript);
                PSDataCollection<PSObject> outputCollection = new PSDataCollection<PSObject>();

                outputCollection.DataAdding += ConsoleOutput;
                PowerShellInstance.Streams.Error.DataAdding += ConsoleError;

                IAsyncResult async = PowerShellInstance.BeginInvoke<PSObject, PSObject>(null, outputCollection);
                while (async.IsCompleted == false && dDuration <= timeout)
                {
                    Thread.Sleep(200);
                    dDuration = DateTime.Now - dStart;
                }

                return outputCollection;
            }

        }

        /// <summary>
        /// Download a File
        /// </summary>
        /// <param name="URL"></param>
        /// <param name="FileName"></param>
        /// <returns>true = success; false = error</returns>
        public bool _DownloadFile2(string URL, string FileName, long FileSize = 0)
        {
            //Check if URL is HTTP, otherwise it must be a PowerShell
            if (!URL.StartsWith("http", StringComparison.CurrentCultureIgnoreCase) && !URL.StartsWith("ftp", StringComparison.CurrentCultureIgnoreCase))
            {
                var oResults = _RunPS(URL, FileName, new TimeSpan(2, 0, 0)); //2h timeout
                if (File.Exists(FileName))
                {
                    DLProgress((int)100, EventArgs.Empty);
                    ProgressDetails(new DLStatus() { Filename = FileName, URL = URL, PercentDownloaded = 100, DownloadedBytes = 100, TotalBytes = 100 }, EventArgs.Empty);
                    return true;
                }

                URL = oResults.FirstOrDefault().BaseObject.ToString();
            }

            try
            {
                Stream ResponseStream = null;
                WebResponse Response = null;

                Int64 ContentLength = 1;
                Int64 ContentLoaded = 0;
                Int64 ioldProgress = 0;
                Int64 iProgress = 0;

                if (URL.StartsWith("http", StringComparison.CurrentCultureIgnoreCase))
                {
                    //_DownloadFile(URL, FileName).Result.ToString();
                    var httpRequest = (HttpWebRequest)WebRequest.Create(URL);
                    httpRequest.UserAgent = "chocolatey command line";
                    httpRequest.AllowAutoRedirect = true;
                    httpRequest.MaximumAutomaticRedirections = 5;
                    Response = httpRequest.GetResponse();

                    // Get back the HTTP response for web server
                    //Response = (HttpWebResponse)httpRequest.GetResponse();
                    ResponseStream = Response.GetResponseStream();
                }

                if (URL.StartsWith("ftp", StringComparison.CurrentCultureIgnoreCase))
                {
                    var ftpRequest = (FtpWebRequest)WebRequest.Create(URL);
                    ftpRequest.ContentLength.ToString();
                    ftpRequest.GetResponse();


                    // Get back the HTTP response for web server
                    Response = (FtpWebResponse)ftpRequest.GetResponse();
                    ResponseStream = Response.GetResponseStream();

                    ContentLength = Response.ContentLength;
                }

                if (URL.StartsWith("<skip>", StringComparison.CurrentCultureIgnoreCase))
                {
                    DLProgress(100, EventArgs.Empty);
                    return true;
                }

                if (ResponseStream == null)
                    return false;

                // Define buffer and buffer size
                int bufferSize = 32768; //4096;
                byte[] buffer = new byte[bufferSize];
                int bytesRead = 0;

                // Read from response and write to file
                FileStream fileStream = File.Create(FileName);
                while ((bytesRead = ResponseStream.Read(buffer, 0, bufferSize)) != 0)
                {
                    if (FileSize > 0)
                    {
                        ContentLength = FileSize;
                    }
                    else
                    {
                        if (ContentLength == 1) { Int64.TryParse(Response.Headers.Get("Content-Length"), out ContentLength); }
                    }

                    fileStream.Write(buffer, 0, bytesRead);
                    ContentLoaded = ContentLoaded + bytesRead;

                    try
                    {
                        iProgress = (100 * ContentLoaded) / ContentLength;
                        //only send status on percent change
                        if (iProgress != ioldProgress)
                        {
                            if ((iProgress % 10) == 5 || (iProgress % 10) == 0)
                            {
                                try
                                {
                                    DLProgress((int)iProgress, EventArgs.Empty);
                                    ProgressDetails(new DLStatus() { Filename = FileName, URL = URL, PercentDownloaded = Convert.ToInt32(iProgress), DownloadedBytes = ContentLoaded, TotalBytes = ContentLength }, EventArgs.Empty);
                                    ioldProgress = iProgress;
                                }
                                catch { }
                            }
                        }
                    }
                    catch { }
                } // end while

                try
                {
                    if (ioldProgress != 100)
                    {
                        iProgress = (100 * ContentLoaded) / ContentLength;
                        DLProgress((int)iProgress, EventArgs.Empty);
                        ProgressDetails(new DLStatus() { Filename = FileName, URL = URL, PercentDownloaded = Convert.ToInt32(iProgress), DownloadedBytes = ContentLoaded, TotalBytes = ContentLength }, EventArgs.Empty);
                        ioldProgress = iProgress;
                    }
                }
                catch { }

                fileStream.Close();
                ResponseStream.Close();
                //Response.Close();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
                Console.WriteLine(ex.Message);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Check if PreReq from Install-Type are compliant (true).
        /// </summary>
        /// <returns>true = compliant; false = noncompliant</returns>
        public bool CheckDTPreReq()
        {
            if (SW != null)
            {

                //Is Product already installed ?
                try
                {
                    if (string.IsNullOrEmpty(SW.PSPreReq))
                        SW.PSPreReq = "$true; ";
                    //Already installed ?
                    if ((bool)_RunPS(SW.PSPreReq).Last().BaseObject)
                    {
                        return true;
                    }
                }
                catch { }
            }

            return false;
        }

        /// <summary>
        /// Check if Install-Type is installed
        /// </summary>
        /// <returns>true = installed ; false = not installed</returns>
        public bool CheckIsInstalled(bool sendProgressEvent)
        {
            if (SW != null)
            {

                //Is Product already installed ?
                try
                {
                    //Already installed ?
                    if ((bool)_RunPS(SW.PSDetection).Last().BaseObject)
                    {
                        UILock.EnterReadLock();
                        try
                        {
                            downloadTask.Installed = true;
                            downloadTask.Installing = false;
                            downloadTask.Downloading = false;
                            downloadTask.WaitingForDependency = false;
                            downloadTask.Error = false;
                            downloadTask.ErrorMessage = "";
                            downloadTask.PercentDownloaded = 100;

                            if (sendProgressEvent)
                                ProgressDetails(downloadTask, EventArgs.Empty);

                        }
                        finally { UILock.ExitReadLock(); }
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }

                downloadTask.Installed = false;
                downloadTask.Installing = false;
                downloadTask.Downloading = false;
            }
            else
            {
                downloadTask.Installed = false;
                downloadTask.Installing = false;
                downloadTask.Downloading = false;
                downloadTask.PercentDownloaded = 0;
            }

            if (sendProgressEvent)
                ProgressDetails(downloadTask, EventArgs.Empty);
            return false;
        }

        /// <summary>
        /// Download all related Files to %TEMP%
        /// </summary>
        /// <returns>true = success</returns>
        public async Task<bool> Download()
        {
            bool bAutoInstall = downloadTask.AutoInstall;
            downloadTask = new DLTask() { ProductName = SW.ProductName, ProductVersion = SW.ProductVersion, Manufacturer = SW.Manufacturer, ShortName = SW.ShortName, IconURL = SW.IconURL, Files = SW.Files };
            if (SW.PreRequisites != null)
            {
                if (SW.PreRequisites.Length > 0)
                {
                    downloadTask.WaitingForDependency = true;
                    downloadTask.AutoInstall = false;
                }
                else
                {
                    downloadTask.AutoInstall = bAutoInstall;
                }
            }
            else
            {
                downloadTask.AutoInstall = bAutoInstall;
            }
            downloadTask.Error = false;
            downloadTask.SWUpd = this;
            downloadTask.Downloading = true;
            ProgressDetails += SWUpdate_ProgressDetails;
            bool bResult = await Task.Run(() => _Download(false, Path.Combine(Environment.ExpandEnvironmentVariables("%TEMP%"), SW.ContentID))).ConfigureAwait(false);
            return bResult;
        }

        /// <summary>
        /// Download all related Files to %TEMP%
        /// </summary>
        /// <param name="Enforce">True = do not check if SW is already installed</param>
        /// <returns>true = success</returns>
        public async Task<bool> Download(bool Enforce)
        {
            return await Download(Enforce, Path.Combine(Environment.ExpandEnvironmentVariables("%TEMP%"), SW.ContentID));
        }

        public async Task<bool> Download(bool Enforce, string DLPath)
        {
            bool bAutoInstall = downloadTask.AutoInstall;
            downloadTask = new DLTask() { ProductName = SW.ProductName, ProductVersion = SW.ProductVersion, Manufacturer = SW.Manufacturer, ShortName = SW.ShortName, IconURL = SW.IconURL, Files = SW.Files };

            if (SW.PreRequisites != null)
            {
                if (SW.PreRequisites.Length > 0)
                {
                    downloadTask.WaitingForDependency = true;
                    downloadTask.AutoInstall = false;
                }
                else
                {
                    downloadTask.AutoInstall = bAutoInstall;
                }
            }
            else
            {
                downloadTask.AutoInstall = bAutoInstall;
            }
            downloadTask.Error = false;
            downloadTask.SWUpd = this;
            downloadTask.Downloading = true;
            ProgressDetails += SWUpdate_ProgressDetails;

            bool bResult = await Task.Run(() => _Download(Enforce, DLPath)).ConfigureAwait(false);
            return bResult;
        }

        public string GetDLPath()
        {
            return Environment.ExpandEnvironmentVariables("%TEMP%\\" + SW.ContentID.ToString());
        }

        public bool GetInstallType(bool bGetFirst = false)
        {
            //Only get other DeploymentTypes if Architecture is not defined...
            if (string.IsNullOrEmpty(this.SW.Architecture))
            {
                foreach (var DT in RZRestAPIv2.GetSoftwares(SW.ProductName, SW.ProductVersion, SW.Manufacturer, RZRestAPIv2.CustomerID))
                {
                    try
                    {
                        //Check PreReqs
                        try
                        {
                            if (!string.IsNullOrEmpty(DT.PSPreReq))
                            {
                                if (!bGetFirst)
                                {
                                    if (!(bool)_RunPS(DT.PSPreReq).Last().BaseObject)
                                        continue;
                                }
                            }
                        }
                        catch { continue; }

                        SW = DT;

                        return true;
                    }
                    catch { }
                }

                return false;
            }

            return true;
        }

        public async Task<bool> Install(bool Force = false, bool Retry = false)
        {
            bool msiIsRunning = false;
            bool RZisRunning = false;
            do
            {
                //Check if MSI is running...
                try
                {
                    using (var mutex = Mutex.OpenExisting(@"Global\_MSIExecute"))
                    {
                        msiIsRunning = true;
                        if (Retry)
                        {
                            Console.WriteLine("Warning: Windows-Installer setup is already running!... waiting...");
                            Thread.Sleep(new TimeSpan(0, 0, 10));
                        }
                        else
                            return false;
                    }
                    GC.Collect();
                }
                catch
                {
                    msiIsRunning = false;
                }


                //Check if RuckZuckis running...
                try
                {
                    using (var mutex = Mutex.OpenExisting(@"Global\RuckZuck"))
                    {
                        RZisRunning = true;
                        if (Retry)
                        {
                            Console.WriteLine("Warning: RuckZuck setup is already running!... waiting...");
                            Thread.Sleep(new TimeSpan(0, 0, 10));
                        }
                        else
                            return false;
                    }
                    GC.Collect();
                }
                catch
                {
                    RZisRunning = false;
                }
            }
            while (msiIsRunning || RZisRunning);

            bool bMutexCreated = false;
            bool bResult = false;

            using (Mutex mutex = new Mutex(false, "Global\\RuckZuck", out bMutexCreated))
            {
                bResult = await Task.Run(() => _Install(Force)).ConfigureAwait(false);

                if (bMutexCreated)
                    mutex.Close();
            }
            GC.Collect();
            return bResult;


        }

        public async Task<bool> UnInstall(bool Force = false, bool Retry = false)
        {
            bool msiIsRunning = false;
            bool RZisRunning = false;
            do
            {
                //Check if MSI is running...
                try
                {
                    using (var mutex = Mutex.OpenExisting(@"Global\_MSIExecute"))
                    {
                        msiIsRunning = true;
                        if (Retry)
                        {
                            Console.WriteLine("Warning: Windows-Installer setup is already running!... waiting...");
                            Thread.Sleep(new TimeSpan(0, 0, 10));
                        }
                        else
                            return false;
                    }
                    GC.Collect();
                }
                catch
                {
                    msiIsRunning = false;
                }


                //Check if RuckZuckis running...
                try
                {
                    using (var mutex = Mutex.OpenExisting(@"Global\RuckZuck"))
                    {
                        RZisRunning = true;
                        if (Retry)
                        {
                            Console.WriteLine("Warning: RuckZuck setup is already running!... waiting...");
                            Thread.Sleep(new TimeSpan(0, 0, 10));
                        }
                        else
                            return false;
                    }
                    GC.Collect();
                }
                catch
                {
                    RZisRunning = false;
                }
            }
            while (msiIsRunning || RZisRunning);

            bool bMutexCreated = false;
            bool bResult = false;

            using (Mutex mutex = new Mutex(false, "Global\\RuckZuck", out bMutexCreated))
            {
                bResult = await Task.Run(() => _UnInstall(Force)).ConfigureAwait(false);

                if (bMutexCreated)
                    mutex.Close();
            }
            GC.Collect();
            return bResult;
        }

        private static void ConsoleError(object sender, DataAddingEventArgs e)
        {
            if (e.ItemAdded != null)
                Console.WriteLine("ERROR:" + e.ItemAdded.ToString());
        }

        private static void ConsoleOutput(object sender, DataAddingEventArgs e)
        {
            //if (e.ItemAdded != null)
            //    Console.WriteLine(e.ItemAdded.ToString());
        }

        private bool _checkFileMd5(string FilePath, string MD5)
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

        //    return true;
        //}
        private bool _checkFileSHA1(string FilePath, string SHA1)
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

        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine(ex.Message);
        //        return false;
        //    }
        private bool _checkFileSHA256(string FilePath, string SHA256)
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

        //                using (Stream streamToWriteTo = File.Open(fileToWriteTo, FileMode.Create))
        //                {
        //                    await streamToReadFrom.CopyToAsync(streamToWriteTo);
        //                }
        //                Console.WriteLine("Donwloaded: " + URL);
        //            }
        //        }
        private bool _checkFileX509(string FilePath, string X509)
        {
            try
            {
                var Cert = X509Certificate.CreateFromSignedFile(FilePath);
                if (Cert.GetCertHashString().ToLower().Replace(" ", "") == X509.ToLower())
                {
                    return AuthenticodeTools.IsTrusted(FilePath);
                }
                else
                    return false;

            }
            catch
            {
                return false;
            }
        }

        private bool _Download(bool Enforce, string DLPath)
        {
            bool bError = false;
            ContentPath = DLPath;
            if (!Enforce)
            {
                //Check if it's still required
                try
                {
                    if (CheckIsInstalled(true))
                    {
                        if (Downloaded != null)
                            Downloaded(downloadTask, EventArgs.Empty);
                        return true;
                    }
                }
                catch { }
            }
            if (SW.Files == null)
                SW.Files = new List<contentFiles>();

            //only XML File contains Files
            if (SW.Files.Count() > 0)
            {
                bool bDLSuccess = false;
                foreach (var vFile in SW.Files)
                {
                    try
                    {
                        if (string.IsNullOrEmpty(vFile.URL))
                        {
                            downloadTask.PercentDownloaded = 100;
                            ProgressDetails(downloadTask, EventArgs.Empty);
                            continue;
                        }

                        string sDir = DLPath; // Path.Combine(Environment.ExpandEnvironmentVariables(DLPath), SW.ContentID);

                        string sFile = Path.Combine(sDir, vFile.FileName);

                        if (!Directory.Exists(sDir))
                            Directory.CreateDirectory(sDir);

                        bool bDownload = true;

                        //Check File-Hash on existing Files...
                        if (File.Exists(sFile))
                        {
                            if (string.IsNullOrEmpty(vFile.FileHash))
                            {
                                File.Delete(sFile);
                            }
                            else
                            {
                                if (string.IsNullOrEmpty(vFile.HashType))
                                    vFile.HashType = "MD5";

                                if (vFile.HashType.ToUpper() == "MD5")
                                {
                                    if (!_checkFileMd5(sFile, vFile.FileHash))
                                    {
                                        Console.WriteLine("Hash mismatch on existing File " + vFile.FileName);
                                        File.Delete(sFile); //Hash mismatch
                                    }
                                    else
                                        bDownload = false; //Do not download, Hash is valid   
                                }
                                if (vFile.HashType.ToUpper() == "SHA1")
                                {
                                    if (!_checkFileSHA1(sFile, vFile.FileHash))
                                        File.Delete(sFile); //Hash mismatch
                                    else
                                        bDownload = false; //Do not download, Hash is valid  
                                }
                                if (vFile.HashType.ToUpper() == "SHA256")
                                {
                                    if (!_checkFileSHA256(sFile, vFile.FileHash))
                                        File.Delete(sFile); //Hash mismatch
                                    else
                                        bDownload = false; //Do not download, Hash is valid  
                                }

                                if (vFile.HashType.ToUpper() == "X509")
                                {
                                    if (!_checkFileX509(sFile, vFile.FileHash))
                                        File.Delete(sFile); //Hash mismatch
                                    else
                                        bDownload = false; //Do not download, Hash is valid  
                                }
                            }
                        }

                        if (bDownload)
                        {
                            downloadTask.PercentDownloaded = 0;
                            downloadTask.Downloading = true;
                            ProgressDetails(downloadTask, EventArgs.Empty);

                            if (!_DownloadFile2(vFile.URL, sFile, vFile.FileSize))
                            {
                                downloadTask.Error = true;
                                downloadTask.PercentDownloaded = 0;
                                downloadTask.ErrorMessage = "ERROR: download failed... " + vFile.FileName;
                                Console.WriteLine("ERROR: download failed... " + vFile.FileName);
                                ProgressDetails(downloadTask, EventArgs.Empty);
                                File.Delete(sFile);
                                if (SendFeedback)
                                    RZRestAPIv2.Feedback(SW.ProductName, SW.ProductVersion, SW.Manufacturer, "false", System.Reflection.Assembly.GetExecutingAssembly().GetName().Name, "download failed", RZRestAPIv2.CustomerID).ConfigureAwait(false);
                                return false;
                            }
                            else
                            {
                                bDLSuccess = true;
                            }

                            //Sleep 1s to complete
                            Thread.Sleep(1000);
                            ProgressDetails(downloadTask, EventArgs.Empty);
                            //downloadTask.Downloading = false;

                        }
                        else
                        {
                            downloadTask.PercentDownloaded = 100;
                            downloadTask.Downloading = false;
                        }

                        //Only Check Hash if downloaded
                        if (!string.IsNullOrEmpty(vFile.FileHash) && bDownload)
                        {
                            if (!vFile.URL.StartsWith("http", StringComparison.InvariantCultureIgnoreCase) && !File.Exists(sFile)) 
                            {
                                downloadTask.PercentDownloaded = 100;
                                downloadTask.Error = false;
                            } 
                            else
                            {
                                if (string.IsNullOrEmpty(vFile.HashType))
                                    vFile.HashType = "MD5";

                                //Check if there is a File
                                long iFileSize = 0;
                                try
                                {
                                    FileInfo fi = new FileInfo(sFile);
                                    iFileSize = fi.Length;
                                }
                                catch { }

                                if (iFileSize == 0)
                                {
                                    downloadTask.Error = true;
                                    downloadTask.PercentDownloaded = 0;
                                    downloadTask.ErrorMessage = "ERROR: empty File... " + vFile.FileName;
                                    Console.WriteLine("ERROR: empty File... " + vFile.FileName);
                                    ProgressDetails(downloadTask, EventArgs.Empty);
                                    File.Delete(sFile);
                                    return false;
                                }
                                else
                                {


                                    //Check default MD5 Hash
                                    if (vFile.HashType.ToUpper() == "MD5")
                                    {
                                        if (!_checkFileMd5(sFile, vFile.FileHash))
                                        {
                                            downloadTask.Error = true;
                                            downloadTask.PercentDownloaded = 0;
                                            downloadTask.ErrorMessage = "ERROR: Hash mismatch on File " + vFile.FileName;
                                            Console.WriteLine("ERROR: Hash mismatch on File " + vFile.FileName);
                                            File.Delete(sFile);
                                            if (SendFeedback)
                                                RZRestAPIv2.Feedback(SW.ProductName, SW.ProductVersion, SW.Manufacturer, "false", System.Reflection.Assembly.GetExecutingAssembly().GetName().Name, "Hash mismatch").ConfigureAwait(false);
                                            bError = true;
                                        }
                                        else
                                        {
                                            downloadTask.PercentDownloaded = 100;
                                        }
                                    }

                                    //Check default SHA1 Hash
                                    if (vFile.HashType.ToUpper() == "SHA1")
                                    {
                                        if (!_checkFileSHA1(sFile, vFile.FileHash))
                                        {
                                            downloadTask.Error = true;
                                            downloadTask.PercentDownloaded = 0;
                                            downloadTask.ErrorMessage = "ERROR: Hash mismatch on File " + vFile.FileName;
                                            Console.WriteLine("ERROR: Hash mismatch on File " + vFile.FileName);
                                            File.Delete(sFile);
                                            if (SendFeedback)
                                                RZRestAPIv2.Feedback(SW.ProductName, SW.ProductVersion, SW.Manufacturer, "false", System.Reflection.Assembly.GetExecutingAssembly().GetName().Name, "Hash mismatch", RZRestAPIv2.CustomerID).ConfigureAwait(false);
                                            bError = true;
                                        }
                                        else
                                        {
                                            downloadTask.PercentDownloaded = 100;
                                        }
                                    }

                                    //Check default SHA256 Hash
                                    if (vFile.HashType.ToUpper() == "SHA256")
                                    {
                                        if (!_checkFileSHA256(sFile, vFile.FileHash))
                                        {
                                            downloadTask.Error = true;
                                            downloadTask.PercentDownloaded = 0;
                                            downloadTask.ErrorMessage = "ERROR: Hash mismatch on File " + vFile.FileName;
                                            Console.WriteLine("ERROR: Hash mismatch on File " + vFile.FileName);
                                            File.Delete(sFile);
                                            if (SendFeedback)
                                                RZRestAPIv2.Feedback(SW.ProductName, SW.ProductVersion, SW.Manufacturer, "false", System.Reflection.Assembly.GetExecutingAssembly().GetName().Name, "Hash mismatch", RZRestAPIv2.CustomerID).ConfigureAwait(false);
                                            bError = true;
                                        }
                                        else
                                        {
                                            downloadTask.PercentDownloaded = 100;
                                        }
                                    }

                                    if (vFile.HashType.ToUpper() == "X509")
                                    {
                                        if (!_checkFileX509(sFile, vFile.FileHash))
                                        {
                                            downloadTask.Error = true;
                                            downloadTask.PercentDownloaded = 0;
                                            downloadTask.ErrorMessage = "ERROR: Signature mismatch on File " + vFile.FileName;
                                            Console.WriteLine("ERROR: Signature mismatch on File " + vFile.FileName);
                                            File.Delete(sFile);
                                            if (SendFeedback)
                                                RZRestAPIv2.Feedback(SW.ProductName, SW.ProductVersion, SW.Manufacturer, "false", System.Reflection.Assembly.GetExecutingAssembly().GetName().Name, "Signature mismatch", RZRestAPIv2.CustomerID).ConfigureAwait(false);
                                            bError = true;
                                        }
                                        else
                                        {
                                            downloadTask.PercentDownloaded = 100;
                                        }
                                    }
                                }
                            }
                        }

                    }
                    catch (Exception ex)
                    {
                        downloadTask.PercentDownloaded = 0;
                        downloadTask.ErrorMessage = ex.Message;
                        Console.WriteLine("ERROR: " + ex.Message);
                        bError = true;
                    }
                }

                if (SendFeedback && bDLSuccess)
                {
                    RZRestAPIv2.IncCounter(SW.ShortName, customerid: RZRestAPIv2.CustomerID);
                }
            }
            else
            {
                downloadTask.PercentDownloaded = 100;
            }

            downloadTask.Downloading = false;


            if (bError)
            {
                downloadTask.PercentDownloaded = 0;
                downloadTask.Error = true;
            }
            else
            {
                downloadTask.Error = false;
                downloadTask.ErrorMessage = "";
            }

            ProgressDetails(downloadTask, EventArgs.Empty);

            if (Downloaded != null)
                Downloaded(downloadTask, EventArgs.Empty);

            return !bError;
        }
        /// <summary>
        /// Install a SWUpdate
        /// </summary>
        /// <param name="Force">Do not check if SW is already installed.</param>
        /// <returns></returns>
        private bool _Install(bool Force = false)
        {
            bool bError = false;

            //Check if Installer is already running
            if (downloadTask.Installing)
            {
                Thread.Sleep(1500);
                return CheckIsInstalled(true); ;
            }

            downloadTask.Installing = true;
            if (!CheckDTPreReq())
            {

                Console.WriteLine("Requirements not valid. Installation will not start.");
                downloadTask.Installing = false;
                downloadTask.Installed = false;
                downloadTask.Error = true;
                downloadTask.ErrorMessage = "Requirements not valid. Installation will not start.";
                ProgressDetails(this.downloadTask, EventArgs.Empty);

                if (SendFeedback)
                    RZRestAPIv2.Feedback(SW.ProductName, SW.ProductVersion, SW.Manufacturer, "false", System.Reflection.Assembly.GetExecutingAssembly().GetName().Name, "Requirements not valid. Installation will not start.", RZRestAPIv2.CustomerID).ConfigureAwait(false);

                return false;
            }

            //Is Product already installed ?
            try
            {
                if (!Force)
                {
                    //Already installed ?
                    if (CheckIsInstalled(true))
                    {
                        return true;
                    }
                }

                downloadTask.Installing = true;

                //Set CurrentDir and $Folder variable
                string sFolder = ContentPath;
                if (string.IsNullOrEmpty(ContentPath))
                {
                    string sLocalPath = Environment.ExpandEnvironmentVariables("%TEMP%");
                    sFolder = Path.Combine(sLocalPath, SW.ContentID.ToString());
                }

                //prevent issue with 8.3 naming in PowerShell...
                string psPath = string.Format("Set-Location (gi \"{0}\").fullname -ErrorAction SilentlyContinue; $Folder = \"{0}\";", sFolder);
                int iExitCode = -1;

                //Run Install Script
                if (!string.IsNullOrEmpty(SW.PSInstall))
                {
                    try
                    {
                        downloadTask.Installing = true;
                        ProgressDetails(this.downloadTask, EventArgs.Empty);

                        var oResult = _RunPS(psPath + SW.PSPreInstall + ";" + SW.PSInstall + ";" + SW.PSPostInstall + ";$ExitCode", "", new TimeSpan(0, 60, 0));

                        try
                        {
                            iExitCode = ((int)oResult.Last().BaseObject);
                        }
                        catch { }

                        //Wait 1s to let the installer close completely...
                        System.Threading.Thread.Sleep(1100);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("PS ERROR: " + ex.Message);
                    }

                    //InstProgress(this, EventArgs.Empty);
                }

                //is installed ?
                if (CheckIsInstalled(false))
                {
                    ProgressDetails(downloadTask, EventArgs.Empty);
                    if (SendFeedback)
                        RZRestAPIv2.Feedback(SW.ProductName, SW.ProductVersion, SW.Manufacturer, "true", System.Reflection.Assembly.GetExecutingAssembly().GetName().Name, "Ok...", RZRestAPIv2.CustomerID).ConfigureAwait(false); ;
                    return true;
                }
                else
                {
                    Console.WriteLine("WARNING: Product not detected after installation.");
                    //if (iExitCode != 0 && iExitCode != 3010)
                    //{
                    //    if (SendFeedback)
                    //        RZRestAPI.Feedback(SW.ProductName, SW.ProductVersion, SW.Manufacturer, SW.Architecture, "false", sUserName, "Product not detected after installation.").ConfigureAwait(false); ;
                    //}

                    if (SendFeedback)
                        RZRestAPIv2.Feedback(SW.ProductName, SW.ProductVersion, SW.Manufacturer, "false", System.Reflection.Assembly.GetExecutingAssembly().GetName().Name, "Product not detected after installation.", RZRestAPIv2.CustomerID).ConfigureAwait(false); ;

                    downloadTask.Error = true;
                    downloadTask.ErrorMessage = "WARNING: Product not detected after installation.";
                    downloadTask.Installed = false;
                    downloadTask.Installing = false;
                    ProgressDetails(downloadTask, EventArgs.Empty);
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR: " + ex.Message);
                RZRestAPIv2.Feedback(SW.ProductName, SW.ProductVersion, SW.Manufacturer, "false", System.Reflection.Assembly.GetExecutingAssembly().GetName().Name, "ERROR: " + ex.Message, RZRestAPIv2.CustomerID).ConfigureAwait(false); ;
                downloadTask.Error = true;
                downloadTask.ErrorMessage = "WARNING: Product not detected after installation.";
                downloadTask.Installed = false;
                downloadTask.Installing = false;
                bError = true;
            }

            //RZRestAPI.Feedback(SW.ProductName, SW.ProductVersion, (!bError).ToString(), "RZUpdate", "");
            ProgressDetails(this.downloadTask, EventArgs.Empty);
            return !bError;
        }

        private bool _UnInstall(bool Force = false)
        {
            //Check if Installer is already running
            if (downloadTask.Installing)
            {
                Thread.Sleep(1500);
                CheckIsInstalled(true);
                //ProgressDetails(this.downloadTask, EventArgs.Empty);
                return true;

            }

            downloadTask.Installing = true;

            var tGetSWRepo = Task.Run(() =>
            {
                bool bError = false;

                if (!CheckDTPreReq() && !Force)
                {

                    Console.WriteLine("Requirements not valid. Installation will not start.");
                    downloadTask.Installing = false;
                    downloadTask.Installed = false;
                    downloadTask.Error = true;
                    downloadTask.ErrorMessage = "Requirements not valid. Installation will not start.";
                    ProgressDetails(this.downloadTask, EventArgs.Empty);

                    if (SendFeedback)
                        RZRestAPIv2.Feedback(SW.ProductName, SW.ProductVersion, SW.Manufacturer, "false", System.Reflection.Assembly.GetExecutingAssembly().GetName().Name, "Requirements not valid. Installation will not start.", RZRestAPIv2.CustomerID).ConfigureAwait(false); ;

                    return false;
                }

                //Is Product already installed ?
                try
                {
                    if (!Force)
                    {
                        //Already installed ?
                        if (!CheckIsInstalled(false))
                        {
                            downloadTask.Installed = false;
                            downloadTask.Installing = false;
                            downloadTask.UnInstalled = true;
                            downloadTask.Error = false;
                            return true;
                        }
                    }

                    //Check if Installer is already running
                    while (downloadTask.Installing)
                    {
                        Thread.Sleep(1500);
                        if (!CheckIsInstalled(false))
                        {
                            downloadTask.Installed = false;
                            downloadTask.Installing = false;
                            downloadTask.UnInstalled = true;
                            downloadTask.Error = false;
                            return true;
                        }
                    }

                    downloadTask.Installing = true;

                    int iExitCode = -1;

                    //Run Install Script
                    if (!string.IsNullOrEmpty(SW.PSUninstall))
                    {
                        try
                        {
                            downloadTask.Installing = true;
                            ProgressDetails(this.downloadTask, EventArgs.Empty);

                            var oResult = _RunPS(SW.PSUninstall + ";$ExitCode", "", new TimeSpan(0, 30, 0));

                            try
                            {
                                iExitCode = ((int)oResult.Last().BaseObject);
                            }
                            catch { }

                            //Wait 500ms to let the installer close completely...
                            System.Threading.Thread.Sleep(550);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("PS ERROR: " + ex.Message);
                        }

                        downloadTask.Installing = false;
                        //InstProgress(this, EventArgs.Empty);
                    }

                    //is installed ?
                    if (!CheckIsInstalled(false))
                    {
                        downloadTask.Installed = false;
                        downloadTask.Installing = false;
                        downloadTask.UnInstalled = true;
                        downloadTask.Error = false;
                        //RZRestAPI.Feedback(SW.ProductName, SW.ProductVersion, "true", "RZUpdate", "Uninstalled...");
                        ProgressDetails(downloadTask, EventArgs.Empty);
                        return true;
                    }
                    else
                    {
                        Console.WriteLine("WARNING: Product is still installed.");
                        downloadTask.Error = true;
                        downloadTask.ErrorMessage = "WARNING: Product is still installed.";
                        downloadTask.Installed = false;
                        downloadTask.Installing = false;
                        ProgressDetails(downloadTask, EventArgs.Empty);
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("ERROR: " + ex.Message);
                    downloadTask.Error = true;
                    downloadTask.ErrorMessage = "WARNING: Product is still installed.";
                    downloadTask.Installed = false;
                    downloadTask.Installing = false;
                    bError = true;
                }

                //RZRestAPI.Feedback(SW.ProductName, SW.ProductVersion, (!bError).ToString(), "RZUpdate", "");
                ProgressDetails(this.downloadTask, EventArgs.Empty);
                return !bError;
            });

            return true;
        }

        private void SWUpdate_ProgressDetails(object sender, EventArgs e)
        {
            if (sender.GetType() == typeof(DLStatus))
            {
                try
                {
                    DLStatus dlStatus = sender as DLStatus;
                    downloadTask.Installing = false;
                    downloadTask.Downloading = true;
                    downloadTask.DownloadedBytes = dlStatus.DownloadedBytes;
                    downloadTask.PercentDownloaded = dlStatus.PercentDownloaded;
                    downloadTask.TotalBytes = dlStatus.TotalBytes;
                }
                catch { }
            }
        }
        //private static async Task<bool> _DownloadFile(string URL, string FileName)
        //{
        //    try
        //    {
        //        HttpClientHandler handler = new HttpClientHandler();
        //        handler.AllowAutoRedirect = true;
        //        handler.MaxAutomaticRedirections = 5;

        //        //DotNetCore2.0
        //        //handler.CheckCertificateRevocationList = false;
        //        //handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => { return true; }; //To prevent Issue with FW

        //        using (HttpClient oClient = new HttpClient(handler))
        //        {
        //            oClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "chocolatey command line");

        //            using (HttpResponseMessage response = await oClient.GetAsync(URL, HttpCompletionOption.ResponseHeadersRead))
        //            using (Stream streamToReadFrom = await response.Content.ReadAsStreamAsync())
        //            {
        //                string fileToWriteTo = FileName; // Path.GetTempFileName();
    }
}
