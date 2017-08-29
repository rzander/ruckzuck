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

namespace RZWCF
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var Server = new UdpClient(5001);

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
                    .UseUrls("http://*:5000")
                    .Build();
            host.Run();
        }

    }
}
