using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;

using System.Diagnostics;
using System.Threading;
using System.Globalization;
using System.Threading.Tasks;
using RZUpdate;
using System.IO;
using RuckZuck.Base;

namespace RuckZuck_Tool
{
    /// <summary>
    /// Interaction logic for InstallSwPanel.xaml
    /// </summary>
    public partial class InstallSwPanel : UserControl
    {
        public string sAuthToken;
        //public string sInternalURL;
        //public List<RZApi.GetSoftware> lAllSoftware;
        public List<GetSoftware> lAllSoftware;
        public List<AddSoftware> lSoftware = new List<AddSoftware>();
        public System.Timers.Timer tSearch = new System.Timers.Timer(1000);
        delegate void AnonymousDelegate();
        public event EventHandler OnSWUpdated = delegate { };

        public List<DLTask> lDLTasks = new List<DLTask>();

        internal DownloadMonitor dm = new DownloadMonitor();


        public delegate void ChangedEventHandler(object sender, EventArgs e);
        public event ChangedEventHandler onEdit;

        public InstallSwPanel()
        {
            InitializeComponent();
            tbSearch.Text = "";
            tSearch.Elapsed += TSearch_Elapsed;
            tSearch.Enabled = false;
            tSearch.AutoReset = false;
        }

        private void TSearch_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {

            AnonymousDelegate update = delegate ()
            {
                if (tbSearch.IsFocused)
                    tbSearch_Search(sender, null);
                else
                    tSearch.Stop();
            };
            Dispatcher.Invoke(update);

        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
                e.Handled = true;
            }
            catch { }
        }

        private void tbSearch_GotFocus(object sender, RoutedEventArgs e)
        {
            tSearch.Stop();
            tbSearch.Foreground = new SolidColorBrush(Colors.Black);
            if (tbSearch.Tag != null)
                tbSearch.Text = "";
        }

        private void tbSearch_LostFocus(object sender, RoutedEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            if (string.IsNullOrEmpty(tbSearch.Text))
            {
                tbSearch.Foreground = new SolidColorBrush(Colors.LightGray);
                tbSearch.Tag = "Search";
                tbSearch.Text = "Search...";
            }
            else
            {
                tbSearch.Foreground = new SolidColorBrush(Colors.Black);
                tbSearch.Tag = null;

                try
                {
                    var vResult = lAllSoftware.FindAll(t => t.ShortName.IndexOf(tbSearch.Text, 0, StringComparison.InvariantCultureIgnoreCase) >= 0).ToList();
                    vResult.AddRange(lAllSoftware.FindAll(t => t.ProductName.IndexOf(tbSearch.Text, 0, StringComparison.InvariantCultureIgnoreCase) >= 0).ToList());
                    vResult.AddRange(lAllSoftware.FindAll(t => t.Manufacturer.IndexOf(tbSearch.Text, 0, StringComparison.InvariantCultureIgnoreCase) >= 0).ToList());
                    if (vResult.Count <= 15)
                    {
                        vResult.AddRange(lAllSoftware.FindAll(t => (t.Description ?? "").IndexOf(tbSearch.Text, 0, StringComparison.InvariantCultureIgnoreCase) >= 0).ToList());
                    }

                    lvSW.ItemsSource = vResult.Distinct().OrderBy(t => t.ShortName).ThenByDescending(t => t.ProductVersion).ThenByDescending(t => t.ProductName);
                }
                catch { }
            }
            Mouse.OverrideCursor = null;
        }

