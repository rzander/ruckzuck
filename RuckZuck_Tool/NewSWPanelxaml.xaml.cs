using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WindowsInstaller;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Threading;
using RuckZuck_WCF;
using RZUpdate;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace RuckZuck_Tool
{
    /// <summary>
    /// Interaction logic for NewSWPanelxaml.xaml
    /// </summary>
    public partial class NewSWPanelxaml : UserControl
    {
        public List<contentFiles> lFiles = new List<contentFiles>();
        string sOldVersion = "";

        public NewSWPanelxaml()
        {
            InitializeComponent();
            //oApi = new RZApi.api();
            lFiles = new List<contentFiles>();

            dgSourceFiles.ItemsSource = lFiles.ToList();
            tbContentId.Text = Guid.NewGuid().ToString();
        }

        public void unload()
        {
            lFiles = new List<contentFiles>();
            dgSourceFiles.ItemsSource = lFiles.ToList();
        }

        private void tbMSIId_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(tbMSIId.Text))
            {
                string sMSI = tbMSIId.Text;
                sMSI = sMSI.Replace("{", "").Replace("}", "").Trim();
                string EncKey = descramble(sMSI.Replace("-", ""));

                if (string.IsNullOrEmpty(tbPSDetection.Text))
                    tbPSDetection.Text = @"Test-Path 'HKLM:\SOFTWARE\Classes\Installer\Products\" + EncKey + "'";
                if (string.IsNullOrEmpty(tbPSUnInstall.Text))
                    tbPSUnInstall.Text = "$proc = (Start-Process -FilePath \"msiexec.exe\" -ArgumentList \"/x {" + sMSI + "} /qb! REBOOT=REALLYSUPPRESS\" -Wait -PassThru);$proc.WaitForExit();$ExitCode = $proc.ExitCode";
                if (string.IsNullOrEmpty(tbPSInstall.Text))
                    tbPSInstall.Text = "$proc = (Start-Process -FilePath \"msiexec.exe\" -ArgumentList \"/i `\"" + "<name of the msi file>" + "`\" /qn ALLUSERS=2 REBOOT=REALLYSUPPRESS\" -Wait -PassThru);$proc.WaitForExit();$ExitCode = $proc.ExitCode";
            }
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

        private void btOpenMSI_Click(object sender, RoutedEventArgs e)
        {
            // Create OpenFileDialog 
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();

            // Set filter for file extension and default file extension 
            dlg.DefaultExt = "*.*";
            dlg.Filter = "MSI|*.msi";


            // Display OpenFileDialog by calling ShowDialog method 
            Nullable<bool> result = dlg.ShowDialog();


            // Get the selected file name and display in a TextBox 
            if (result == true)
            {
                try
                {
                    // Open document 
                    string sMSIfilename = dlg.FileName;
                    using (MSInstaller iMSI = new MSInstaller(sMSIfilename))
                    {
                        tbMSIId.Text = iMSI.Property("ProductCode");
                        string sHashType = "";
                        string sFileHash = "";
                        //Try to get File Signature...
                        try
                        {
                            var Cert = X509Certificate.CreateFromSignedFile(dlg.FileName);

                            sFileHash = Cert.GetCertHashString().ToLower().Replace(" ", "");
                            sHashType = "X509";
                        }
                        catch
                        {
                            sFileHash = GetMD5Hash(dlg.FileName);
                            sHashType = "MD5";
                        }

                        lFiles.Add(new contentFiles() { FileName = iMSI.FileName, FileHash = sFileHash, HashType = sHashType });
                        dgSourceFiles.ItemsSource = lFiles.ToList();
                        dgSourceFiles.Items.Refresh();

                        if (tbPSDetection.Text.Contains("\\Classes\\Installer\\Products"))
                            tbPSDetection.Text = "";
                        if (tbPSInstall.Text.Contains("-FilePath \"msiexec.exe\""))
                            tbPSInstall.Text = "";
                        if (tbPSUnInstall.Text.Contains("-FilePath \"msiexec.exe\""))
                            tbPSUnInstall.Text = "";

                        tbMSIId_LostFocus(this, e);

                        tbProductName.Text = iMSI.Property("ProductName");
                        tbVersion.Text = iMSI.Property("ProductVersion");
                        tbManufacturer.Text = iMSI.Property("Manufacturer");
                        tbArchitecture.Text = iMSI.MSIArchitecture.ToUpper();


                        if (string.IsNullOrEmpty(tbArchitecture.Text))
                            tbArchitecture.Text = "X86";

                        if (tbArchitecture.Text == "INTEL")
                            tbArchitecture.Text = "X86";

                        if (tbArchitecture.Text == "X64")
                        {
                            tbPSPrereq.Text = "[Environment]::Is64BitProcess";
                        }
                        else
                        {
                            tbPSPrereq.Text = "$true";
                        }

                        tbPSInstall.Text = tbPSInstall.Text.Replace("<name of the msi file>", iMSI.FileName);
                        tbContentId.Text = Guid.NewGuid().ToString();

                        try
                        {
                            if (!string.IsNullOrEmpty(iMSI.Property("ARPURLINFOABOUT")))
                                tbProductURL.Text = iMSI.Property("ARPURLINFOABOUT");
                        }
                        catch { }
                    }
                }
                catch { }
            }
        }

        public static string GetMD5Hash(string filename)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(filename))
                {
                    return BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", ""); ;
                }
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if(string.IsNullOrEmpty(tbDescription.Text))
            {
                if (MessageBox.Show("Description is empty !", "Warning", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK)
                    return;
            }

            if(tbPSInstall.Text.Contains("/?") | tbPSUnInstall.Text.Contains("/?"))
            {
                if (MessageBox.Show("Paremeter for silent In-Uninstall are missing or unknown ( \"/?\")", "Warning", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK)
                    return;
            }

            AddSoftware oSoftware = new AddSoftware();
            oSoftware.Architecture = tbArchitecture.Text;
            oSoftware.ContentID = tbContentId.Text;
            oSoftware.Description = tbDescription.Text;

            try
            {
                oSoftware.Files = ((List<contentFiles>)dgSourceFiles.ItemsSource);
            }
            catch { }
            try
            {
                List<contentFiles> lResult = new List<contentFiles>();
                foreach (contentFiles oFile in dgSourceFiles.ItemsSource)
                {
                    lResult.Add(new contentFiles() { FileHash = oFile.FileHash, FileName = oFile.FileName, HashType = oFile.HashType, URL = oFile.URL });
                }
                oSoftware.Files = lResult;
            }
            catch { }
            oSoftware.Manufacturer = tbManufacturer.Text;
            oSoftware.MSIProductID = tbMSIId.Text;
            oSoftware.ProductName = tbProductName.Text;
            oSoftware.ProductVersion = tbVersion.Text;
            oSoftware.PSDetection = tbPSDetection.Text;
            oSoftware.PSInstall = tbPSInstall.Text;
            oSoftware.PSPreReq = tbPSPrereq.Text;
            oSoftware.PSUninstall = tbPSUnInstall.Text;
            oSoftware.ProductURL = tbProductURL.Text;
            oSoftware.Author = Properties.Settings.Default.UserKey;
            oSoftware.PSPreInstall = tbPSPreInstall.Text;
            oSoftware.PSPostInstall = tbPSPostInstall.Text;

            if (!string.IsNullOrEmpty(tbPreReq.Text))
                oSoftware.PreRequisites = tbPreReq.Text.Split(';');


            if (imgIcon.Tag != null)
                oSoftware.Image = imgIcon.Tag as byte[];

            oSoftware.Category = tbCategories.Text.Trim();

            oSoftware.Shortname = tbShortname.Text.Trim();

            if (RZRestAPI.UploadSWEntry(oSoftware))
                btUpload.IsEnabled = false;
        }

        private void btLoadIcon_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();

            // Set filter for file extension and default file extension 
            dlg.Filter = "All files (*.*)|*.*";


            // Display OpenFileDialog by calling ShowDialog method 
            Nullable<bool> result = dlg.ShowDialog();


            // Get the selected file name and display in a TextBox 
            if (result == true)
            {
                try
                {
                    // Open document 
                    string sImgfilename = dlg.FileName;
                    byte[] data;
                    if (sImgfilename.EndsWith(".exe", StringComparison.CurrentCultureIgnoreCase))
                        data = imageToByteArray(RZScan.GetImageFromExe(sImgfilename));
                    else
                        data = imageToByteArray(System.Drawing.Image.FromFile(sImgfilename));

                    imgIcon.Tag = data;
                    //var bitmap = new BitmapImage(new Uri(sImgfilename, UriKind.Absolute));
                    imgIcon.Source = ByteToImage(data);
                }
                catch { }
            }
        }

        public byte[] imageToByteArray(System.Drawing.Image imageIn)
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
            catch { return null; }
        }

        private void dgSourceFiles_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Commit)
            {
                if (e.Column.Header.ToString() == "URL")
                {
                    if (!string.IsNullOrEmpty(((TextBox)e.EditingElement).Text))
                    {
                        // Create OpenFileDialog 
                        Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();

                        // Set filter for file extension and default file extension 
                        dlg.DefaultExt = "*.*";
                        dlg.Filter = "All|*.*";


                        // Display OpenFileDialog by calling ShowDialog method 
                        Nullable<bool> result = dlg.ShowDialog();

                        if (result == true)
                        {
                            try
                            {
                                string sOldFileName = "";
                                string sOldFileHash = "";
                                sOldFileName = ((contentFiles)e.Row.Item).FileName ?? "setup.exe";
                                sOldFileHash = ((contentFiles)e.Row.Item).FileHash;
                                ((contentFiles)e.Row.Item).URL = ((TextBox)e.EditingElement).Text;
                                ((contentFiles)e.Row.Item).FileName = dlg.SafeFileName;

                                //Try to get File Signature...
                                try
                                {
                                    var Cert = X509Certificate.CreateFromSignedFile(dlg.FileName);

                                    ((contentFiles)e.Row.Item).FileHash = Cert.GetCertHashString().ToLower().Replace(" ", "");
                                    ((contentFiles)e.Row.Item).HashType = "X509";
                                }
                                catch
                                {
                                    ((contentFiles)e.Row.Item).FileHash = GetMD5Hash(dlg.FileName);
                                    ((contentFiles)e.Row.Item).HashType = "MD5";
                                }


                                if (sOldFileName != ((contentFiles)e.Row.Item).FileName)
                                {
                                    tbPSInstall.Text = tbPSInstall.Text.Replace(sOldFileName, ((contentFiles)e.Row.Item).FileName);
                                    tbContentId.Text = Guid.NewGuid().ToString();
                                }

                                if (sOldFileHash != ((contentFiles)e.Row.Item).FileHash)
                                {
                                    tbContentId.Text = Guid.NewGuid().ToString();
                                }

                                var oData = ((List<contentFiles>)dgSourceFiles.ItemsSource).ToList(); ;
                                dgSourceFiles.ItemsSource = null;
                                dgSourceFiles.ItemsSource = oData;
                            }
                            catch { }
                        }

                        e.Row.Background = null;
                        btUpload.IsEnabled = true;
                    }
                    else
                    {
                        btUpload.IsEnabled = false;
                        e.Row.Background = Brushes.LightSalmon;
                    }
                }

                if (e.Column.Header.ToString() == "FileName")
                {
                    string sOldFileName = ((contentFiles)e.Row.Item).FileName ?? "setup.exe";

                    if (sOldFileName != ((TextBox)e.EditingElement).Text)
                    {
                        tbPSInstall.Text = tbPSInstall.Text.Replace(sOldFileName, ((TextBox)e.EditingElement).Text);
                        tbContentId.Text = Guid.NewGuid().ToString();
                    }
                }
                if (e.Column.Header.ToString() == "FileHash")
                {
                    string sOldFileHash = ((contentFiles)e.Row.Item).FileHash;

                    if (sOldFileHash != ((TextBox)e.EditingElement).Text)
                    {
                        tbContentId.Text = Guid.NewGuid().ToString();
                    }
                }

                if (e.Column.Header.ToString() == "HashType")
                {
                    if(string.IsNullOrEmpty(((TextBox)e.EditingElement).Text))
                        ((TextBox)e.EditingElement).Text = "MD5";
                }
            }
        }


        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (this.ActualHeight > 640)
            {
                this.Height.ToString();
                rPreInstall.Height = new GridLength(48, GridUnitType.Auto);
                rPostInstall.Height = new GridLength(48, GridUnitType.Auto);
                rArch.Height = new GridLength(24, GridUnitType.Pixel);
                rPreReq.Height = new GridLength(24, GridUnitType.Pixel);
                tbPSPreInstall.IsEnabled = true;
                tbPSPostInstall.IsEnabled = true;
            }
            else
            {
                rPreInstall.Height = new GridLength(0);
                rPostInstall.Height = new GridLength(0);
                rArch.Height = new GridLength(0);
                rPreReq.Height = new GridLength(0);
                tbPSPreInstall.IsEnabled = false;
                tbPSPostInstall.IsEnabled = false;
            }
        }

        private void btSaveAsXML_Click(object sender, RoutedEventArgs e)
        {
            string sFilename = "EXP_" + tbProductName.Text + tbVersion.Text + tbArchitecture.Text.Trim() + ".json";
            var ofd = new Microsoft.Win32.SaveFileDialog() { Filter = "JSON Files (*.json)|*.json" };
            ofd.FileName = sFilename;
            var result = ofd.ShowDialog();
            if (result != false)
            {
                SaveAsJSON(ofd.FileName);
            }
        }

        private void SaveAsJSON(string sFile)
        {
            AddSoftware oSoftware = new AddSoftware();
            oSoftware.Architecture = tbArchitecture.Text.Trim();
            oSoftware.ContentID = tbContentId.Text.Trim();
            oSoftware.Description = tbDescription.Text;
            try
            {
                oSoftware.Files = ((List<contentFiles>)dgSourceFiles.ItemsSource);
                foreach (contentFiles oFile in oSoftware.Files)
                {
                    if (string.IsNullOrEmpty(oFile.HashType))
                        oFile.HashType = "MD5";
                }
            }
            catch { }
            try
            {
                oSoftware.Files = ((List<contentFiles>)dgSourceFiles.ItemsSource);
            }
            catch { }
            oSoftware.Manufacturer = tbManufacturer.Text.Trim();
            oSoftware.MSIProductID = tbMSIId.Text;
            oSoftware.ProductName = tbProductName.Text.Trim();
            oSoftware.ProductVersion = tbVersion.Text.Trim();
            oSoftware.PSDetection = tbPSDetection.Text;
            oSoftware.PSInstall = tbPSInstall.Text;
            oSoftware.PSPreReq = tbPSPrereq.Text;
            oSoftware.PSUninstall = tbPSUnInstall.Text;
            oSoftware.ProductURL = tbProductURL.Text.Trim();
            oSoftware.Author = Properties.Settings.Default.UserKey;
            oSoftware.PSPreInstall = tbPSPreInstall.Text;
            oSoftware.PSPostInstall = tbPSPostInstall.Text;
            oSoftware.PreRequisites = tbPreReq.Text.Split(';');
            oSoftware.Shortname = tbShortname.Text.Trim();
            oSoftware.Category = tbCategories.Text.Trim();

            if (imgIcon.Tag != null)
                oSoftware.Image = imgIcon.Tag as byte[];

            //Convert to JSON
            JavaScriptSerializer ser = new JavaScriptSerializer();
            string sJson = ser.Serialize(oSoftware);
            File.WriteAllText(sFile, sJson);
        }

        private void btOpenXML_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new Microsoft.Win32.OpenFileDialog() { Filter = "RuckZuck Files|*.xml;*.json" };
            ofd.FileName = "";
            var result = ofd.ShowDialog();
            if (result != false)
            {
                RZUpdater oUpd = new RZUpdater(ofd.FileName);
                OpenXML(oUpd.SoftwareUpdate.SW);
            }
        }

        public void OpenXML(AddSoftware oSoftware)
        {
            tbArchitecture.Text = oSoftware.Architecture.Trim();
            tbContentId.Text = oSoftware.ContentID;

            if (string.IsNullOrEmpty(tbContentId.Text))
                tbContentId.Text = Guid.NewGuid().ToString();

            tbDescription.Text = oSoftware.Description;

            if (oSoftware.Files != null)
            {
                foreach (contentFiles vFiles in oSoftware.Files)
                {
                    if (string.IsNullOrEmpty(vFiles.HashType))
                        vFiles.HashType = "MD5";
                }

                dgSourceFiles.ItemsSource = oSoftware.Files;
            }
            else
            {
                dgSourceFiles.ItemsSource = null;
                dgSourceFiles.Items.Clear();
            }

            tbManufacturer.Text = oSoftware.Manufacturer;
            tbMSIId.Text = oSoftware.MSIProductID;
            tbProductName.Text = oSoftware.ProductName;
            tbVersion.Text = oSoftware.ProductVersion;
            tbPSDetection.Text = oSoftware.PSDetection;
            tbPSInstall.Text = oSoftware.PSInstall;
            tbPSPrereq.Text = oSoftware.PSPreReq;
            tbPSUnInstall.Text = oSoftware.PSUninstall;
            tbProductURL.Text = oSoftware.ProductURL;
            tbPSPostInstall.Text = oSoftware.PSPostInstall;
            tbPSPreInstall.Text = oSoftware.PSPreInstall;
            if (oSoftware.PreRequisites != null)
                tbPreReq.Text = string.Join(";", oSoftware.PreRequisites) ?? "";
            else
                tbPreReq.Text = "";
            try
            {
                imgIcon.Tag = oSoftware.Image;
                imgIcon.Source = ByteToImage(oSoftware.Image);
            }
            catch { }

            tbCategories.Text = oSoftware.Category ?? "";
            tbShortname.Text = oSoftware.Shortname ?? "";
        }

        private void tbContentId_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            tbContentId.Text = Guid.NewGuid().ToString();
        }

        private void tbContentId_KeyDown(object sender, KeyEventArgs e)
        {
            if(e.Key == Key.F2)
            {
                tbContentId.IsReadOnly = false;
            }
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            tbArchitecture.Text = "X86";
            
            tbPSDetection.Text = Regex.Replace(tbPSDetection.Text, @"WOW6432NODE\\", "", RegexOptions.IgnoreCase); //tbPSDetection.Text.ToLower().Replace(@"wow6432node\", "");
            tbPSDetection.Text = Regex.Replace(tbPSDetection.Text, "ProgramFiles(x86)", "ProgramFiles", RegexOptions.IgnoreCase); //tbPSDetection.Text.ToLower().Replace(@"programfiles(x86)", "ProgramFiles");
            tbPSPrereq.Text = Regex.Replace(tbPSPrereq.Text, @"WOW6432NODE\\", "", RegexOptions.IgnoreCase);  //tbPSPrereq.Text.ToLower().Replace(@"wow6432node\", "");
            tbPSPrereq.Text = Regex.Replace(tbPSPrereq.Text, "ProgramFiles(x86)", "ProgramFiles", RegexOptions.IgnoreCase);  //tbPSPrereq.Text.ToLower().Replace(@"programfiles(x86)", "ProgramFiles");
            tbPSInstall.Text = Regex.Replace(tbPSInstall.Text, "ProgramFiles(x86)", "ProgramFiles", RegexOptions.IgnoreCase); //tbPSInstall.Text.ToLower().Replace(@"programfiles(x86)", "ProgramFiles");
            tbPSInstall.Text = Regex.Replace(tbPSInstall.Text, @"WOW6432NODE\\", "", RegexOptions.IgnoreCase); //tbPSInstall.Text.ToLower().Replace(@"wow6432node\", "");
            tbPSUnInstall.Text = Regex.Replace(tbPSUnInstall.Text, @"WOW6432NODE\\", "", RegexOptions.IgnoreCase); //tbPSUnInstall.Text.ToLower().Replace(@"wow6432node\", "");
            tbPSUnInstall.Text = Regex.Replace(tbPSUnInstall.Text, "ProgramFiles(x86)", "ProgramFiles", RegexOptions.IgnoreCase); //tbPSUnInstall.Text.ToLower().Replace(@"programfiles(x86)", "ProgramFiles");
            tbPSPostInstall.Text = Regex.Replace(tbPSPostInstall.Text, @"WOW6432NODE\\", "", RegexOptions.IgnoreCase); //tbPSPostInstall.Text.ToLower().Replace(@"wow6432node\", "");
            tbPSPostInstall.Text = Regex.Replace(tbPSPostInstall.Text, "ProgramFiles(x86)", "ProgramFiles", RegexOptions.IgnoreCase); //tbPSPostInstall.Text.ToLower().Replace(@"programfiles(x86)", "ProgramFiles");

            if(tbPSPrereq.Text.StartsWith("[Environment]::Is64BitProcess", StringComparison.InvariantCultureIgnoreCase))
            {
                tbPSPrereq.Text = "!" + tbPSPrereq.Text;
            }
            btUpload.IsEnabled = true;

        }

        private void MenuItem_Click_1(object sender, RoutedEventArgs e)
        {
            try
            {
                List<GetSoftware> oSW = RZRestAPI.SWResults(tbProductName.Text).ToList();
                if (oSW.FirstOrDefault() != null)
                {
                    tbProductURL.Text = oSW.FirstOrDefault().ProductURL;
                    tbDescription.Text = oSW.FirstOrDefault().Description;
                }

                btUpload.IsEnabled = true;
            }
            catch { }
        }


        private void tbVersion_GotFocus(object sender, RoutedEventArgs e)
        {
            sOldVersion = tbVersion.Text;
        }

        private void tbVersion_LostFocus(object sender, RoutedEventArgs e)
        {
            //Replace the Version in the PSDetctionScript
            if (tbVersion.Text != sOldVersion)
            {
                tbPSDetection.Text = tbPSDetection.Text.Replace("'" + sOldVersion + "'", "'" + tbVersion.Text + "'");
            }
        }


        private void btCreateEXE_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string sTempFile = Path.Combine(Environment.ExpandEnvironmentVariables("%TEMP%"), Path.GetRandomFileName());
                SaveAsJSON(sTempFile);

                string jSW = File.ReadAllText(sTempFile);
                File.Delete(sTempFile);

                CreateExe oExe = new CreateExe(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, tbProductName.Text + "_" + tbVersion.Text + "_" + tbArchitecture.Text + "_setup.exe"));
                if (imgIcon.Tag != null)
                    oExe.Icon = imgIcon.Tag as byte[];
                oExe.Sources.Add(Properties.Resources.Source.Replace("RZRZRZ", tbProductName.Text));
                oExe.Sources.Add(Properties.Resources.RZUpdate);
                oExe.Sources.Add(Properties.Resources.RZRestApi);
                oExe.Sources.Add(Properties.Resources.Assembly.Replace("RZRZRZ", tbProductName.Text).Replace("[assembly: AssemblyFileVersion(\"1.0.0.0\")]", "[assembly: AssemblyFileVersion(\"" + tbVersion.Text + "\")]"));

                System.Resources.ResourceWriter writer = new System.Resources.ResourceWriter("Resources.resx");
                writer.AddResource("SW.json", jSW);
                writer.Generate();
                writer.Close();
                oExe.cp.EmbeddedResources.Add("Resources.resx");

                if (!oExe.Compile())
                {
                    //MessageBox.Show("Failed to create .Exe", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }

            }
            catch { }
        }

        private void btTest_Click(object sender, RoutedEventArgs e)
        {
            string sTempFile = Path.Combine(Environment.ExpandEnvironmentVariables("%TEMP%"), Path.GetRandomFileName());
            SaveAsJSON(sTempFile);

            if (!AttachConsole(-1))  // Attach to a parent process console
                AllocConsole(); // Alloc a new console if none available

            Thread thread = new Thread(() =>
            {
                Console.Clear();
                RZUpdater oRZSW = new RZUpdater(sTempFile);
                oRZSW.SoftwareUpdate.ProgressDetails += SoftwareUpdate_ProgressDetails;
                Console.WriteLine(oRZSW.SoftwareUpdate.SW.ProductName + " " + oRZSW.SoftwareUpdate.SW.ProductVersion + " :");

                Console.WriteLine("Downloading...");

                if (oRZSW.SoftwareUpdate.Download().Result)
                {
                    Console.WriteLine("Installing...");
                    if (oRZSW.SoftwareUpdate.Install(false, true).Result)
                    {
                        Console.WriteLine("done.");
                    }
                    else
                    {
                        Console.WriteLine("Error: The installation failed.");
                    }
                }

                try
                {
                    File.Delete(sTempFile);
                }
                catch { }

                oRZSW.SoftwareUpdate.ProgressDetails -= SoftwareUpdate_ProgressDetails;

            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

        }

        private void SoftwareUpdate_ProgressDetails(object sender, EventArgs e)
        {
            int iPos = Console.CursorTop;
            if (sender.GetType() == typeof(DLStatus))
            {
                Console.WriteLine("Downloaded: " + ((DLStatus)sender).PercentDownloaded.ToString() + "%");
            }

            if (sender.GetType() == typeof(DLTask))
            {
                if(((DLTask)sender).Downloading)
                    Console.WriteLine("Downloaded: " + ((DLTask)sender).PercentDownloaded.ToString() + "%");
            }
        }


        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool AllocConsole();

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        static extern bool AttachConsole(int dwProcessId);

    }



    /// <summary>
    /// MSInstaller (MSI) Main Class
    /// </summary>
    public class MSInstaller : IDisposable
    {
        #region Internal

        internal static string sMSIPath;
        internal static Database msiDatabase;
        internal static Installer msiInstaller;

        #endregion

        /// <summary>
        /// MSInstaller (MSI) Constructor
        /// </summary>
        /// <param name="path">Path to WindowsInstaller (.MSI) File</param>
        public MSInstaller(string path)
        {
            sMSIPath = path;
            Type classType = Type.GetTypeFromProgID("WindowsInstaller.Installer");
            Object installerClassObject = Activator.CreateInstance(classType);
            msiInstaller = (Installer)installerClassObject;
            msiDatabase = msiInstaller.OpenDatabase(sMSIPath, WindowsInstaller.MsiOpenDatabaseMode.msiOpenDatabaseModeReadOnly);
        }

        /// <summary>
        /// Destructor to cleanup the MSInstaller Class
        /// </summary>
        public void Dispose()
        {
            try
            {
                Marshal.FinalReleaseComObject(msiDatabase);
                Marshal.FinalReleaseComObject(msiInstaller);
            }
            catch { }
            msiDatabase = null;
            msiInstaller = null;
            GC.Collect();
        }

        #region MSI Functions

        /// <summary>
        /// Get an MSI Property
        /// </summary>
        /// <param name="sProperty">MSI Property Name</param>
        /// <returns>Value of an MSI Property</returns>
        public string Property(string sProperty)
        {
            try
            {
                WindowsInstaller.View vMSI = msiDatabase.OpenView("SELECT * FROM Property");
                vMSI.Execute(null);
                Record iRec = vMSI.Fetch();
                while (iRec != null)
                {
                    if (string.Compare(iRec.get_StringData(1), sProperty, true) == 0)
                        return iRec.get_StringData(2);

                    iRec = vMSI.Fetch();
                }

                return "";
            }
            catch (Exception ex)
            {
                return "";
            }
            finally
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        /// <summary>
        /// Get the MSI Athor Name from MSI Summary_Information
        /// </summary>
        public string MSIAuthor
        {
            get
            {
                try
                {
                    SummaryInfo SI = msiInstaller.get_SummaryInformation(sMSIPath, 0);
                    return SI.get_Property(4).ToString();
                }
                catch (Exception ex)
                {
                    return "";
                }
            }
        }

        /// <summary>
        /// Get the MSI Architecture/Platform (Intel; AMDx64; x64, Intel64)
        /// </summary>
        public string MSIArchitecture
        {
            get
            {
                try
                {
                    SummaryInfo SI = msiInstaller.get_SummaryInformation(sMSIPath, 0);
                    return SI.get_Property(7).ToString().Split(';')[0];
                }
                catch (Exception ex)
                {
                    return "";
                }
            }
        }

        #endregion

        #region General Functions

        /// <summary>
        /// Generate a random 6 charater string to be used as a MIF Filename
        /// </summary>
        /// <returns>Random 6 charcter string</returns>
        public static string NewRandomMIFName()
        {
            return RandomString(6, true);
        }

        /// <summary>
        /// Generate a random string
        /// </summary>
        /// <param name="size">length of the string</param>
        /// <param name="lowerCase">use only lowercase characters</param>
        /// <returns></returns>
        private static string RandomString(int size, bool lowerCase)
        {
            StringBuilder builder = new StringBuilder();
            Random random = new Random();
            char ch;
            for (int i = 0; i < size; i++)
            {
                ch = Convert.ToChar(Convert.ToInt32(Math.Floor(26 * random.NextDouble() + 65)));
                builder.Append(ch);
            }
            if (lowerCase)
                return builder.ToString().ToLower(CultureInfo.CurrentCulture);
            return builder.ToString();
        }

        /// <summary>
        /// Get the full DirectoryPath of the MSI File
        /// </summary>
        public string DirectoryPath
        {
            get
            {
                FileInfo FI = new FileInfo(sMSIPath);
                return FI.DirectoryName;
            }
        }

        /// <summary>
        /// Get the FileName of the MSI
        /// </summary>
        public string FileName
        {
            get
            {
                FileInfo FI = new FileInfo(sMSIPath);
                return FI.Name;
            }
        }

        /// <summary>
        /// Get the Size of the Package Folder where the MSI is stored.
        /// </summary>
        /// <returns>Size including unit (KB or MB)</returns>
        public string getPkgSize()
        {
            try
            {
                DirectoryInfo dInfo = new DirectoryInfo(this.DirectoryPath);
                FileInfo[] aFiles = dInfo.GetFiles("*.*", SearchOption.AllDirectories);
                decimal iSize = 0;
                foreach (FileInfo iFile in aFiles)
                {
                    iSize = iSize + iFile.Length;
                }
                iSize = iSize / 1024;
                if (iSize < 1024)
                {
                    iSize = Math.Round(iSize);
                    return iSize.ToString(CultureInfo.CurrentCulture) + " KB";
                }
                else
                {
                    iSize = iSize / 1024;
                    iSize = Math.Round(iSize, 0);
                    return iSize.ToString(CultureInfo.CurrentCulture) + " MB";
                }
            }
            catch (Exception ex)
            {
                return "";
            }
        }

        #endregion

    }

}
