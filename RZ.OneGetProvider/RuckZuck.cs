using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using PackageManagement.Sdk;

using System.Web;
using System.Net;
using System.IO;
using System.Security.Cryptography;
using System.Management.Automation;
using System.Collections.ObjectModel;
using Microsoft.Win32;
using System.Security.Cryptography;
using System.Security;
using RuckZuck_WCF;

namespace PackageManagement
{
    partial class PackageProvider 
    {
        // RuckZuck Code Here

        private static string _AuthenticationToken = "";
        private RZUpdate.RZScan oScan;
        private RZUpdate.RZUpdater oUpdate;

        public List<AddSoftware> lSoftware = new List<AddSoftware>();
        public static string WebServiceURL = "https://ruckzuck.azurewebsites.net/wcf/RZService.svc";

        private DateTime dLastTokenRefresh = new DateTime();

        /// <summary>
        /// Initialize the RuckZuck Web-Service
        /// </summary>
        /// <param name="request"></param>
        private void _initRZ(Request request)
        {
            try
            {
                if (Properties.Settings.Default.Location.StartsWith("https:"))
                {
                    RZRestAPI.sURL = Properties.Settings.Default.Location;
                }
                else
                {
                    Properties.Settings.Default.Location = RZRestAPI.sURL;
                    Properties.Settings.Default.Save();
                }

                _reAuthenticate(request);

                oScan = new RZUpdate.RZScan(false, false);
                oUpdate = new RZUpdate.RZUpdater();

                /*
                //Username and PW is not yet implemented
                if (!string.IsNullOrEmpty(Properties.Settings.Default.Username))
                {
                    _AuthenticationToken = RZApi.AuthenticateUser(Properties.Settings.Default.Username, ToInsecureString(DecryptString(Properties.Settings.Default.Password)));
                }
                else
                {
                    _AuthenticationToken = RZApi.AuthenticateUser("FreeRZ", GetTimeToken());
                }

                request.Debug("RZ Token: " + _AuthenticationToken);

                RZApi.SecuredWebServiceHeaderValue = new RZ.SecuredWebServiceHeader() { AuthenticatedToken = _AuthenticationToken };
            
                dLastTokenRefresh = DateTime.Now;*/
            }
            catch { }
        }

        private string _providerVersion
        {
            get
            {
                return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(); // "1.0.0.0";
            }
        }

        private void _getDynamicOptions(string category, Request request)
        {
            switch ((category ?? string.Empty).ToLowerInvariant())
            {
                case "install":
                    // todo: put any options required for install/uninstall/getinstalledpackages
                    request.YieldDynamicOption("SkipDependencies", Constants.OptionType.Switch, false);
                    request.YieldDynamicOption("LocalPath", Constants.OptionType.Folder, false);
                    break;

                case "provider":
                    request.YieldDynamicOption("Username", Constants.OptionType.String, false);
                    request.YieldDynamicOption("Password", Constants.OptionType.String, false);
                    request.YieldDynamicOption("ContentURL", Constants.OptionType.String, false);
                    // todo: put any options used with this provider. Not currently used.

                    break;

                case "source":
                    // todo: put any options for package sources

                    break;

                case "package":
                    // todo: put any options used when searching for packages
                    request.YieldDynamicOption("Contains", Constants.OptionType.String, false);
                    request.YieldDynamicOption("Updates", Constants.OptionType.Switch, false);
                    break;
            }
        }

        private void _resolvePackageSources(Request request)
        {
            bool bValidated = false;
            bool bIsTrusted = false;
            try
            {
                _reAuthenticate(request); //Check if AuthToken is still valid
                if (!string.IsNullOrEmpty(_AuthenticationToken))
                {
                    bValidated = true;
                }

                if (string.Equals(Properties.Settings.Default.Location, WebServiceURL, StringComparison.InvariantCultureIgnoreCase))
                    bIsTrusted = true;
            }
            catch(Exception ex)
            {
                request.Debug("RZ112: " + ex.Message);
                return; 
            }

            request.YieldPackageSource("RuckZuck", Properties.Settings.Default.Location, bIsTrusted, true, bValidated);
        }

