using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Diagnostics;
using System.Threading;

namespace RZWCF
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var Server = new UdpClient(int.Parse(Environment.GetEnvironmentVariable("UDPPort") ?? "5001"));

            if (Environment.GetEnvironmentVariable("UseIPFS") == "1")
            {
                try
                {
                    if (!Directory.Exists("/app/.ipfs"))
                    {
                        var oInit = Process.Start("ipfs", @"init"); //>/dev/null 2>/dev/null
                        oInit.StartInfo.RedirectStandardOutput = false;
                        oInit.WaitForExit(5000);
                        Thread.Sleep(3000);
                    }
                    Process.Start("ipfs", "config --json Experimental.FilestoreEnabled true").WaitForExit();
                    Process.Start("ipfs", "config Addresses.API /ip4/0.0.0.0/tcp/5002").WaitForExit();
                    Process.Start("ipfs", "config Addresses.Gateway /ip4/0.0.0.0/tcp/8080").WaitForExit();
                    //Process.Start("ipfs", "config --json API.HTTPHeaders.Access-Control-Allow-Origin '[\"*\"]'").WaitForExit(); ;
                    //Process.Start("ipfs", "config --json API.HTTPHeaders.Access-Control-Allow-Methods '[\"PUT\",\"GET\",\"POST\"]'").WaitForExit();
                    //Process.Start("ipfs", "add -r --nocopy /app/wwwroot/files").WaitForExit();

                    Thread.Sleep(2000);
                    Process.Start("ipfs", "daemon").WaitForExit(7000);
                    Thread.Sleep(2000);

                    RuckZuck_WCF.RZRestProxy.IPFSAdd("/app/RZCache.dll");

                }
                catch (Exception ex)
                {
                    RuckZuck_WCF.RZRestProxy.RedirectToIPFS = true;
                    RuckZuck_WCF.RZRestProxy.UseIPFS = 0;
                    Console.WriteLine("IPFS ERROR:" + ex.Message);
                }
            }

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("localURL")))
            {
                var ResponseData = Encoding.ASCII.GetBytes(Environment.GetEnvironmentVariable("localURL"));

                Task.Run(() =>
                {
                    while (true)
                    {
                        var ClientEp = new IPEndPoint(IPAddress.Any, 0);
                        var ClientRequestData = Server.Receive(ref ClientEp);
                        var ClientRequest = Encoding.ASCII.GetString(ClientRequestData);
                        
                        Console.WriteLine("Discovery request from {0}...", ClientRequest);
                        Server.Send(ResponseData, ResponseData.Length, ClientEp);
                    }
                });
            }

            var host = new WebHostBuilder()
                    .UseKestrel()
                    .UseContentRoot(Directory.GetCurrentDirectory())
                    .UseIISIntegration()
                    .UseStartup<Startup>()
                    //.UseApplicationInsights()
                    .UseWebRoot("wwwroot")
                    .UseUrls("http://*:" + Environment.GetEnvironmentVariable("WebPort") ?? "5000")
                    .Build();
            host.Run();
        }

    }
}
