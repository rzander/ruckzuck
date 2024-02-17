# RuckZuck Base Library for .NET6/8

Library is published on Nuget: https://www.nuget.org/packages/RZ.Base.Lib

Example with SeriLog logger:
```C#
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Serilog;
using Serilog.Extensions.Logging;
namespace RZ.Base.Lib.Test
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .CreateLogger();

            var mslog = new SerilogLoggerFactory(Log.Logger).CreateLogger<RuckZuck>();

            RuckZuck rz = new RuckZuck(logger: mslog);

            string sShortname =  "Teams";
            if(args.Length > 0)
            {
                sShortname = args[0];
            }

            JArray oSW = rz.GetSoftwares(sShortname, rz.Customerid).Result;
            rz.Install(oSW).Wait();
        }
    }
}

```