        private void _addPackageSource(string name, string location, bool trusted, Request request)
        {
            Properties.Settings.Default.Location = location;

            //Set default URL if no loaction is specified
            if (!string.IsNullOrEmpty(location))
            {
                Properties.Settings.Default.Location = location;
                Properties.Settings.Default.Save();
            }

            //RZRestAPI.sURL = Properties.Settings.Default.Location;

            string sUser = "FreeRZ";
            SecureString sPW = ToSecureString(GetTimeToken());


            if (request.OptionKeys.Contains("Username"))
            {
                Properties.Settings.Default.Username = request.GetOptionValue("Username");
                sUser = Properties.Settings.Default.Username;
            }
            else
                Properties.Settings.Default.Username = "";

            if (request.OptionKeys.Contains("Password"))
            {
                sPW = ToSecureString(request.GetOptionValue("Password"));
                Properties.Settings.Default.Password = EncryptString(sPW);
            }
            else
                Properties.Settings.Default.Password = "";

            if (request.OptionKeys.Contains("ContentURL"))
            {
                Properties.Settings.Default.ContentURL = request.GetOptionValue("ContentURL");
            }
            else
                Properties.Settings.Default.ContentURL = "";

            Properties.Settings.Default.Save();

            _AuthenticationToken = RZRestAPI.GetAuthToken(sUser, ToInsecureString(sPW));

            Guid gToken;
            if (!Guid.TryParse(_AuthenticationToken, out gToken))
            {
                request.Warning(_AuthenticationToken);
                dLastTokenRefresh = new DateTime();
                return;
            }
        }

        private void _removePackageSource(string name, Request request)
        {
            Properties.Settings.Default.Location = WebServiceURL;
            Properties.Settings.Default.Username = "";
            Properties.Settings.Default.Password = "";
            Properties.Settings.Default.Save();

            dLastTokenRefresh = new DateTime();

            //RZRestAPI.sURL = Properties.Settings.Default.Location;
        }

        /// <summary>
        /// Check if RuckZuck Token is still valid or request a new token if last refresh was mor than an hour
        /// </summary>
        /// <param name="request"></param>
        private void _reAuthenticate(Request request)
        {
            //Check if there is a token..
            Guid gToken;
            if (!Guid.TryParse(_AuthenticationToken, out gToken))
            {
                dLastTokenRefresh = new DateTime();
            }

            //Re-Authenticate after 30min
            if ((DateTime.Now - dLastTokenRefresh).TotalMinutes >= 30)
            {
                

                if (string.IsNullOrEmpty(Properties.Settings.Default.Location))
                {
                    //Properties.Settings.Default.Location = WebServiceURL;
                    //Properties.Settings.Default.Save();
                    
                }

                //RZRestAPI.sURL = Properties.Settings.Default.Location;

                if (!string.IsNullOrEmpty(Properties.Settings.Default.Username))
                {
                    _AuthenticationToken = RZRestAPI.GetAuthToken(Properties.Settings.Default.Username, ToInsecureString(DecryptString(Properties.Settings.Default.Password)));
                    dLastTokenRefresh = DateTime.Now;
                    request.Debug("RZ Account: " + Properties.Settings.Default.Username);
                }
                else
                {
                    _AuthenticationToken = RZRestAPI.GetAuthToken("FreeRZ", GetTimeToken());
                    dLastTokenRefresh = DateTime.Now;
                    request.Debug("RZ Account: FreeRZ");
                }

                if (!Guid.TryParse(_AuthenticationToken, out gToken))
                {
                    dLastTokenRefresh = new DateTime();
                    request.Warning(_AuthenticationToken);
                    _AuthenticationToken = "";
                    return;
                }

                request.Debug("RZ Authentication Token:" + _AuthenticationToken);
            }
        }

