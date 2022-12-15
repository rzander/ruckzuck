using RuckZuck.Base;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Input;

namespace RuckZuck_Tool
{
    /// <summary>
    /// Interaction logic for DownloadMonitor.xaml
    /// </summary>
    public partial class DownloadMonitor : Window
    {
        public ObservableCollection<DLTask> lDLTasks = new ObservableCollection<DLTask>();
        delegate void AnonymousDelegate();
        System.Timers.Timer tDelay;
        public event EventHandler AllDone = delegate { };
        private ReaderWriterLockSlim UILock = new ReaderWriterLockSlim();
        internal DateTime tLastRefresh = DateTime.Now;

        public DownloadMonitor()
        {
            InitializeComponent();
            tDelay = new System.Timers.Timer(300);
            tDelay.Elapsed += tDelay_Elapsed;
            tDelay.AutoReset = false;

            lDLTasks.CollectionChanged += LDLTasks_CollectionChanged;
        }

        public void RefreshData()
        {
            //Debug.WriteLine("RefreshData: " + DateTime.Now.ToString("HH:mm:ss.fff"));
            if (DateTime.Now - tLastRefresh < new TimeSpan(0, 0, 0, 1, 0))
            {
                tDelay.Interval = 300;
                tDelay.Start();
            }
            else
            {
                tDelay_Elapsed(this, null);
            }
        }

        private void tDelay_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            //Debug.WriteLine("TaskUpdate: " + DateTime.Now.ToString("HH:mm:ss.fff"));

            tDelay.Stop();
            TaskUpdate();
            tLastRefresh = DateTime.Now;
        }

        private void LDLTasks_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            //Debug.WriteLine("CollectionChanged: " + DateTime.Now.ToLongTimeString());
            tDelay.Interval = 300;
            tDelay.Start();
        }

        private void TaskUpdate()
        {
            UILock.TryEnterReadLock(350);
            try
            {
                bool bInstalling = false;

                AnonymousDelegate update = delegate ()
                {

                    lvDL.ItemsSource = null;

                    lvDL.ItemsSource = lDLTasks;
                    lvDL.UpdateLayout();
                };
                Dispatcher.Invoke(update);


                if (lDLTasks.Count > 0)
                {
                    if (lDLTasks.Count(t => !t.Installed) == 0)
                    {
                        AllDone(this, EventArgs.Empty);
                    }
                }

                if (lDLTasks.Count(t => t.Installing) > 0)
                {
                    bInstalling = true;
                    tDelay.Interval = 800;
                    tDelay.Start();
                    return;
                }

                foreach (DLTask oDL in lDLTasks.Where(t => t.AutoInstall && t.PercentDownloaded == 100 && !t.Error && !t.Installed && !t.Downloading && !t.WaitingForDependency && !t.Installing && !t.UnInstalled))
                {
                    try
                    {
                        Mutex mRes = null;
                        if (!Mutex.TryOpenExisting(@"RuckZuck", out mRes))
                        {
                            _ = oDL.SWUpd.InstallAsync(false, false);
                        }

                        return;
                    }
                    catch
                    {
                    }
                }

                foreach (DLTask oDL in lDLTasks.Where(t => t.WaitingForDependency && t.PercentDownloaded == 100 && !t.Error && !t.Installed && !t.Downloading && !t.Installing && !t.UnInstalled))
                {
                    try
                    {
                        bool allDone = true;
                        foreach (string sPreReq in oDL.SWUpd.SW.PreRequisites)
                        {
                            if (lDLTasks.Count(t => t.ShortName == sPreReq && !t.Installed && !t.Error) != 0)
                            {
                                allDone = false;
                            }
                        }

                        if (allDone)
                        {
                            if (!bInstalling)
                            {
                                Mutex mRes = null;
                                if (!Mutex.TryOpenExisting(@"RuckZuck", out mRes))
                                {
                                    _ = oDL.SWUpd.InstallAsync(false, false);
                                }

                                return;
                            }
                        }
                        else
                        {
                            if (!oDL.Downloading && !oDL.WaitingForDependency && oDL.AutoInstall)
                                if (!bInstalling)
                                {
                                    Mutex mRes = null;
                                    if (!Mutex.TryOpenExisting(@"RuckZuck", out mRes))
                                    {
                                        _ = oDL.SWUpd.InstallAsync(false, false);
                                    }

                                    return;
                                }
                        }
                    }
                    catch
                    {
                    }
                }

                tDelay.Interval = 300;
                tDelay.Start();
            }
            catch
            {
                tDelay.Interval = 500;
                tDelay.Start();
            }
            finally
            {
                UILock.ExitReadLock();
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                e.Cancel = true;
                tDelay.Stop();

                UILock.TryEnterReadLock(5000);
                try
                {
                    var xRem = lDLTasks.Where(x => x.Installed || x.Error || (x.PercentDownloaded == 100 && x.AutoInstall == false) || (x.Status == "Waiting" && x.DownloadedBytes == 0 && x.Downloading == false) || x.UnInstalled).ToList();
                    foreach (var o in xRem)
                    {
                        try
                        {
                            lDLTasks.Remove(o);
                        }
                        catch { }
                    }
                    //xRem.ForEach(x => lDLTasks.Remove(x));
                }
                catch { }

                Hide();
            }
            finally
            {
                UILock.ExitReadLock();
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F5)
            {
                LDLTasks_CollectionChanged(sender, null);
            }

        }
    }
}
