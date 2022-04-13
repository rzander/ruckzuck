using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;

namespace RuckZuck_Tool
{
    public class Program
    {
        [STAThreadAttribute]
        public static void Main()
        {
            //Disbale SSL/TLS Errors
            System.Net.ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
            //Disable CRL Check
            System.Net.ServicePointManager.CheckCertificateRevocationList = false;

            //AppDomain.CurrentDomain.AssemblyResolve += OnResolveAssembly;

            App app = new App();

            app.StartupUri = new System.Uri("MainWindow.xaml", System.UriKind.Relative);

            app.Run();
        }

        private static Assembly OnResolveAssembly(object sender, ResolveEventArgs args)
        {
            Assembly executingAssembly = Assembly.GetExecutingAssembly();
            AssemblyName assemblyName = new AssemblyName(args.Name);

            string path = assemblyName.Name + ".dll";
            if (assemblyName.CultureInfo.Equals(CultureInfo.InvariantCulture) == false)
            {
                path = String.Format(@"{0}\{1}", assemblyName.CultureInfo, path);
            }

            using (Stream stream = executingAssembly.GetManifestResourceStream("RuckZuck_Tool." + path))
            {
                if (stream == null)
                    return null;

                byte[] assemblyRawBytes = new byte[stream.Length];
                stream.Read(assemblyRawBytes, 0, assemblyRawBytes.Length);
                return Assembly.Load(assemblyRawBytes);
            }
        }
    }

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
    }
}