        private void _findPackage(string name, string requiredVersion, string minimumVersion, string maximumVersion, int id, Request request)
        {
            _reAuthenticate(request); //Check if AuthToken is still valid

            try
            {
                bool exactSearch = true;
                if (request.OptionKeys.Contains("Contains"))
                {
                    name = request.GetOptionValue("Contains");
                    request.Message("exact search disabled.");
                    exactSearch = false;
                }

                //Search all if no name is specified
                if (string.IsNullOrEmpty(name))
                    exactSearch = false;

                bool bUpdate = false;
                if (request.OptionKeys.Contains("Updates"))
                {
                    request.Message("check updates for installed Software.");
                    bUpdate = true;
                }

                List<GetSoftware> lResult = new List<GetSoftware>();

                //Get all installed SW
                if (bUpdate)
                {
                    oScan.CheckForUpdates = false;
                    oScan.SWScan().Wait();
                    oScan.CheckUpdates(null).Wait();
                    lSoftware = oScan.InstalledSoftware;


                    List<AddSoftware> RequiredUpdates = oScan.NewSoftwareVersions; // RZApi.CheckForUpdate(lSoftware.ToArray()).ToList().Where(t => t.Architecture != "new").ToList();
                    foreach (var SW in RequiredUpdates)
                    {
                        try
                        {
                            if (string.IsNullOrEmpty(name))
                            {
                                lResult.Add(new GetSoftware() { ProductName = SW.ProductName, ProductVersion = SW.ProductVersion, Manufacturer = SW.Manufacturer, Shortname = SW.Shortname, Description = SW.Description, ProductURL = SW.ProductURL });
                            }
                            else
                            {
                                if ((SW.ProductName.ToLowerInvariant() == name.ToLowerInvariant() | SW.Shortname.ToLowerInvariant() == name.ToLowerInvariant()) & exactSearch)
                                {
                                    lResult.Add(new GetSoftware() { ProductName = SW.ProductName, ProductVersion = SW.ProductVersion, Manufacturer = SW.Manufacturer, Shortname = SW.Shortname, Description = SW.Description, ProductURL = SW.ProductURL });
                                }
                                if ((SW.ProductName.ToLowerInvariant().Contains(name.ToLowerInvariant()) | SW.Shortname.ToLowerInvariant().Contains(name.ToLowerInvariant())) & !exactSearch)
                                {
                                    lResult.Add(new GetSoftware() { ProductName = SW.ProductName, ProductVersion = SW.ProductVersion, Manufacturer = SW.Manufacturer, Shortname = SW.Shortname, Description = SW.Description, ProductURL = SW.ProductURL });
                                }
                            }
                        }
                        catch { }
                    }
                    if (lResult.Count == 0)
                        request.Warning("No updates found...");
                }
                else
                {
                    if (string.IsNullOrEmpty(requiredVersion))
                    {
                        //Find by Shortname
                        if (exactSearch)
                        {
                            lResult = RZRestAPI.SWGet(name).OrderBy(t => t.Shortname).ToList();

                            if (lResult.Count == 0)
                            {
                                //Find any
                                lResult = RZRestAPI.SWResults(name).Where(t => t.ProductName == name).OrderBy(t => t.ProductName).ToList();
                            }
                        }
                        else
                        {
                            lResult = RZRestAPI.SWGet(name).OrderBy(t => t.Shortname).ToList();

                            if (lResult.Count == 0)
                            {
                                //Find any
                                lResult = RZRestAPI.SWResults(name).OrderBy(t => t.Shortname).ToList();
                            }
                        }
                    }
                    else
                    {
                        //Find by Shortname
                        if (exactSearch)
                        {
                            lResult = RZRestAPI.SWGet(name, requiredVersion).OrderBy(t => t.Shortname).ToList();
                        }
                        else
                        {
                            //Find any
                            lResult = RZRestAPI.SWResults(name).Where(t => t.ProductVersion == requiredVersion).OrderBy(t => t.Shortname).ToList();
                        }
                    }
                }


                if (minimumVersion != null)
                {
                    try
                    {
                        lResult = lResult.Where(p => Version.Parse(p.ProductVersion) >= Version.Parse(minimumVersion)).ToList();
                    }
                    catch
                    {
                        lResult = lResult.Where(p => p.ProductVersion == minimumVersion).ToList();
                    }
                }
                if (maximumVersion != null)
                {
                    try
                    {
                        lResult = lResult.Where(p => Version.Parse(p.ProductVersion) <= Version.Parse(maximumVersion)).ToList();
                    }
                    catch
                    {
                        lResult = lResult.Where(p => p.ProductVersion == maximumVersion).ToList();
                    }
                }


                foreach (var SW in lResult.OrderBy(t => t.Shortname))
                {
                    request.YieldSoftwareIdentity(SW.ProductName + ";" + SW.ProductVersion + ";" + SW.Manufacturer, SW.ProductName, SW.ProductVersion, "", SW.Description, Properties.Settings.Default.Location, name, SW.IconId.ToString(), SW.Shortname);
                    //Trust the original RucKZuck source
                    if (string.Equals(Properties.Settings.Default.Location, WebServiceURL, StringComparison.InvariantCultureIgnoreCase))
                    {
                        request.AddMetadata("FromTrustedSource", "True");
                    }
                }

            }
            catch (Exception ex)
            {
                request.Debug("E334:" + ex.Message);
                dLastTokenRefresh = new DateTime();
            }
        }

