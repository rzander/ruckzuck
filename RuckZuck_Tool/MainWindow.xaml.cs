using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

using System.IO;
using System.Reflection;
using System.Globalization;
using System.Diagnostics;

using System.Management.Automation.Runspaces;
using System.Management.Automation;
using System.Collections.ObjectModel;
using System.Threading;
using System.Net;
using RuckZuck_WCF;
using RZUpdate;
using System.Security.Cryptography;
using System.Text;

namespace RuckZuck_Tool
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        string sAuthToken;
        delegate void AnonymousDelegate();
        //List<GetSoftware2> oDBx = new List<GetSoftware2>();
        //List<GetSoftware> oDB = new List<GetSoftware>();
        List<string> CommandArgs = new List<string>();
        internal RZScan oSCAN;
        public System.Timers.Timer tReAuth = new System.Timers.Timer();

        public MainWindow()
        {
            DateTime dstart = DateTime.Now;
            InitializeComponent();

            CommandArgs.AddRange(Environment.GetCommandLineArgs());
            CommandArgs.RemoveAt(0);

            //Disable SSL/TLS Errors
            System.Net.ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
            //Disable CRL Check
            System.Net.ServicePointManager.CheckCertificateRevocationList = false;
            //Get Proxy from IE
            WebRequest.DefaultWebProxy = WebRequest.GetSystemWebProxy();

            if (Properties.Settings.Default.UpgradeSettings)
            {
                Properties.Settings.Default.Upgrade();
                Properties.Settings.Default.UpgradeSettings = false;
                Properties.Settings.Default.Save();
            }

            tbSVC.Text = Properties.Settings.Default.WebService;

            //Get Version
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(System.Reflection.Assembly.GetExecutingAssembly().Location);
            tbVersion.Text = string.Format(tbVersion.Text, fvi.FileVersion);
            lVersion.Content = "Version: " + fvi.FileVersion;

            //Hide Tabs
            Style s = new Style();
            s.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Collapsed));
            tabWizard.ItemContainerStyle = s;

            if (string.IsNullOrEmpty(Properties.Settings.Default.UserKey))
            {
                Properties.Settings.Default.UserKey = Guid.NewGuid().ToString();
                Properties.Settings.Default.Save();
            }

            //RZRestAPI.sURL = Properties.Settings.Default.WebService;
            RZRestAPI.DisableBroadcast = Properties.Settings.Default.DisableBroadcast;
            tbSVC.Text = RZRestAPI.sURL;
            tbIPFSGW.Text = Properties.Settings.Default.IPFSGW;
            cbRZCache.IsChecked = !Properties.Settings.Default.DisableBroadcast;

            //Authenticate;
            Authenticate();
            //ReAuthenticate every 20min
            tReAuth.Interval = 1200000;
            tReAuth.Elapsed += TReAuth_Elapsed;
            tReAuth.Enabled = true;
            tReAuth.AutoReset = true;

            tReAuth.Start();

            //Set SOAP Header
            //oAPI.SecuredWebServiceHeaderValue = new RZApi.SecuredWebServiceHeader() { AuthenticatedToken = sAuthToken };

            oInstPanel.sAuthToken = sAuthToken;
            oInstPanel.onEdit += oInstPanel_onEdit;
            oUpdPanel.onEdit += oInstPanel_onEdit;
            //oInstPanel.OnSWUpdated += OUpdPanel_OnSWUpdated;
            oUpdPanel.OnSWUpdated += OUpdPanel_OnSWUpdated;

            double dSeconds = (DateTime.Now - dstart).TotalSeconds;
            dSeconds.ToString();


            //Run PowerShell check in separate thread...
            Thread thread = new Thread(() =>
            {
                try
                {
                    Runspace runspace = RunspaceFactory.CreateRunspace();
                    runspace.Open();

                    PowerShell powershell = PowerShell.Create();
                    powershell.AddScript("(get-Host).Version");
                    powershell.Runspace = runspace;
                    Collection<PSObject> results = powershell.Invoke();
                    if (((System.Version)(results[0].BaseObject)).Major < 4)
                    {
                        if (MessageBox.Show("The current Version of PowerShell is not supported. Do you want to update ?", "Update Powershell", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.Yes) == MessageBoxResult.Yes)
                        {
                            //Update...
                            Process.Start("https://www.microsoft.com/en-us/download/details.aspx?id=50395");
                            this.Close();
                        }
                    }
                }
                catch { }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

            FileVersionInfo FI = FileVersionInfo.GetVersionInfo(Assembly.GetEntryAssembly().Location);

            oSCAN = new RZScan(false, true);
            
            oSCAN.StaticInstalledSoftware.Add(new AddSoftware() { ProductName = "RuckZuck", Manufacturer = FI.CompanyName, ProductVersion = FI.ProductVersion.ToString() });
            oSCAN.OnSWScanCompleted += OSCAN_OnSWScanCompleted;
            oSCAN.OnUpdatesDetected += OSCAN_OnUpdatesDetected;
            oSCAN.OnSWRepoLoaded += OSCAN_OnSWRepoLoaded;
            oSCAN.OnUpdScanCompleted += OSCAN_OnUpdScanCompleted;
            oSCAN.OnInstalledSWAdded += OSCAN_OnInstalledSWAdded;
            oSCAN.bCheckUpdates = true;

            oSCAN.GetSWRepository().ConfigureAwait(false);

            //oSCAN.tRegCheck.Start();

            if (CommandArgs.Count > 0)
            {
                oInstPanel.EnableFeedback = false;
                oInstPanel.EnableEdit = false;
                oInstPanel.EnableSupport = false;
            }
            else
            {
                //Skip Startpage for registerred users...
                if (tbURL.IsEnabled)
                {
                    tabWizard.SelectedItem = tabMain;
                    //oSCAN.SWScan();

                    oInstPanel.EnableFeedback = true;
                    oInstPanel.EnableEdit = true;
                    oInstPanel.EnableSupport = true;
                }
                else
                {
                    oInstPanel.EnableSupport = false;
                    //oSCAN.SWScan();
                }
            }
        }

        private void TReAuth_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            Authenticate();
        }

        /// <summary>
        /// Authenticate User and refresh token
        /// </summary>
        public void Authenticate()
        {
            try
            {
                //Authenticate with custom User and Password from config file...
                if (!string.IsNullOrEmpty(Properties.Settings.Default.UserPW))
                {
                    string sResponse = RZRestAPI.GetAuthToken(Properties.Settings.Default.UserKey, Decrypt(Properties.Settings.Default.UserPW, Environment.UserName));
                    try
                    {
                        Guid.Parse(sResponse);
                        sAuthToken = sResponse;
                        tbURL.IsEnabled = true;
                        tbIPFSGW.IsEnabled = true;
                    }
                    catch { }
                }

                //Authenticate with dummy account
                if (string.IsNullOrEmpty(sAuthToken))
                    sAuthToken = RZRestAPI.GetAuthToken("FreeRZ", GetTimeToken());
            }
            catch { }
        }

        /// <summary>
        /// Encrypt a string
        /// </summary>
        /// <param name="strPlainText"></param>
        /// <param name="strKey"></param>
        /// <returns></returns>
        public static string Encrypt(string strPlainText, string strKey)
        {
            try
            {
                TripleDESCryptoServiceProvider objDES = new TripleDESCryptoServiceProvider();

                SHA1CryptoServiceProvider objSHA1 = new SHA1CryptoServiceProvider();
                byte[] bHash = objSHA1.ComputeHash(ASCIIEncoding.ASCII.GetBytes(strKey));

                byte[] bRes = ProtectedData.Protect(ASCIIEncoding.ASCII.GetBytes(strPlainText), bHash, DataProtectionScope.CurrentUser);

                return Convert.ToBase64String(bRes);
            }
            catch (System.Exception ex)
            {
                ex.Message.ToString();
            }
            return "";
        }

        /// <summary>
        /// Decrypt a string
        /// </summary>
        /// <param name="strBase64Text"></param>
        /// <param name="strKey"></param>
        /// <returns></returns>
        public static string Decrypt(string strBase64Text, string strKey)
        {
            try
            {
                TripleDESCryptoServiceProvider objDES = new TripleDESCryptoServiceProvider();

                SHA1CryptoServiceProvider objSHA1 = new SHA1CryptoServiceProvider();
                byte[] bHash = objSHA1.ComputeHash(ASCIIEncoding.ASCII.GetBytes(strKey));

                byte[] arrBuffer = Convert.FromBase64String(strBase64Text);
                return ASCIIEncoding.ASCII.GetString(System.Security.Cryptography.ProtectedData.Unprotect(arrBuffer, bHash, DataProtectionScope.CurrentUser));
            }
            catch (System.Exception ex)
            {
                ex.Message.ToString();
            }
            return "";

        }

        private void OUpdPanel_OnSWUpdated(object sender, EventArgs e)
        {
            oSCAN.tRegCheck.AutoReset = false;
            oSCAN.tRegCheck.Enabled = true;
            //Wait 1s;
            oSCAN.tRegCheck.Interval = 1000;
            oSCAN.tRegCheck.Start();
        }

        private void OSCAN_OnUpdScanCompleted(object sender, EventArgs e)
        {
            //Remove duplicates...
            lNewVersion = ((RZScan)sender).NewSoftwareVersions.GroupBy(x => x.Shortname).Select(y => y.First()).ToList();

            //var distinctItems = items.GroupBy(x => x.Id).Select(y => y.First());

            foreach (string sExclude in Properties.Settings.Default.UpdExlusion)
            {
                try
                {
                    lNewVersion.RemoveAll(t => t.Shortname == sExclude);
                }
                catch { }
            }

            AnonymousDelegate update = delegate ()
            {
                Mouse.OverrideCursor = null;
                lbWait.Visibility = Visibility.Hidden;
                btNextScan.IsEnabled = true;
                btBackScan.IsEnabled = true;

                if (lNewVersion.Count > 0)
                {
                    btUpdateSoftware.IsEnabled = true;
                    if (lNewVersion.Count == 1)
                        btUpdateSoftware.Content = "there is currently (" + lNewVersion.Count.ToString() + ") update available...";
                    else
                        btUpdateSoftware.Content = "there are currently (" + lNewVersion.Count.ToString() + ") updates available...";
                }
                else
                {
                    btUpdateSoftware.IsEnabled = false;
                    btUpdateSoftware.Content = "there are currently no updates available...";
                }

                //tabWizard.SelectedItem = tabMain;

            };
            Dispatcher.Invoke(update);
        }

        private void OSCAN_OnSWRepoLoaded(object sender, EventArgs e)
        {
            try
            {
                //oDB = ((RZScan)sender).SoftwareRepository;
                AnonymousDelegate update = delegate ()
                {
                    btInstallSoftware.Content = "Install new Software";
                    btInstallSoftware.IsEnabled = true;
                };
                Dispatcher.Invoke(update);

                oSCAN.bCheckUpdates = true;
                oSCAN.SWScan();
            }
            catch { }
        }
        private void OSCAN_OnUpdatesDetected(object sender, EventArgs e)
        {

        }

        private void OSCAN_OnSWScanCompleted(object sender, EventArgs e)
        {
            lSoftware = ((RZScan)sender).InstalledSoftware;
            AnonymousDelegate update = delegate ()
            {
                Mouse.OverrideCursor = null;
                lbWait.Visibility = Visibility.Hidden;
                btNextScan.IsEnabled = true;
                btBackScan.IsEnabled = true;

                if (lNewVersion.Count > 0)
                {
                    btUpdateSoftware.IsEnabled = true;
                    if(lNewVersion.Count == 1)
                        btUpdateSoftware.Content = "there is currently (" + lNewVersion.Count.ToString() + ") update available...";
                    else
                        btUpdateSoftware.Content = "there are currently (" + lNewVersion.Count.ToString() + ") updates available...";
                }
                else
                {
                    btUpdateSoftware.IsEnabled = false;
                    if (((RZScan)sender).bCheckUpdates)
                    {
                        btUpdateSoftware.Content = "Scanning for updates... please wait !";
                    }
                    else
                    {
                        btUpdateSoftware.Content = "there are currently no updates available...";
                    }
                }

                //tabWizard.SelectedItem = tabMain;

            };
            Dispatcher.Invoke(update);
        }

        void oInstPanel_onEdit(object sender, EventArgs e)
        {
            AnonymousDelegate update = delegate ()
            {
                try
                {
                    bool bNoPreReqCheck = false;
                    if (sender.GetType() == typeof(GetSoftware))
                    {
                        GetSoftware oSelectedItem = (GetSoftware)sender;

                        //Ignore PreRequisites if SHIFT is pressed
                        if (Keyboard.Modifiers == ModifierKeys.Shift)
                        {
                            bNoPreReqCheck = true;
                        }

                        //Load Software details for a valid DeploymentType...
                        SWUpdate oSW = new SWUpdate(oSelectedItem.ProductName, oSelectedItem.ProductVersion, oSelectedItem.Manufacturer, bNoPreReqCheck);

                        oNewPanel.OpenXML(oSW.SW);


                        tabWizard.SelectedItem = tabNewSWSMI;
                    }

                    if (sender.GetType() == typeof(AddSoftware))
                    {
                        AddSoftware oSelectedItem = (AddSoftware)sender;

                        //Ignore PreRequisites if SHIFT is pressed
                        if (Keyboard.Modifiers == ModifierKeys.Shift)
                        {
                            bNoPreReqCheck = true;
                        }

                        //Load Software details for a valid DeploymentType...
                        SWUpdate oSW = new SWUpdate(oSelectedItem.ProductName, oSelectedItem.ProductVersion, oSelectedItem.Manufacturer, bNoPreReqCheck);

                        oNewPanel.OpenXML(oSW.SW);


                        tabWizard.SelectedItem = tabNewSWSMI;
                    }
                }
                catch { }
            };
            Dispatcher.Invoke(update);
        }


        public List<AddSoftware> lSoftware = new List<AddSoftware>();
        public List<AddSoftware> lNewVersion = new List<AddSoftware>();
        public List<AddSoftware> lUnknownSoftware = new List<AddSoftware>();

        private void OSCAN_OnInstalledSWAdded(object sender, EventArgs e)
        {
            oSCAN.CheckUpdates(new List<AddSoftware>() { ((AddSoftware)sender) });
        }

        private void btNewSoftware_Click(object sender, RoutedEventArgs e)
        {
            tabWizard.SelectedItem = tabNewSWSMI;
            oNewPanel.btOpenMSI.RaiseEvent(e);
        }

        private void btInstallSoftware_Click(object sender, RoutedEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                if (oSCAN.SoftwareRepository.Count == 0)
                {
                    try
                    {
                        oSCAN.GetSWRepository().Wait(2000);
                    }
                    catch { }
                }

                List<GetSoftware> oDBCat = new List<GetSoftware>();
                PropertyGroupDescription PGD = new PropertyGroupDescription("", new ShortnameToCategory());

                foreach (GetSoftware oSW in oSCAN.SoftwareRepository)
                {
                    try
                    {
                        if (oSW.Categories.Count > 1)
                        {
                            foreach (string sCAT in oSW.Categories)
                            {
                                try
                                {

                                    //Check if SW is already installed
                                    if (lSoftware.FirstOrDefault(t => t.ProductName == oSW.ProductName && t.ProductVersion == oSW.ProductVersion) != null)
                                    {
                                        GetSoftware oNew = new GetSoftware() { Categories = new List<string> { sCAT }, Description = oSW.Description, Downloads = oSW.Downloads, SWId = oSW.SWId, Manufacturer = oSW.Manufacturer, ProductName = oSW.ProductName, ProductURL = oSW.ProductURL, ProductVersion = oSW.ProductVersion, Quality = oSW.Quality, Shortname = oSW.Shortname, IconHash = oSW.IconHash, isInstalled = true };
                                        oDBCat.Add(oNew);
                                    }
                                    else
                                    {
                                        GetSoftware oNew = new GetSoftware() { Categories = new List<string> { sCAT }, Description = oSW.Description, Downloads = oSW.Downloads, SWId = oSW.SWId, Manufacturer = oSW.Manufacturer, ProductName = oSW.ProductName, ProductURL = oSW.ProductURL, ProductVersion = oSW.ProductVersion, Quality = oSW.Quality, Shortname = oSW.Shortname, IconHash = oSW.IconHash, isInstalled = false };
                                        oDBCat.Add(oNew);
                                    }
                                }
                                catch { }
                            }
                        }
                        else
                        {
                            //Check if SW is already installed
                            if (lSoftware.FirstOrDefault(t => t.ProductName == oSW.ProductName && t.ProductVersion == oSW.ProductVersion) != null)
                            {
                                oDBCat.Add(new GetSoftware() { Categories = oSW.Categories, Description = oSW.Description, Downloads = oSW.Downloads, SWId = oSW.SWId, Manufacturer = oSW.Manufacturer, ProductName = oSW.ProductName, ProductURL = oSW.ProductURL, ProductVersion = oSW.ProductVersion, Quality = oSW.Quality, Shortname = oSW.Shortname, IconHash = oSW.IconHash, isInstalled = true });
                            }
                            else
                            {
                                oDBCat.Add(new GetSoftware() { Categories = oSW.Categories, Description = oSW.Description, Downloads = oSW.Downloads, SWId = oSW.SWId, Manufacturer = oSW.Manufacturer, ProductName = oSW.ProductName, ProductURL = oSW.ProductURL, ProductVersion = oSW.ProductVersion, Quality = oSW.Quality, Shortname = oSW.Shortname, IconHash = oSW.IconHash, isInstalled = false });
                            }
                        }
                    }
                    catch { }
                }

                ListCollectionView lcv = new ListCollectionView(oDBCat.ToList());

                foreach (var o in RZRestAPI.GetCategories(oSCAN.SoftwareRepository))
                {
                        PGD.GroupNames.Add(o);
                }

                lcv.GroupDescriptions.Add(PGD);
                
                oInstPanel.lvSW.ItemsSource = lcv;
                oInstPanel.lSoftware = lSoftware;
                //var target = oSCAN.SoftwareRepository.Select(x => new GetSoftware() { Categories = x.Categories, Description = x.Description, Downloads = x.Downloads, IconId = x.IconId, Image = x.Image, Manufacturer = x.Manufacturer, ProductName = x.ProductName, ProductURL = x.ProductURL, ProductVersion = x.ProductVersion, Quality = x.Quality, Shortname = x.Shortname, isInstalled = false }).ToList();
                oInstPanel.lAllSoftware = oSCAN.SoftwareRepository; 

                //Mark all installed...
                oInstPanel.lAllSoftware.ForEach(x => { if (lSoftware.FirstOrDefault(t => (t.ProductName == x.ProductName && t.ProductVersion == x.ProductVersion)) != null) { x.isInstalled = true; } });


                /*if (!string.IsNullOrEmpty(tbURL.Text))
                    oInstPanel.sInternalURL = tbURL.Text;*/
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }

            tabWizard.SelectedItem = tabInstallSW;
        }

        public class ShortnameToCategory : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                try
                {
                    
                    if (value.GetType() == typeof(GetSoftware))
                    {
                        GetSoftware oSW = (GetSoftware)value;
                        return oSW.Categories[0];
                    }
                }
                catch(Exception ex)
                {
                    ex.Message.ToString();
                }

                return null;

            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                throw new NotImplementedException();
            }
        }

        private void btNewSoftwareARP_Click(object sender, RoutedEventArgs e)
        {
            tabWizard.SelectedItem = tabNewSWARP;
        }

        private void btCreatARPSW_Click(object sender, RoutedEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                if (arpGrid2.SelectedItems.Count > 0)
                {
                    AddSoftware oSelectedItem = arpGrid2.SelectedItem as AddSoftware;
                    //oNewPanel = new NewSWPanelxaml();
                    oNewPanel.tbManufacturer.Text = oSelectedItem.Manufacturer;
                    oNewPanel.tbProductName.Text = oSelectedItem.ProductName;
                    oNewPanel.tbVersion.Text = oSelectedItem.ProductVersion;
                    oNewPanel.imgIcon.Tag = oSelectedItem.Image;
                    oNewPanel.tbProductURL.Text = oSelectedItem.ProductURL;
                    
                    oNewPanel.tbDescription.Text = oSelectedItem.Description;
                    oNewPanel.tbArchitecture.Text = oSelectedItem.Architecture;
                    oNewPanel.tbContentId.Text = oSelectedItem.ContentID;
                    oNewPanel.tbPSDetection.Text = oSelectedItem.PSDetection;
                    oNewPanel.tbPSInstall.Text = oSelectedItem.PSInstall;
                    oNewPanel.tbPSPrereq.Text = oSelectedItem.PSPreReq;
                    oNewPanel.tbPSUnInstall.Text = oSelectedItem.PSUninstall;
                    oNewPanel.tbPSPreInstall.Text = oSelectedItem.PSPreInstall;
                    oNewPanel.tbPSPostInstall.Text = oSelectedItem.PSPostInstall;
                    oNewPanel.tbMSIId.Text = oSelectedItem.MSIProductID;
                    oNewPanel.imgIcon.Source = ByteToImage(oSelectedItem.Image);

                    oNewPanel.dgSourceFiles.DataContext = null;

                    if (string.IsNullOrEmpty(oSelectedItem.ContentID))
                        oNewPanel.tbContentId.Text = Guid.NewGuid().ToString();

                    if (oSelectedItem.Architecture == "NEW")
                    {
                        if (Environment.Is64BitOperatingSystem)
                            oNewPanel.tbArchitecture.Text = "X64";
                        else
                            oNewPanel.tbArchitecture.Text = "X86";
                    }

                    if (oNewPanel.tbPSUnInstall.Text.ToLowerInvariant().Contains("(x86)") || oNewPanel.tbPSDetection.Text.ToLowerInvariant().Contains("wow6432node"))
                        oNewPanel.tbPSPrereq.Text = "[Environment]::Is64BitProcess";

                    //oNewPanel.tbPSUnInstall.Text = oNewPanel.tbPSUnInstall.Text.Replace(@"C:\Program Files (x86)", "$(${Env:ProgramFiles(x86)})");
                    //oNewPanel.tbPSUnInstall.Text = oNewPanel.tbPSUnInstall.Text.Replace(@"C:\Program Piles", "$($Env:ProgramFiles)");

                    oNewPanel.tbPSUnInstall.Text = System.Text.RegularExpressions.Regex.Replace(oNewPanel.tbPSUnInstall.Text, System.Text.RegularExpressions.Regex.Escape(@"C:\Program Files (x86)"), @"$(${Env:ProgramFiles(x86)})", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.IgnorePatternWhitespace);
                    oNewPanel.tbPSUnInstall.Text = System.Text.RegularExpressions.Regex.Replace(oNewPanel.tbPSUnInstall.Text, System.Text.RegularExpressions.Regex.Escape(@"C:\Program Files"), "$($Env:ProgramFiles)", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.IgnorePatternWhitespace);
                    oNewPanel.tbPSUnInstall.Text = System.Text.RegularExpressions.Regex.Replace(oNewPanel.tbPSUnInstall.Text, System.Text.RegularExpressions.Regex.Escape(@"C:\Program Data"), "$($Env:ProgramData)", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.IgnorePatternWhitespace);

                    if(oNewPanel.tbPSDetection.Text.ToLowerInvariant().Contains("wow6432node"))
                        oNewPanel.tbArchitecture.Text = "X64";

                    if(oNewPanel.tbPSUnInstall.Text.ToLowerInvariant().Contains("(x86)"))
                        oNewPanel.tbArchitecture.Text = "X64";


                    if (oNewPanel.tbPSUnInstall.Text.ToUpperInvariant().Contains("/SILENT"))
                    {
                        oNewPanel.tbPSInstall.Text = oNewPanel.tbPSInstall.Text.Replace("/?", "/SP- /VERYSILENT /NORESTART");
                    }

                }

                tabWizard.SelectedItem = tabNewSWSMI;
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        public static ImageSource ByteToImage(byte[] imageData)
        {
            try
            {
                BitmapImage biImg = new BitmapImage();
                MemoryStream ms = new MemoryStream(imageData);
                biImg.BeginInit();
                biImg.StreamSource = ms;
                biImg.EndInit();

                ImageSource imgSrc = biImg as ImageSource;
                return imgSrc;
            }
            catch { }

            return null;
        }

        private void btNextStart_Click(object sender, RoutedEventArgs e)
        {
            tabWizard.SelectedItem = tabMain;
            //tabWizard.SelectedItem = tabScan;
            //lSoftware.Clear();
            //btScan_Click(this, e);
        }

        private void tabWizard_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(tabWizard.SelectedItem != tabNewSWSMI && e.Source == tabWizard)
                oNewPanel.unload();

            if (tabWizard.SelectedItem == tabNewSWARP && e.Source == tabWizard)
            {
                Mouse.OverrideCursor = Cursors.Wait;
                try
                {
                    arpGrid2.AutoGenerateColumns = false;
                    List<GetSoftware> lServer = new List<GetSoftware>();
                    if (oInstPanel.lvSW.ItemsSource == null)
                    {
                        lServer = RZRestAPI.SWResults("").OrderBy(t => t.Shortname).ThenByDescending(t => t.ProductVersion).ThenByDescending(t => t.ProductName).ToList();
                    }
                    else
                    {
                        lServer = oInstPanel.lvSW.ItemsSource as List<GetSoftware>;
                    }
                    
                    if(lServer == null)
                        lServer = RZRestAPI.SWResults("").OrderBy(t => t.Shortname).ThenByDescending(t => t.ProductVersion).ThenByDescending(t => t.ProductName).ToList();

                    if (Keyboard.Modifiers == ModifierKeys.Shift)
                    {
                        arpGrid2.ItemsSource = lSoftware.OrderBy(t => t.ProductName).ThenBy(t => t.ProductVersion).ThenBy(t => t.Manufacturer).ToList();
                    }
                    else
                    {
                        arpGrid2.ItemsSource = lSoftware.Where(t => lServer.Count(x => x.ProductName == t.ProductName && x.Manufacturer == t.Manufacturer && x.ProductVersion == t.ProductVersion) == 0).OrderBy(t => t.ProductName).ThenBy(t => t.ProductVersion).ThenBy(t => t.Manufacturer).ToList();
                    }
                }
                finally
                {
                    Mouse.OverrideCursor = null; 
                }
            }
        }

        private void btNextScan_Click(object sender, RoutedEventArgs e)
        {
            tabWizard.SelectedItem = tabMain;
        }

        private void btNextScanResult_Click(object sender, RoutedEventArgs e)
        {
            tabWizard.SelectedItem = tabMain;
        }

        private void btBackScanResult_Click(object sender, RoutedEventArgs e)
        {
            tabWizard.SelectedItem = tabScan;
        }

        private void btFinishMain_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void btBackInstall_Click(object sender, RoutedEventArgs e)
        {
            btNextScan.IsEnabled = true;
            btBackScan.IsEnabled = false;
            tabWizard.SelectedItem = tabMain;
        }

        private void btBackNewSWSMI_Click(object sender, RoutedEventArgs e)
        {
            tabWizard.SelectedItem = tabMain;
        }

        private void btBackNewSWARP_Click(object sender, RoutedEventArgs e)
        {
            tabWizard.SelectedItem = tabMain;
        }

        private void btFinishNewSWARP_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void btFinishNewSWSMI_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void btUpdateSoftware_Click(object sender, RoutedEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                foreach (string sException in Properties.Settings.Default.UpdExlusion)
                {
                    lNewVersion.RemoveAll(t => t.Shortname == sException);
                }

                oUpdPanel.lvSW.ItemsSource = lNewVersion; //oAPI.CheckForUpdate(lSoftware.Select(t => new RZApi.AddSoftware() {  ProductName = t.ProductName, ProductVersion = t.ProductVersion, Manufacturer = t.Manufacturer }).ToArray());
                oUpdPanel.lInstalledSW = oSCAN.InstalledSoftware;
                oUpdPanel.lSWRep = oSCAN.SoftwareRepository;
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }

            tabWizard.SelectedItem = tabUpdateSW;
        }

        private void btFinishInstall_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private string GetTimeToken()
        {
            byte[] time = BitConverter.GetBytes(DateTime.UtcNow.ToBinary());
            byte[] key = Guid.NewGuid().ToByteArray();
            return Convert.ToBase64String(time.Concat(key).ToArray());
        }

        private void btBackSettings_Click(object sender, RoutedEventArgs e)
        {
            tabWizard.SelectedItem = tabMain;
            /*if (tbURL.Text != Properties.Settings.Default.InternalURL)
            {
                Properties.Settings.Default.InternalURL = tbURL.Text;
                Properties.Settings.Default.LocallRepository = tbRepository.Text;
                Properties.Settings.Default.Save();
            }*/

            if(!string.IsNullOrEmpty(tbSVC.Text))
            {
                if (tbSVC.Text != Properties.Settings.Default.WebService)
                {
                    //Properties.Settings.Default.WebService = tbSVC.Text;
                }
            }
        }

        private void tabSettings_Loaded(object sender, RoutedEventArgs e)
        {
            tbUsername.Text = Properties.Settings.Default.UserKey;
            tbPassword.Password = Decrypt(Properties.Settings.Default.UserPW, Environment.UserName);
        }

        private void btSettingsSave_Click(object sender, RoutedEventArgs e)
        {
            string sResponse = RZRestAPI.GetAuthToken(tbUsername.Text, tbPassword.Password);
            try
            {
                //Check if GUID came back..
                Guid.Parse(sResponse);

                //Enable InternalURL
                //tbURL.IsEnabled = true;
                tbIPFSGW.IsEnabled = true;

                //Update and save new username and password
                Properties.Settings.Default.UserKey = tbUsername.Text;
                Properties.Settings.Default.UserPW = Encrypt(tbPassword.Password, Environment.UserName);
                //Properties.Settings.Default.InternalURL = tbURL.Text;
                Properties.Settings.Default.IPFSGW = tbIPFSGW.Text;
                RuckZuck_WCF.RZRestAPI.ipfs_GW_URL = tbIPFSGW.Text;
                Properties.Settings.Default.Save();

                //oInstPanel.sInternalURL = tbURL.Text;

                //Back to Main
                //tabWizard.SelectedItem = tabMain;
                tbUsername.BorderBrush = Brushes.Green;

                oInstPanel.EnableFeedback = true;
                oInstPanel.EnableEdit = true;
                oInstPanel.EnableSupport = true;

                oSCAN.SoftwareRepository = new List<GetSoftware>();
                oSCAN.GetSWRepository().ConfigureAwait(false);

            }
            catch 
            {
                //Username or Password are wrong !
                tbUsername.BorderBrush = Brushes.Red;
                tbPassword.BorderBrush = Brushes.Red;
                tbUsername.ToolTip = sResponse;
                tbPassword.ToolTip = sResponse;
                //oInstPanel.sInternalURL = "";
            }
        }

        private void btOpenSettings_Click(object sender, RoutedEventArgs e)
        {
            tabWizard.SelectedItem = tabSettings;
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void btUpdExclusion_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.UpdExlusion.Clear();
            Properties.Settings.Default.Save();
        }

        private void cbRZCache_Checked(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.DisableBroadcast = !cbRZCache.IsChecked ?? false;
            Properties.Settings.Default.Save();
        }
    }
}
