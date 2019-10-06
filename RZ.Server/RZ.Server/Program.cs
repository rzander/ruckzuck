using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace RZ.Server
{
    public class Program
    {
        public static void Main(string[] args)
        {
            //CreateWebHostBuilder(args).Build().Run();


            var Server = new UdpClient(int.Parse(Environment.GetEnvironmentVariable("UDPPort") ?? "5001"));

            //Broadcast listener (UDP)
            Task.Run(() =>
            {
                try
                {
                    Console.WriteLine("Starting UDP Listener on Port: " + (Environment.GetEnvironmentVariable("UDPPort") ?? "5001"));
                    while (true)
                    {
                        var ClientEp = new IPEndPoint(IPAddress.Any, 0);
                        var ClientRequestData = Server.Receive(ref ClientEp);
                        var ClientRequest = Encoding.ASCII.GetString(ClientRequestData);

                        Console.WriteLine("Discovery request from {0}...", ClientRequest);
                        string sLocalURL = Base.localURL;

                        if (Environment.GetEnvironmentVariable("localURL") != null)
                            sLocalURL = Environment.GetEnvironmentVariable("localURL");

                        if (string.IsNullOrEmpty(sLocalURL))
                        {
                            string sIP = "localhost";

                            try
                            {
                                foreach (NetworkInterface f in NetworkInterface.GetAllNetworkInterfaces().Where(t => t.OperationalStatus == OperationalStatus.Up))
                                    foreach (GatewayIPAddressInformation d in f.GetIPProperties().GatewayAddresses.Where(t => t.Address.AddressFamily == AddressFamily.InterNetwork))
                                    {
                                        sIP = f.GetIPProperties().UnicastAddresses.Where(t => t.Address.AddressFamily == AddressFamily.InterNetwork).First().Address.ToString();
                                    }
                            }
                            catch { }

                            sLocalURL = "http://" + sIP + ":" + (Environment.GetEnvironmentVariable("WebPort") ?? "5000");
                        }
                        var ResponseData = Encoding.ASCII.GetBytes(sLocalURL);
                        Server.Send(ResponseData, ResponseData.Length, ClientEp);
                    }
                }
                catch(Exception ex)
                {
                    Console.WriteLine("ERROR: UDP Listener - " + ex.Message);
                }
            });

            var host = new WebHostBuilder()
                .UseKestrel(c => c.AddServerHeader = false)
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseIISIntegration()
                .UseStartup<Startup>().ConfigureAppConfiguration((builderContext, config) =>
                {
                    config.AddJsonFile("appsettings.json", optional: false);
                })
                .UseUrls("http://*:" + (Environment.GetEnvironmentVariable("WebPort") ?? "5000"))
                .Build();

            host.Run();




        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>();
    }
}