        /// <summary>
        /// Install a Software from RuckZuck repo
        /// </summary>
        /// <param name="fastPackageReference"></param>
        /// <param name="request"></param>
        private void _installPackage(string fastPackageReference, Request request)
        {
            _reAuthenticate(request); //Check if AuthToken is still valid

            bool bSkipDep = false;
            if (request.OptionKeys.Contains("SkipDependencies"))
            {
                request.Message("Skip all dependencies.");
                bSkipDep = true;
            }

            string sProd = fastPackageReference;
            string sVer = "";
            string sManu = "";

            if (fastPackageReference.Contains(";"))
            {
                try
                {
                    sProd = fastPackageReference.Split(';')[0].Trim();
                    sVer = fastPackageReference.Split(';')[1].Trim();
                    sManu = fastPackageReference.Split(';')[2].Trim();
                }
                catch { }
            }

            oUpdate.SoftwareUpdate.SW.ProductName = sProd;
            oUpdate.SoftwareUpdate.SW.ProductVersion = sVer;
            oUpdate.SoftwareUpdate.SW.Manufacturer = sManu;

            oUpdate.SoftwareUpdate.GetInstallType();

            oUpdate.SoftwareUpdate.Download(false).Result.ToString();

            if (oUpdate.SoftwareUpdate.Install(false, true).Result)
                request.Verbose(sManu + " " + sProd + " " + sVer + " installed.");
            else
                request.Verbose(sManu + " " + sProd + " " + sVer + " NOT installed.");
        }

        /// <summary>
        /// Uninstall a SW
        /// </summary>
        /// <param name="fastPackageReference"></param>
        /// <param name="request"></param>
        private void _uninstallPackage(string fastPackageReference, Request request)
        {
            _reAuthenticate(request); //Check if AuthToken is still valid

            string sProd = fastPackageReference;
            string sVer = "";
            string sManu = "";

            if (fastPackageReference.Contains(";"))
            {
                try
                {
                    sProd = fastPackageReference.Split(';')[0].Trim();
                    sVer = fastPackageReference.Split(';')[1].Trim();
                    sManu = fastPackageReference.Split(';')[2].Trim();
                }
                catch { }
            }

            if (string.IsNullOrEmpty(sVer))
            {
                sVer = lSoftware.First(t => t.ProductName == sProd).ProductVersion;
            }

            request.Debug(sProd);
            request.Debug(sVer);

            oUpdate.SoftwareUpdate.SW.ProductName = sProd;
            oUpdate.SoftwareUpdate.SW.ProductVersion = sVer;
            oUpdate.SoftwareUpdate.SW.Manufacturer = sManu;

            oUpdate.SoftwareUpdate.GetInstallType();

            if (!string.IsNullOrEmpty(oUpdate.SoftwareUpdate.SW.PSUninstall))
                RunPS(oUpdate.SoftwareUpdate.SW.PSUninstall);
        }

