using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using Microsoft.Win32;
using System.Diagnostics;
using System.Drawing;
using RuckZuck.Base;

namespace RuckZuck.Base
{
    /// <summary>
    /// Class to detect Installed SW and Updates
    /// </summary>
    public partial class RZScan
    {
        public List<AddSoftware> InstalledSoftware = new List<AddSoftware>();
        public List<AddSoftware> NewSoftwareVersions = new List<AddSoftware>();
        public List<GetSoftware> SoftwareRepository = new List<GetSoftware>();
        public List<AddSoftware> StaticInstalledSoftware = new List<AddSoftware>();
        public System.Timers.Timer tRegCheck = new System.Timers.Timer();
        internal bool bCheckUpdates = false;
        internal bool bInitialScan = true;
        internal bool bRunScan = false;
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="CheckForUpdates">initial update check enabled</param>
        /// <param name="API">RuckZuck Web-Service API</param>
        public RZScan(bool CheckForUpdates)
        {
            new RZScan(false, CheckForUpdates);
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="RunScan">initial scan enabled</param>
        /// <param name="CheckForUpdates">initial update check enabled</param>
        /// <param name="API">RuckZuck Web-Service API</param>
        public RZScan(bool RunScan, bool CheckForUpdates)
        {
            InstalledSoftware = new List<AddSoftware>();
            SoftwareRepository = new List<GetSoftware>();
            NewSoftwareVersions = new List<AddSoftware>();
            StaticInstalledSoftware = new List<AddSoftware>();

            if (CheckForUpdates)
                bCheckUpdates = true;

            OnSWScanCompleted += RZScan_OnSWScanCompleted;
            OnUpdScanCompleted += RZScan_OnUpdScanCompleted;
            OnSWRepoLoaded += RZScan_OnSWRepoLoaded;

            if (RunScan)
            {
                bRunScan = true;
                //SWScan();
                GetSWRepository().ConfigureAwait(false); //Scan Runs when Repo is loaded
            }
            //Check every 60s
            tRegCheck.Interval = 60000;
            tRegCheck.Elapsed += TRegCheck_Elapsed;

        }

        delegate void AnonymousDelegate();

        public event EventHandler OnInstalledSWAdded = delegate { };
        public event EventHandler OnSWRepoLoaded = delegate { };

        public event EventHandler OnSWScanCompleted = delegate { };
        public event EventHandler OnUpdatesDetected = delegate { };
        public event EventHandler OnUpdScanCompleted = delegate { };
        public static Bitmap GetImageFromExe(string Filename, string empty = "")
        {
            try
            {
                Bitmap bResult = System.Drawing.Icon.ExtractAssociatedIcon(Filename).ToBitmap();

                //try
                //{
                //    TsudaKageyu.IconExtractor iE = new TsudaKageyu.IconExtractor(Filename);
                //    if (iE.FileName != null)
                //    {
                //        List<Icon> lIcons = TsudaKageyu.IconUtil.Split(iE.GetIcon(0)).ToList();
                //        //Max Size 128px...
                //        var ico = lIcons.Where(t => t.Height <= 128 && t.ToBitmap().PixelFormat == System.Drawing.Imaging.PixelFormat.Format32bppArgb).OrderByDescending(t => t.Height).FirstOrDefault();
                //        if (ico != null)
                //            return ico.ToBitmap();
                //        else
                //            return bResult;
                //    }
                //}
                //catch { }

                return bResult;
            }
            catch
            {
                return null;
            }
        }

        public async Task<bool> GetSWRepository()
        {
            //var tGetSWRepo =
            bool bResult = await Task.Run(() =>
            {
                try
                {
                    var oDB = RZRestAPIv2.GetCatalog().Distinct().OrderBy(t => t.ShortName).ThenByDescending(t => t.ProductVersion).ThenByDescending(t => t.ProductName).ToList();
                    lock (SoftwareRepository)
                    {
                        SoftwareRepository = oDB.Select(item => new GetSoftware()
                        {
                            Categories = item.Categories ?? new List<string>(),
                            Description = item.Description,
                            Downloads = item.Downloads,
                            SWId = item.SWId,
                            Manufacturer = item.Manufacturer,
                            ProductName = item.ProductName,
                            ProductURL = item.ProductURL,
                            ProductVersion = item.ProductVersion,
                            ShortName = item.ShortName,
                            IconHash = item.IconHash
                        }).ToList();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message.ToString());
                }

                OnSWRepoLoaded(this, new EventArgs());

                return true;
            });

            return bResult;
        }

        public async Task SWScanAsync()
        {
            var tSWScan = Task.Run(() =>
            {
                List<AddSoftware> TempInstalledSoftware = new List<AddSoftware>();
                try
                {
                    var CUX64 = RegScanAsync(RegistryHive.CurrentUser, RegistryView.Default, TempInstalledSoftware);
                    //var CUX86 = RegScan(RegistryHive.CurrentUser, RegistryView.Registry32, TempInstalledSoftware);
                    var LMX86 = RegScanAsync(RegistryHive.LocalMachine, RegistryView.Registry32, TempInstalledSoftware);
                    var LMX64 = RegScanAsync(RegistryHive.LocalMachine, RegistryView.Registry64, TempInstalledSoftware);

                    CUX64.Wait();
                    //CUX86.Wait();
                    LMX86.Wait();
                    LMX64.Wait();
                }
                catch { }

                TempInstalledSoftware.AddRange(StaticInstalledSoftware);

                if (InstalledSoftware.Count == 0)
                {
                    lock (InstalledSoftware)
                    {
                        InstalledSoftware.AddRange(TempInstalledSoftware);
                    }
                }
                else
                {
                    //Detect added SW
                    foreach (AddSoftware oSW in TempInstalledSoftware.Where(x => !InstalledSoftware.Any(y => y.ProductName == x.ProductName && y.ProductVersion == x.ProductVersion)))
                    {
                        OnInstalledSWAdded(oSW, new EventArgs());
                    }

                    bool bUpdateRemoved = false;
                    //Detect removed SW
                    foreach (AddSoftware oSW in InstalledSoftware.Where(x => !TempInstalledSoftware.Any(y => y.ProductName == x.ProductName && y.ProductVersion == x.ProductVersion)))
                    {
                        try
                        {
                            lock (NewSoftwareVersions)
                            {
                                int iCount = NewSoftwareVersions.RemoveAll(t => t.ProductName == oSW.ProductName);
                                iCount = iCount + NewSoftwareVersions.RemoveAll(t => t.MSIProductID == oSW.ProductVersion && t.Manufacturer == oSW.Manufacturer);
                                if (iCount > 0)
                                    bUpdateRemoved = true;
                            }
                        }
                        catch { }
                    }

                    lock (InstalledSoftware)
                    {
                        InstalledSoftware = TempInstalledSoftware;
                    }
                    if (bUpdateRemoved)
                        OnUpdScanCompleted(this, new EventArgs());
                }

                OnSWScanCompleted(this, new EventArgs());
            });

            await tSWScan;
        }

        internal void _CheckUpdates(List<AddSoftware> aSWCheck)
        {
            try
            {
                if (aSWCheck == null || aSWCheck.Count() == 0)
                    aSWCheck = InstalledSoftware.Select(t => new AddSoftware() { ProductName = t.ProductName, ProductVersion = t.ProductVersion, Manufacturer = t.Manufacturer }).ToList();

                var vSWCheck = aSWCheck.Select(t => new AddSoftware() { ProductName = t.ProductName, ProductVersion = t.ProductVersion, Manufacturer = t.Manufacturer }).ToList();

                //we do not have to check for updates if it's in the Catalog
                foreach (var oSW in SoftwareRepository)
                {
                    vSWCheck.RemoveAll(t => t.ProductName.ToLower().Trim() == oSW.ProductName.ToLower().Trim() && t.Manufacturer.ToLower().Trim() == oSW.Manufacturer.ToLower().Trim() && t.ProductVersion.ToLower().Trim() == oSW.ProductVersion.ToLower().Trim());
                }

                List<AddSoftware> lCheckResult = (RZRestAPIv2.CheckForUpdateAsync(vSWCheck).Result).ToList();

                var lResult = lCheckResult.Select(item => new AddSoftware()
                {
                    Architecture = item.Architecture,
                    Category = item.Category,
                    Description = item.Description,
                    Manufacturer = item.Manufacturer,
                    ProductName = item.ProductName,
                    ProductURL = item.ProductURL,
                    ProductVersion = item.ProductVersion,
                    MSIProductID = item.MSIProductID,
                    ShortName = item.ShortName,
                    SWId = item.SWId,
                    IconHash = item.IconHash
                }).ToList();

                //Only take updated Versions
                var lNew = lResult.Where(t => t.ShortName != "new").ToList();

                //Remove Update if new Version is already installed
                foreach (var oSW in InstalledSoftware)
                {
                    lNew.RemoveAll(t => t.ProductName.ToLower().Trim() == oSW.ProductName.ToLower().Trim() && t.Manufacturer.ToLower().Trim().TrimEnd('.') == oSW.Manufacturer.ToLower().Trim().TrimEnd('.') && t.ProductVersion.ToLower().Trim() == oSW.ProductVersion.ToLower().Trim());
                }

                lock (NewSoftwareVersions)
                {
                    //Store new Versions of existing SW
                    NewSoftwareVersions.AddRange(lNew);

                    //Remove duplicates
                    NewSoftwareVersions = NewSoftwareVersions.GroupBy(x => x.ShortName).Select(y => y.First()).ToList();
                }
                if (lNew.Count > 0)
                    OnUpdatesDetected(lNew, new EventArgs());
            }
            catch (Exception ex)
            {
                ex.ToString();
            }

            OnUpdScanCompleted(this, new EventArgs());
        }

        internal void _RegScan(RegistryHive RegHive, RegistryView RegView, List<AddSoftware> lScanList)
        {
            try
            {
                RegistryKey oUBase = RegistryKey.OpenBaseKey(RegHive, RegView);
                RegistryKey oUKey = oUBase.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall", false);
                List<string> USubKeys = new List<string>();
                USubKeys.AddRange(oUKey.GetSubKeyNames());
                foreach (string sProdID in USubKeys)
                {
                    try
                    {
                        RegistryKey oRegkey = oUKey.OpenSubKey(sProdID);
                        bool bSystemComponent = Convert.ToBoolean(oRegkey.GetValue("SystemComponent", 0));
                        bool bWindowsInstaller = Convert.ToBoolean(oRegkey.GetValue("WindowsInstaller", 0));
                        string sParent = oRegkey.GetValue("ParentKeyName", "").ToString();
                        string sRelease = oRegkey.GetValue("ReleaseType", "").ToString();

                        //Check if its a SystemComponent or WindowsInstaller
                        if (bSystemComponent)
                            continue;

                        //Check if NO PrentKeyName exists
                        if (!string.IsNullOrEmpty(sParent))
                            continue;

                        //Check if NO ReleaseType exists
                        if (!string.IsNullOrEmpty(sRelease))
                            continue;

                        AddSoftware oItem = GetSWPropertiesAsync(oUKey.OpenSubKey(sProdID)).Result;
                        if (!string.IsNullOrEmpty(oItem.ProductName))
                        {
                            try
                            {
                                lock (lScanList)
                                {
                                    lScanList.Add(oItem);
                                }

                            }
                            catch (Exception ex)
                            {
                                ex.Message.ToString();
                            }
                        }
                    }
                    catch { }

                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        /// <summary>
        /// Check for updated Version in the RuckZuck Repository
        /// </summary>
        /// <param name="aSWCheck">null = all Installed SW</param>
        internal async Task CheckUpdatesAsync(List<AddSoftware> aSWCheck)
        {
            await Task.Run(() => _CheckUpdates(aSWCheck));
        }

        internal string descramble(string sKey)
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

        internal async Task<AddSoftware> GetSWPropertiesAsync(RegistryKey oRegkey)
        {
            return await Task.Run(() => {
            AddSoftware oResult = new AddSoftware();
            Version oVer = null;
            bool bVersion = false;

            oResult.PSPreReq = "$true";

            if (oRegkey.View == RegistryView.Registry32)
                oResult.Architecture = "X86";
            else
            {
                oResult.Architecture = "X64";
                oResult.PSPreReq = "[Environment]::Is64BitProcess";
            }



            string sMSI = oRegkey.Name.Split('\\').Last();

            string EncKey = "";
            if (sMSI.StartsWith("{") && sMSI.EndsWith("}"))
            {
                bool bIsMSI = true;
                EncKey = descramble(sMSI.Substring(1, 36).Replace("-", ""));
                try
                {
                    RegistryKey oBase = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, oRegkey.View);
                    RegistryKey oKey = oBase.OpenSubKey(@"SOFTWARE\Classes\Installer\Products\" + EncKey, false);
                    if (oKey == null)
                        bIsMSI = false;
                }
                catch { bIsMSI = false; }

                if (bIsMSI)
                {
                    oResult.MSIProductID = sMSI;
                    oResult.PSUninstall = "$proc = (Start-Process -FilePath \"msiexec.exe\" -ArgumentList \"/x " + sMSI + " /qn REBOOT=REALLYSUPPRESS \" -Wait -PassThru);$proc.WaitForExit();$ExitCode = $proc.ExitCode";


                    oResult.PSDetection = @"Test-Path 'HKLM:\SOFTWARE\Classes\Installer\Products\" + EncKey + "'";

                    try
                    {
                        RegistryKey oSource = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Classes\Installer\Products\" + EncKey + "\\SourceList");
                        if (oSource != null)
                        {
                            oResult.PSInstall = "$proc = (Start-Process -FilePath \"msiexec.exe\" -ArgumentList \"/i `\"" + oSource.GetValue("PackageName") + "`\" /qn ALLUSERS=2 REBOOT=REALLYSUPPRESS\" -Wait -PassThru);$proc.WaitForExit();$ExitCode = $proc.ExitCode";
                        }
                    }
                    catch
                    {
                        try
                        {
                            string sVer = oRegkey.GetValue("DisplayVersion", "").ToString();
                            if (Version.TryParse(sVer, out oVer))
                                oResult.PSDetection = @"if(([version](Get-ItemPropertyValue -path '" + oRegkey.Name.Replace("HKEY_LOCAL_MACHINE", "HKLM:") + "' -Name DisplayVersion -ea SilentlyContinue)) -ge '" + sVer + "') { $true } else { $false }";
                            else
                                oResult.PSDetection = @"if((Get-ItemPropertyValue -path '" + oRegkey.Name.Replace("HKEY_LOCAL_MACHINE", "HKLM:") + "' -Name DisplayVersion -ea SilentlyContinue) -eq '" + sVer + "') { $true } else { $false }";
                        }
                        catch { }

                        oResult.PSInstall = "$proc = (Start-Process -FilePath \"msiexec.exe\" -ArgumentList \"/i `\"<PackageName.msi>`\" /qn ALLUSERS=2 REBOOT=REALLYSUPPRESS\" -Wait -PassThru);$proc.WaitForExit();$ExitCode = $proc.ExitCode";
                    }
                }
                else
                {
                    oResult.PSInstall = "$proc = (Start-Process -FilePath \"setup.exe\" -ArgumentList \"/?\" -Wait -PassThru);$proc.WaitForExit();$ExitCode = $proc.ExitCode";

                    string sVer = oRegkey.GetValue("DisplayVersion", "").ToString();
                    if (Version.TryParse(sVer, out oVer)) //check if its a Version
                        bVersion = true;

                    if (Environment.Is64BitOperatingSystem && oRegkey.View == RegistryView.Registry32)
                    {
                        if (bVersion)
                            oResult.PSDetection = @"if(([version](Get-ItemPropertyValue -path '" + oRegkey.Name.ToUpper().Replace("HKEY_LOCAL_MACHINE", "HKLM:").Replace("SOFTWARE\\", "SOFTWARE\\WOW6432NODE\\") + "' -Name DisplayVersion -ea SilentlyContinue)) -ge '" + sVer + "') { $true } else { $false }";
                        else
                            oResult.PSDetection = @"if((Get-ItemPropertyValue -path '" + oRegkey.Name.ToUpper().Replace("HKEY_LOCAL_MACHINE", "HKLM:").Replace("SOFTWARE\\", "SOFTWARE\\WOW6432NODE\\") + "' -Name DisplayVersion -ea SilentlyContinue) -eq '" + sVer + "') { $true } else { $false }";
                    }
                    else
                    {
                        if (bVersion)
                            oResult.PSDetection = @"if(([version](Get-ItemPropertyValue -path '" + oRegkey.Name.ToUpper().Replace("HKEY_LOCAL_MACHINE", "HKLM:") + "' -Name DisplayVersion -ea SilentlyContinue)) -ge '" + sVer + "') { $true } else { $false }";
                        else
                            oResult.PSDetection = @"if((Get-ItemPropertyValue -path '" + oRegkey.Name.ToUpper().Replace("HKEY_LOCAL_MACHINE", "HKLM:") + "' -Name DisplayVersion -ea SilentlyContinue) -eq '" + sVer + "') { $true } else { $false }";

                    }
                }
            }
            else
            {
                string sVer = oRegkey.GetValue("DisplayVersion", "").ToString();
                if (Version.TryParse(sVer, out oVer)) //check if its a Version
                    bVersion = true;

                oResult.PSInstall = "$proc = (Start-Process -FilePath \"setup.exe\" -ArgumentList \"/?\" -Wait -PassThru);$proc.WaitForExit();$ExitCode = $proc.ExitCode";
                if (Environment.Is64BitOperatingSystem && oRegkey.View == RegistryView.Registry32)
                {
                    if (bVersion)
                        oResult.PSDetection = @"if(([version](Get-ItemPropertyValue -path '" + oRegkey.Name.ToUpper().Replace("HKEY_LOCAL_MACHINE", "HKLM:").Replace("SOFTWARE\\", "SOFTWARE\\WOW6432NODE\\") + "' -Name DisplayVersion -ea SilentlyContinue)) -ge '" + sVer + "') { $true } else { $false }";
                    else
                        oResult.PSDetection = @"if((Get-ItemPropertyValue -path '" + oRegkey.Name.ToUpper().Replace("HKEY_LOCAL_MACHINE", "HKLM:").Replace("SOFTWARE\\", "SOFTWARE\\WOW6432NODE\\") + "' -Name DisplayVersion -ea SilentlyContinue) -eq '" + sVer + "') { $true } else { $false }";
                }
                else
                {
                    if (bVersion)
                        oResult.PSDetection = @"if(([version](Get-ItemPropertyValue -path '" + oRegkey.Name.ToUpper().Replace("HKEY_LOCAL_MACHINE", "HKLM:") + "' -Name DisplayVersion -ea SilentlyContinue)) -ge '" + sVer + "') { $true } else { $false }";
                    else
                        oResult.PSDetection = @"if((Get-ItemPropertyValue -path '" + oRegkey.Name.ToUpper().Replace("HKEY_LOCAL_MACHINE", "HKLM:") + "' -Name DisplayVersion -ea SilentlyContinue) -eq '" + sVer + "') { $true } else { $false }";
                }

            }

            oResult.PSDetection = oResult.PSDetection.Replace("HKEY_LOCAL_MACHINE", "HKLM:");
            oResult.PSDetection = oResult.PSDetection.Replace("HKEY_CURRENT_USER", "HKCU:");

            oResult.ProductName = oRegkey.GetValue("DisplayName", "").ToString();
            oResult.ProductVersion = oRegkey.GetValue("DisplayVersion", "").ToString();
            oResult.Manufacturer = oRegkey.GetValue("Publisher", "").ToString().TrimEnd('.');

            //If not an MSI try to get Uninstall command from Registry
            if (string.IsNullOrEmpty(oResult.MSIProductID))
            {
                try
                {
                    string sUninst = oRegkey.GetValue("QuietUninstallString", "").ToString().Replace("\"", "");
                    if (!string.IsNullOrEmpty(sUninst))
                    {
                        try
                        {
                            if (sUninst.IndexOf('/') >= 0)
                                oResult.PSUninstall = "$proc = (Start-Process -FilePath \"" + sUninst.Split('/')[0] + "\" -ArgumentList \"" + sUninst.Substring(sUninst.IndexOf('/')) + "\" -Wait -PassThru);$proc.WaitForExit();$ExitCode = $proc.ExitCode";
                            else
                                oResult.PSUninstall = "$proc = (Start-Process -FilePath \"" + sUninst + "\" -ArgumentList \"/?\" -Wait -PassThru);$proc.WaitForExit();$ExitCode = $proc.ExitCode";
                            //$proc = (Start-Process -FilePath "zps17_en.exe" -ArgumentList "/Silent" -PassThru);$proc.WaitForExit();$ExitCode = $proc.ExitCode
                        }
                        catch
                        {
                            oResult.PSUninstall = "$proc = (Start-Process -FilePath \"" + sUninst + "\" -ArgumentList \"/?\" -Wait -PassThru);$proc.WaitForExit();$ExitCode = $proc.ExitCode";
                        }
                    }
                }
                catch { }
            }
            //If no silent uninstall is provided, use the normal uninstall string
            if (string.IsNullOrEmpty(oResult.PSUninstall))
            {
                if (string.IsNullOrEmpty(oResult.MSIProductID))
                {
                    string sUninst = oRegkey.GetValue("UninstallString", "").ToString().Replace("\"", "");
                    oResult.PSUninstall = "$proc  = (Start-Process -FilePath \"" + sUninst + "\" -ArgumentList \"/?\" -Wait -PassThru);$proc.WaitForExit();$ExitCode = $proc.ExitCode";
                }
            }

            //get Version String
            if (string.IsNullOrEmpty(oResult.ProductVersion))
            {
                string sVersion = oRegkey.GetValue("Version", "").ToString();
                if (!string.IsNullOrEmpty(sVersion))
                {
                    try
                    {
                        Int32 iVersion = Convert.ToInt32(sVersion);
                        string sfullval = iVersion.ToString("X8");
                        sVersion = Convert.ToInt32(sfullval.Substring(0, 2), 16).ToString();
                        sVersion = sVersion + "." + Convert.ToInt32(sfullval.Substring(2, 2), 16).ToString();
                        sVersion = sVersion + "." + Convert.ToInt32(sfullval.Substring(4, 4), 16).ToString();

                        oResult.ProductVersion = sVersion;
                    }
                    catch
                    {
                    }

                }
            }

            //Get Image
            string sDisplayIcon = oRegkey.GetValue("DisplayIcon", "").ToString().Split(',')[0];
            if (!string.IsNullOrEmpty(EncKey))
            {
                try
                {
                    RegistryKey oKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Classes\Installer\Products\" + EncKey, false);
                    if (oKey != null)
                    {
                        string sIcon = oKey.GetValue("ProductIcon", "") as string;

                        if (!string.IsNullOrEmpty(sIcon))
                            sDisplayIcon = sIcon;
                    }
                }
                catch { }
            }

            //Add default Icon
            if (string.IsNullOrEmpty(sDisplayIcon))
            {
                sDisplayIcon = @"C:\windows\system32\msiexec.exe";
            }

            if (!sDisplayIcon.Contains(":\\"))
                sDisplayIcon = @"C:\windows\system32\" + sDisplayIcon;

            sDisplayIcon = sDisplayIcon.Replace("\"", "");
            sDisplayIcon = sDisplayIcon.Split(',')[0];

            if (!File.Exists(sDisplayIcon))
            {
                if (File.Exists(sDisplayIcon.Replace(" (x86)", "")))
                    sDisplayIcon = sDisplayIcon.Replace(" (x86)", "");
                else
                    sDisplayIcon = "";
            }

            if (!string.IsNullOrEmpty(sDisplayIcon))
            {
                oResult.Image = imageToByteArray(GetImageFromExe(sDisplayIcon.Replace("\"", "")));
            }

            oResult.Architecture = "X64";

            return oResult;
            });
        }

        internal byte[] imageToByteArray(System.Drawing.Image imageIn)
        {
            try
            {
                MemoryStream ms = new MemoryStream();
                imageIn.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                return ms.ToArray();
            }
            catch { }
            return null;
        }

        internal async Task RegScanAsync(RegistryHive RegHive, RegistryView RegView, List<AddSoftware> lScanList)
        {
            await Task.Run(() => _RegScan(RegHive, RegView, lScanList));
        }

        private async void RZScan_OnSWRepoLoaded(object sender, EventArgs e)
        {
            if (bRunScan)
            {
                await SWScanAsync();
            }
        }

        private async void RZScan_OnSWScanCompleted(object sender, EventArgs e)
        {
            if (bCheckUpdates)
            {
                await CheckUpdatesAsync(null);
            }
        }

        private void RZScan_OnUpdScanCompleted(object sender, EventArgs e)
        {
            if (bInitialScan)
            {
                bInitialScan = false;
                bCheckUpdates = false;
            }
        }

        private async void TRegCheck_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            await SWScanAsync();
        }
    }
}