        private void tbSearch_Search(object sender, RoutedEventArgs e)
        {

            Mouse.OverrideCursor = Cursors.Wait;
            if (string.IsNullOrEmpty(tbSearch.Text))
            {
                tbSearch.Foreground = new SolidColorBrush(Colors.LightGray);
                tbSearch.Tag = "Search";
                tbSearch.Text = "Search...";


                ListCollectionView lcv = new ListCollectionView(lAllSoftware.Distinct().OrderBy(t => t.ShortName).ThenByDescending(t => t.ProductVersion).ThenByDescending(t => t.ProductName).ToList());

                //ListCollectionView lcv = new ListCollectionView(oAPI.SWResults("", "").Distinct().OrderBy(t => t.ShortName).ThenByDescending(t => t.ProductVersion).ThenByDescending(t => t.ProductName).ToList());
                PropertyGroupDescription PGD = new PropertyGroupDescription("", new ShortNameToCategory());

                //PGD.GroupNames.Add(RZRestAPI.GetCategories(lAllSoftware));
                foreach (var o in RZRestAPIv2.GetCategories(lAllSoftware))
                {
                    PGD.GroupNames.Add(o);
                }


                lcv.GroupDescriptions.Add(PGD);

                lvSW.ItemsSource = lcv;
            }
            else
            {
                tbSearch.Foreground = new SolidColorBrush(Colors.Black);
                tbSearch.Tag = null;

                try
                {
                    var vResult = lAllSoftware.FindAll(t => t.ShortName.IndexOf(tbSearch.Text, 0, StringComparison.InvariantCultureIgnoreCase) >= 0).ToList();
                    vResult.AddRange(lAllSoftware.FindAll(t => t.ProductName.IndexOf(tbSearch.Text, 0, StringComparison.InvariantCultureIgnoreCase) >= 0).ToList());
                    vResult.AddRange(lAllSoftware.FindAll(t => t.Manufacturer.IndexOf(tbSearch.Text, 0, StringComparison.InvariantCultureIgnoreCase) >= 0).ToList());
                    if (vResult.Count <= 15)
                    {
                        vResult.AddRange(lAllSoftware.FindAll(t => (t.Description ?? "").IndexOf(tbSearch.Text, 0, StringComparison.InvariantCultureIgnoreCase) >= 0).ToList());
                    }

                    lvSW.ItemsSource = vResult.Distinct().OrderBy(t => t.ShortName).ThenByDescending(t => t.ProductVersion).ThenByDescending(t => t.ProductName);
                }
                catch { }
            }
            Mouse.OverrideCursor = null;
        }

        public class ShortNameToCategory : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                if (value.GetType() == typeof(GetSoftware))
                {
                    GetSoftware oSW = (GetSoftware)value;

                    return oSW.Categories[0];
                }