        private void _downloadPackage(string fastPackageReference, string location, Request request)
        {
            _reAuthenticate(request); //Check if AuthToken is still valid

            bool bSkipDep = false;
            if (request.OptionKeys.Contains("SkipDependencies"))
            {
                request.Message("Skip all dependencies.");
                bSkipDep = true;
            }

            string sProd = fastPackageReference;
            string sVer = "";
            string sManu = "";

            if (fastPackageReference.Contains(";"))
            {
                try
                {
                    sProd = fastPackageReference.Split(';')[0].Trim();
                    sVer = fastPackageReference.Split(';')[1].Trim();
                    sManu = fastPackageReference.Split(';')[2].Trim();
                }
                catch { }
            }

            request.Debug("Calling 'SWGet::DownloadPackage' '{0}', '{1}'", sProd, sVer);

            oUpdate.SoftwareUpdate.SW.ProductName = sProd;
            oUpdate.SoftwareUpdate.SW.ProductVersion = sVer;
            oUpdate.SoftwareUpdate.SW.Manufacturer = sManu;

            oUpdate.SoftwareUpdate.GetInstallType();
            if(oUpdate.SoftwareUpdate.Download(false).Result)
                request.Verbose(sManu + " " + sProd + " " + sVer + " downloaded.");
            else
                request.Verbose(sManu + " " + sProd + " " + sVer + " NOT downloaded.");

        }

        private void _getInstalledPackages(string name, string requiredVersion, string minimumVersion, string maximumVersion, Request request)
        {
            _reAuthenticate(request); //Check if AuthToken is still valid

            try
            {
                List<AddSoftware> lResult = getInstalledSW().ToList();

                List<GetSoftware> lServer = RZRestAPI.SWResults(name).OrderBy(t => t.Shortname).ToList();
                request.Debug("Items Found: " + lServer.Count().ToString());

                List<AddSoftware> lLocal = lResult.Where(t => lServer.Count(x => x.ProductName == t.ProductName & x.Manufacturer == t.Manufacturer & x.ProductVersion == t.ProductVersion) != 0).OrderBy(t => t.ProductName).ThenBy(t => t.ProductVersion).ThenBy(t => t.Manufacturer).ToList();


                if (!string.IsNullOrEmpty(name))
                {
                    string sProdName = "";
                    try
                    {
                        sProdName = lServer.FirstOrDefault(p => string.Equals(p.Shortname, name, StringComparison.OrdinalIgnoreCase)).ProductName;
                    }
                    catch { }
                    lLocal = lLocal.Where(p => String.Equals(p.ProductName, name, StringComparison.OrdinalIgnoreCase) | String.Equals(p.ProductName, sProdName, StringComparison.OrdinalIgnoreCase)).OrderBy(t => t.ProductName).ToList();
                }

                if (requiredVersion != null)
                {
                    lLocal = lLocal.Where(p => p.ProductVersion.ToLowerInvariant() == requiredVersion.ToLowerInvariant()).ToList();
                }
                if (minimumVersion != null)
                {
                    try
                    {
                        lLocal = lLocal.Where(p => Version.Parse(p.ProductVersion) >= Version.Parse(minimumVersion)).ToList();
                    }
                    catch
                    {
                        lLocal = lLocal.Where(p => p.ProductVersion == minimumVersion).ToList();
                    }
                }
                if (maximumVersion != null)
                {
                    try
                    {
                        lLocal = lLocal.Where(p => Version.Parse(p.ProductVersion) <= Version.Parse(maximumVersion)).ToList();
                    }
                    catch
                    {
                        lLocal = lLocal.Where(p => p.ProductVersion == maximumVersion).ToList();
                    }
                }

                foreach (var SW in lLocal)
                {
                    request.YieldSoftwareIdentity(SW.ProductName + ";" + SW.ProductVersion, SW.ProductName, SW.ProductVersion, "", "", RZRestAPI.sURL, name ?? "", "", SW.Shortname);
                    //request.YieldSoftwareIdentity(SW.Shortname, SW.ProductName, SW.ProductVersion, "", SW.Description, "Local", "", SW.ProductURL, SW.ContentID.ToString());
                }
            }
            catch
            {
                dLastTokenRefresh = new DateTime();
                
                
            }
        }

