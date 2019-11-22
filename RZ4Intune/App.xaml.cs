using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

using System.Reflection;
using System.Globalization;
using System.IO;
using Microsoft.Identity.Client;
using active_directory_wpf_msgraph_v2;

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
        static App()
        {
            _clientApp = PublicClientApplicationBuilder.Create(ClientId)
                .WithAuthority(AzureCloudInstance.AzurePublic, Tenant)
                .Build();
            TokenCacheHelper.EnableSerialization(_clientApp.UserTokenCache);
        }

        // Below are the clientId (Application Id) of your app registration and the tenant information. 
        // You have to replace:
        // - the content of ClientID with the Application Id for your app registration
        // - Te content of Tenant by the information about the accounts allowed to sign-in in your application:
        //   - For Work or School account in your org, use your tenant ID, or domain
        //   - for any Work or School accounts, use organizations
        //   - for any Work or School accounts, or Microsoft personal account, use common
        //   - for Microsoft Personal account, use consumers
        private static string ClientId = "eb5c948f-6f20-4b0a-97d9-f0b15371289a"; // "0b8b0665-bc13-4fdc-bd72-e0227b9fc011";

        // Note: Tenant is important for the quickstart. We'd need to check with Andre/Portal if we
        // want to change to the AadAuthorityAudience.
        private static string Tenant = "common";

        private static IPublicClientApplication _clientApp;

        public static IPublicClientApplication PublicClientApp { get { return _clientApp; } }
    }
}