                return null;

            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                throw new NotImplementedException();
            }
        }

        private void lvSW_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            tSearch.Stop();
            if (lvSW.SelectedItems.Count > 0)
            {
                btInstall.IsEnabled = true;
            }
            else
            {
                btInstall.IsEnabled = false;
            }
        }

        private void btInstall_Click(object sender, RoutedEventArgs e)
        {
            if (lvSW.SelectedItem != null)
            {
                tSearch.Stop();
                try
                {
                    foreach (var oItem in lvSW.SelectedItems)
                    {
                        try
                        {
                            SWUpdate oSW = null;
                            if (oItem.GetType() == typeof(GetSoftware))
                            {
                                GetSoftware dgr = oItem as GetSoftware;

                                if (oSW == null)
                                    oSW = new SWUpdate(dgr.ProductName, dgr.ProductVersion, dgr.Manufacturer);

                                if (oSW.SW == null)
                                {
                                    dm.lDLTasks.Add(new DLTask() { ProductName = dgr.ProductName, ProductVersion = dgr.ProductVersion, Error = true, ErrorMessage = "Requirements not valid. Installation will not start." });
                                    dm.Show();
                                    continue;
                                }
                            }


                            if (oItem.GetType() == typeof(AddSoftware))
                            {
                                AddSoftware dgr = oItem as AddSoftware;
                                //sPS = GetSWInstallPS(dgr.ProductName, dgr.ProductVersion, "");
                                oSW = new SWUpdate(dgr);

                                if (oSW.SW == null)
                                {
                                    dm.lDLTasks.Add(new DLTask() { ProductName = dgr.ProductName, ProductVersion = dgr.ProductVersion, Error = true, ErrorMessage = "Requirements not valid. Installation will not start." });
                                    dm.Show();
                                    continue;
                                }
                            }

                            try
                            {
                                var xRem = dm.lDLTasks.Where(x => x.ProductName == oSW.SW.ProductName && (x.Error || (x.PercentDownloaded == 100 && x.AutoInstall == false) || (x.Status == "Waiting" && x.DownloadedBytes == 0 && x.Downloading == false) || x.UnInstalled)).ToList();
                                foreach (var o in xRem)
                                {
                                    try
                                    {
                                        dm.lDLTasks.Remove(o);
                                    }
                                    catch { }
                                }
                                //xRem.ForEach(x => lDLTasks.Remove(x));
                            }
                            catch { }


                            //Allow only one entry
                            if (dm.lDLTasks.FirstOrDefault(t => t.ProductName == oSW.SW.ProductName) == null)
                            {
                                //oSW.Downloaded += OSW_Downloaded;
                                oSW.ProgressDetails += OSW_ProgressDetails;
                                oSW.downloadTask.AutoInstall = true;

                                oSW.Download(false).ConfigureAwait(false); ;
                                dm.lDLTasks.Add(oSW.downloadTask);

                                foreach (string sPreReq in oSW.SW.PreRequisites)
                                {
                                    try
                                    {
                                        SWUpdate oPreReq = new SWUpdate(sPreReq);
                                        if (oPreReq.GetInstallType())
                                        {
                                            if (dm.lDLTasks.FirstOrDefault(t => t.ProductName == oPreReq.SW.ProductName) == null)
                                            {
                                                //oPreReq.Downloaded += OSW_Downloaded;
                                                oPreReq.ProgressDetails += OSW_ProgressDetails;
                                                oPreReq.downloadTask.AutoInstall = true;
                                                oPreReq.Download(false).ConfigureAwait(false); ;
                                                dm.lDLTasks.Add(oPreReq.downloadTask);
                                            }
                                        }

                                    }
                                    catch { }

                                }
                            }
                            dm.Show();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(ex.Message);
                        }
                    }
                }
                catch { }
                OnSWUpdated(this, new EventArgs());

            }
        }

        private void OSW_ProgressDetails(object sender, EventArgs e)
        {
            dm.RefreshData();
        }

        private void tbSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                lvSW.Focus();
                tSearch.Stop();
                tSearch.Enabled = false;
                tSearch.Interval = 1000;
            }
            else
            {
                tSearch.Interval = 1000;
                tSearch.Enabled = true;
                tSearch.Start();
            }
        }

        private void miOpenPage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (lvSW.SelectedItems.Count > 0)
                {
                    Process.Start(((GetSoftware)lvSW.SelectedItem).ProductURL.ToString());
                }
            }
            catch { }
        }

        private void miSendFeedback_Click(object sender, RoutedEventArgs e)
        {
            if (lvSW.SelectedItems.Count > 0)
            {
                try
                {
                    GetSoftware oSelectedItem = ((GetSoftware)lvSW.SelectedItem);
                    var vDB = Task.Run(() =>
                    {
                        try
                        {
                            AnonymousDelegate update = delegate ()
                            {
                                FeedbackForm oFeedBack = new FeedbackForm();
                                oFeedBack.Title = oSelectedItem.ProductName + " " + oSelectedItem.ProductVersion;
                                oFeedBack.ShowDialog();

                                if (oFeedBack.hasFeedback)
                                {
                                    RZRestAPIv2.Feedback(oSelectedItem.ProductName, oSelectedItem.ProductVersion, oSelectedItem.Manufacturer, oFeedBack.isWorking.ToString(), System.Reflection.Assembly.GetExecutingAssembly().GetName().Name, oFeedBack.tbFeedback.Text, RZRestAPIv2.CustomerID).ConfigureAwait(false); ;
                                }
                            };
                            Dispatcher.Invoke(update);
                        }
                        catch { }
                    });
                }
                catch { }
            }
        }

        private void miDownloadFiles_Click(object sender, RoutedEventArgs e)
        {
            if (lvSW.SelectedItem != null)
            {
                try
                {
                    foreach (var oItem in lvSW.SelectedItems)
                    {
                        try
                        {
                            SWUpdate oSW = null;
                            if (oItem.GetType() == typeof(GetSoftware))
                            {
                                GetSoftware dgr = oItem as GetSoftware;
                                //sPS = GetSWInstallPS(dgr.ProductName, dgr.ProductVersion, "");
                                oSW = new SWUpdate(dgr.ProductName, dgr.ProductVersion, dgr.Manufacturer);
                            }


                            if (oItem.GetType() == typeof(AddSoftware))
                            {
                                AddSoftware dgr = oItem as AddSoftware;
                                //sPS = GetSWInstallPS(dgr.ProductName, dgr.ProductVersion, "");
                                oSW = new SWUpdate(dgr);
                            }

                            //lDLTasks.Add(oSW.downloadTask);
                            if (dm.lDLTasks.FirstOrDefault(t => t.ProductName == oSW.SW.ProductName) == null)
                            {
                                //oSW.Downloaded += OSW_Downloaded;
                                oSW.ProgressDetails += OSW_ProgressDetails;
                                oSW.downloadTask.AutoInstall = false;
                                oSW.Download(false).ConfigureAwait(false); 
                                dm.lDLTasks.Add(oSW.downloadTask);

                            }
                            dm.Show();

                            continue;

                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(ex.Message);
                        }
                    }
                }
                catch { }
                OnSWUpdated(this, new EventArgs());

            }

        }

        private void miEdit_Click(object sender, RoutedEventArgs e)
        {
            lvSW.ContextMenu.IsOpen = false;
            Thread.Sleep(200);

            Dispatcher.Invoke(new Action(() => { }), System.Windows.Threading.DispatcherPriority.ContextIdle, null);

            if (lvSW.SelectedItems.Count > 0)
            {
                GetSoftware oSelectedItem = lvSW.SelectedItems[0] as GetSoftware;
                onEdit?.Invoke(oSelectedItem, EventArgs.Empty);
            }
        }

        private void btOldFeedback_Click(object sender, RoutedEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                var oldSW = RZRestAPIv2.GetCatalog("--old--"); //.Distinct().Select(x => new GetSoftware() { Categories = x.Categories.ToList(), Description = x.Description, Downloads = x.Downloads, IconId = x.IconId, Image = x.Image, Manufacturer = x.Manufacturer, ProductName = x.ProductName, ProductURL = x.ProductURL, ProductVersion = x.ProductVersion, Quality = x.Quality, ShortName = x.ShortName, isInstalled = false }).ToList();
                tbSearch.Text = "";

                //Mark all installed...
                oldSW.ForEach(x => { if (lSoftware.FirstOrDefault(t => (t.ProductName == x.ProductName && t.ProductVersion == x.ProductVersion)) != null) { x.isInstalled = true; } });

                ListCollectionView lcv = new ListCollectionView(oldSW.ToList());

                lvSW.ItemsSource = lcv;

            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private void btNew_Click(object sender, RoutedEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                tSearch.Stop();
                var badSW = RZRestAPIv2.GetCatalog("--new--");
                tbSearch.Text = "";

                //Mark all installed...
                badSW.ForEach(x => { if (lSoftware.FirstOrDefault(t => (t.ProductName == x.ProductName && t.ProductVersion == x.ProductVersion)) != null) { x.isInstalled = true; } });


                ListCollectionView lcv = new ListCollectionView(badSW.ToList());

                lvSW.ItemsSource = lcv;
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private void miOpenFolders_Click(object sender, RoutedEventArgs e)
        {
            tSearch.Stop();
            if (lvSW.SelectedItems.Count > 0)
            {
                int iSelecteItems = lvSW.SelectedItems.Count;
                foreach (GetSoftware oSelectedItem in lvSW.SelectedItems)
                {
                    try
                    {
                        //AddSoftware oItem = Converter.Convert(Converter.Convert(oSelectedItem, oAPI));
                        SWUpdate oUpd = new SWUpdate(oSelectedItem.ProductName, oSelectedItem.ProductVersion, oSelectedItem.Manufacturer);
                        Process.Start("explorer.exe", oUpd.GetDLPath());
                    }
                    catch { }
                }
            }
        }

        private void miInstall_Click(object sender, RoutedEventArgs e)
        {
            tSearch.Stop();
            lvSW.ContextMenu.IsOpen = false;
            Thread.Sleep(200);

            Dispatcher.Invoke(new Action(() => { }), System.Windows.Threading.DispatcherPriority.ContextIdle, null);

            btInstall_Click(sender, e);
        }

        private void miUninstall_Click(object sender, RoutedEventArgs e)
        {
            tSearch.Stop();
            lvSW.ContextMenu.IsOpen = false;
            Thread.Sleep(200);

            Dispatcher.Invoke(new Action(() => { }), System.Windows.Threading.DispatcherPriority.ContextIdle, null);

            if (lvSW.SelectedItem != null)
            {
                try
                {
                    foreach (var oItem in lvSW.SelectedItems)
                    {
                        try
                        {
                            SWUpdate oSW = null;
                            if (oItem.GetType() == typeof(GetSoftware))
                            {
                                GetSoftware dgr = oItem as GetSoftware;
                                //sPS = GetSWInstallPS(dgr.ProductName, dgr.ProductVersion, "");
                                oSW = new SWUpdate(dgr.ProductName, dgr.ProductVersion, dgr.Manufacturer);

                                if (oSW.SW == null)
                                {
                                    dm.lDLTasks.Add(new DLTask() { ProductName = dgr.ProductName, ProductVersion = dgr.ProductVersion, Error = true, ErrorMessage = "Requirements not valid." });
                                    dm.Show();
                                    continue;
                                }
                            }


                            if (oItem.GetType() == typeof(AddSoftware))
                            {
                                AddSoftware dgr = oItem as AddSoftware;
                                //sPS = GetSWInstallPS(dgr.ProductName, dgr.ProductVersion, "");
                                oSW = new SWUpdate(dgr);

                                if (oSW.SW == null)
                                {
                                    dm.lDLTasks.Add(new DLTask() { ProductName = dgr.ProductName, ProductVersion = dgr.ProductVersion, Error = true, ErrorMessage = "Requirements not valid." });
                                    dm.Show();
                                    continue;
                                }
                            }

                            //lDLTasks.Add(oSW.downloadTask);
                            if (dm.lDLTasks.FirstOrDefault(t => t.ProductName == oSW.SW.ProductName) == null)
                            {
                                //oSW.Downloaded += OSW_Downloaded;
                                oSW.ProgressDetails += OSW_ProgressDetails;
                                oSW.downloadTask.AutoInstall = true;
                                dm.lDLTasks.Add(oSW.downloadTask);
                                oSW.UnInstall(false, false).ConfigureAwait(false);

                            }
                            dm.Show();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(ex.Message);
                        }
                    }
                }
                catch { }
                OnSWUpdated(this, new EventArgs());
            }
        }

        private void miCreateExe_Click(object sender, RoutedEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                foreach (var oItem in lvSW.SelectedItems)
                {
                    try
                    {
                        SWUpdate oSW = null;
                        if (oItem.GetType() == typeof(GetSoftware))
                        {
                            GetSoftware dgr = oItem as GetSoftware;
                            //sPS = GetSWInstallPS(dgr.ProductName, dgr.ProductVersion, "");
                            oSW = new SWUpdate(dgr.ProductName, dgr.ProductVersion, dgr.Manufacturer);
                        }

                        if (oItem.GetType() == typeof(AddSoftware))
                        {
                            AddSoftware dgr = oItem as AddSoftware;
                            //sPS = GetSWInstallPS(dgr.ProductName, dgr.ProductVersion, "");
                            oSW = new SWUpdate(dgr);
                        }

                        CreateExe oExe = new CreateExe(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, oSW.SW.ShortName + "_setup.exe"));

                        //Get Icon if missing
                        if (oSW.SW.Image == null)
                            oSW.SW.Image = RZRestAPIv2.GetIcon(oSW.SW.IconHash, "", 48);

                        oExe.Icon = oSW.SW.Image;
                        oExe.Sources.Add(Properties.Resources.Source.Replace("RZRZRZ", oSW.SW.ShortName));
                        oExe.Sources.Add(Properties.Resources.RZUpdate);
                        oExe.Sources.Add(Properties.Resources.RZRestApi);
                        oExe.Sources.Add(Properties.Resources.Assembly.Replace("RZRZRZ", oSW.SW.ShortName));

                        if (!oExe.Compile())
                        {
                            MessageBox.Show("Failed to create .Exe", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                        else
                        {
                            MessageBox.Show(oSW.SW.ShortName + "_setup.exe" + " created in: " + AppDomain.CurrentDomain.BaseDirectory, "EXE created", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                    catch { }
                }
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }
    }


}