        private static string GetTimeToken()
        {
            byte[] time = BitConverter.GetBytes(DateTime.UtcNow.ToBinary());
            byte[] key = Guid.NewGuid().ToByteArray();
            return Convert.ToBase64String(time.Concat(key).ToArray());
        }

        public Collection<PSObject> RunPS(string PSScript)
        {
            PowerShell PowerShellInstance = PowerShell.Create();

            PowerShellInstance.AddScript(PSScript);

            Collection<PSObject> PSOutput = PowerShellInstance.Invoke();
            
            return PSOutput;

        }

        public string descramble(string sKey)
        {
            int[] code = new int[] { 8, 4, 4, 2, 2, 2, 2, 2, 2, 2, 2 };
            int ipos = 0;
            string sResult = "";
            for (int i = 0; i < code.Length; i++)
            {
                sResult = sResult + new string(sKey.Substring(ipos, code[i]).Reverse().ToArray());
                ipos = ipos + code[i];
            }

            return sResult;
        }

        public List<AddSoftware> getInstalledSW()
        {
            lSoftware = new List<AddSoftware>();
            
            if (oScan.bInitialScan)
            {
                oScan = new RZUpdate.RZScan(false, false);
                oScan.SWScan().Wait();
            }

            return oScan.InstalledSoftware;
        }

        #region PW 
        static byte[] entropy = System.Text.Encoding.Unicode.GetBytes("RZ" + Environment.UserName);

        public static string EncryptString(System.Security.SecureString input)
        {
            byte[] encryptedData = System.Security.Cryptography.ProtectedData.Protect(
                System.Text.Encoding.Unicode.GetBytes(ToInsecureString(input)),
                entropy,
                System.Security.Cryptography.DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encryptedData);
        }

        public static System.Security.SecureString DecryptString(string encryptedData)
        {
            try
            {
                byte[] decryptedData = System.Security.Cryptography.ProtectedData.Unprotect(
                    Convert.FromBase64String(encryptedData),
                    entropy,
                    System.Security.Cryptography.DataProtectionScope.CurrentUser);
                return ToSecureString(System.Text.Encoding.Unicode.GetString(decryptedData));
            }
            catch
            {
                return new SecureString();
            }
        }

        public static System.Security.SecureString ToSecureString(string input)
        {
            SecureString secure = new SecureString();
            foreach (char c in input)
            {
                secure.AppendChar(c);
            }
            secure.MakeReadOnly();
            return secure;
        }

        public static string ToInsecureString(SecureString input)
        {
            string returnValue = string.Empty;
            IntPtr ptr = System.Runtime.InteropServices.Marshal.SecureStringToBSTR(input);
            try
            {
                returnValue = System.Runtime.InteropServices.Marshal.PtrToStringBSTR(ptr);
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.ZeroFreeBSTR(ptr);
            }
            return returnValue;
        }

        #endregion
    }
}
