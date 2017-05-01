using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Navigation;
using System.Diagnostics;
using System.Globalization;
using RuckZuck_WCF;
using RZUpdate;
using System.Threading;

namespace RuckZuck_Tool
{
    /// <summary>
    /// Interaction logic for InstallSwPanel.xaml
    /// </summary>
    public partial class UpdateSwPanel : UserControl
    {
        public string sInternalURL;
        public event EventHandler OnSWUpdated = delegate { };
        internal DownloadMonitor dm = new DownloadMonitor();
        internal List<AddSoftware> lInstalledSW;
        internal List<GetSoftware> lSWRep;
        public delegate void ChangedEventHandler(object sender, EventArgs e);
        public event ChangedEventHandler onEdit;

        public UpdateSwPanel()
        {
            InitializeComponent();
            dm.AllDone += Dm_AllDone;
        }

        private void Dm_AllDone(object sender, EventArgs e)
        {
            OnSWUpdated(this, new EventArgs());
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

        private void lvSW_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
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
            foreach(AddSoftware oItem in lvSW.SelectedItems)
            {
                try
                {
                    SWUpdate oSW = new SWUpdate(oItem);
                    oSW.GetInstallType();

                    if (dm.lDLTasks.FirstOrDefault(t => t.ProductName == oSW.SW.ProductName) == null)
                    {
                        //oSW.Downloaded += OSW_Downloaded;
                        oSW.ProgressDetails += OSW_ProgressDetails;
                        oSW.downloadTask.AutoInstall = true;
                        oSW.Download(false).ConfigureAwait(false);
                        dm.lDLTasks.Add(oSW.downloadTask);

                        foreach (string sPreReq in oSW.SW.PreRequisites)
                        {
                            try
                            {
                                SWUpdate oPreReq = new SWUpdate(sPreReq);
                                oPreReq.GetInstallType();
                                if (dm.lDLTasks.FirstOrDefault(t => t.ProductName == oPreReq.SW.ProductName) == null)
                                {
                                    //oPreReq.Downloaded += OSW_Downloaded;
                                    oPreReq.ProgressDetails += OSW_ProgressDetails;
                                    oPreReq.downloadTask.AutoInstall = true;
                                    oPreReq.Download(false).ConfigureAwait(false);
                                    dm.lDLTasks.Add(oPreReq.downloadTask);
                                }

                            }
                            catch { }

                        }
                    }
                    dm.Show();

                }
                catch { }

                OnSWUpdated(this, new EventArgs());
            }
            
        }

        private void btInstallAll_Click(object sender, RoutedEventArgs e)
        {
            List<AddSoftware> lSW = lvSW.ItemsSource as List<AddSoftware>;
            foreach (var oItem in lSW)
            {
                try
                {
                    SWUpdate oSW = new SWUpdate(oItem);
                    oSW.GetInstallType();

                    if (dm.lDLTasks.FirstOrDefault(t => t.ProductName == oSW.SW.ProductName) == null)
                    {
                        //oSW.Downloaded += OSW_Downloaded;
                        oSW.ProgressDetails += OSW_ProgressDetails;
                        oSW.downloadTask.AutoInstall = true;
                        oSW.Download(false).ConfigureAwait(false);
                        dm.lDLTasks.Add(oSW.downloadTask);

                        foreach (string sPreReq in oSW.SW.PreRequisites)
                        {
                            try
                            {
                                SWUpdate oPreReq = new SWUpdate(sPreReq);
                                oPreReq.GetInstallType();
                                if (dm.lDLTasks.FirstOrDefault(t => t.ProductName == oPreReq.SW.ProductName) == null)
                                {
                                    //oPreReq.Downloaded += OSW_Downloaded;
                                    oPreReq.ProgressDetails += OSW_ProgressDetails;
                                    oPreReq.downloadTask.AutoInstall = true;
                                    oPreReq.Download(false).ConfigureAwait(false);
                                    dm.lDLTasks.Add(oPreReq.downloadTask);
                                }

                            }
                            catch { }

                        }
                    }
                    dm.Show();

                }
                catch { }

                OnSWUpdated(this, new EventArgs());
            }

            
        }

        private void OSW_ProgressDetails(object sender, EventArgs e)
        {
            dm.RefreshData();
        }

        private void miIgnoreUpdate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (lvSW.SelectedItems.Count > 0)
                {
                    foreach(AddSoftware oSW in lvSW.SelectedItems)
                    {
                        if(!Properties.Settings.Default.UpdExlusion.Contains(oSW.Shortname))
                            Properties.Settings.Default.UpdExlusion.Add(oSW.Shortname);
                        ((List<AddSoftware>)lvSW.ItemsSource).Remove(oSW);
                    }
                    Properties.Settings.Default.Save();
                    var oList = ((List<AddSoftware>)lvSW.ItemsSource).ToList();
                    lvSW.ItemsSource = null;
                    lvSW.ItemsSource = oList;
                }
                
                
            }
            catch { }
        }

        private void miOpenPage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (lvSW.SelectedItems.Count > 0)
                {
                    try
                    {
                        string sShortName = ((AddSoftware)lvSW.SelectedItem).Shortname;
                        
                        Process.Start(lSWRep.Where(t => t.Shortname == sShortName).FirstOrDefault().ProductURL);
                    }
                    catch { }
                }
            }
            catch { }
        }

        private void miUninstall_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (lvSW.SelectedItems.Count > 0)
                {
                    try
                    {
                        string sProdName = ((AddSoftware)lvSW.SelectedItem).ProductName;
                        
                        List<AddSoftware> possibleSW = lInstalledSW.Where(t => t.Manufacturer == ((AddSoftware)lvSW.SelectedItem).Manufacturer & t.ProductVersion == ((AddSoftware)lvSW.SelectedItem).MSIProductID).ToList();
                        if(possibleSW.Count == 1)
                        {
                            SWUpdate._RunPS(possibleSW[0].PSUninstall.ToString());
                        }

                        if (possibleSW.Count > 1)
                        {
                            bool bRun = false;
                            foreach(AddSoftware aSW in possibleSW)
                            {
                                string subProdName = new String(sProdName.Where(c => c != '-' && c != '.' &&(c < '0' || c > '9')).ToArray()).Trim();
                                if(subProdName == new String(aSW.ProductName.Where(c => c != '-' && c != '.' && (c < '0' || c > '9')).ToArray()).Trim())
                                {
                                    SWUpdate._RunPS(aSW.PSUninstall.ToString());
                                    bRun = true;
                                    continue;
                                }
                            }

                            if(!bRun)
                            {
                                Process.Start("control", "appwiz.cpl");
                            }
                            
                        }



                    }
                    catch { }
                }
            }
            catch { }
        }

        private void miEdit_Click(object sender, RoutedEventArgs e)
        {
            lvSW.ContextMenu.IsOpen = false;
            Thread.Sleep(200);

            Dispatcher.Invoke(new Action(() => { }), System.Windows.Threading.DispatcherPriority.ContextIdle, null);

            if (lvSW.SelectedItems.Count > 0)
            {
                GetSoftware oSelectedItem = lvSW.SelectedItems[0] as GetSoftware;
                if (onEdit != null)
                    onEdit(oSelectedItem, EventArgs.Empty);
            }
        }
    }
}
