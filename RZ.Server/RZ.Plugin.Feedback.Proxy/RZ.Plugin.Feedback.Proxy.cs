using Microsoft.Azure.ServiceBus;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json.Linq;
using RZ.Server;
using RZ.Server.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace RZ.Plugin.Feedback.Azure
{
    public class Plugin_Feedback : IFeedback
    {
        private IMemoryCache _cache;

        public string Name
        {
            get
            {
                return Assembly.GetExecutingAssembly().ManifestModule.Name;
            }
        }

        public Dictionary<string, string> Settings { get; set; }

        public void Init(string PluginPath)
        {
            //Check if MemoryCache is initialized
            if (_cache != null)
            {
                _cache.Dispose();
            }

            _cache = new MemoryCache(new MemoryCacheOptions());

            if (Settings == null)
                Settings = new Dictionary<string, string>();

        }

        public Task<bool> StoreFeedback(string name = "", string ver = "", string man = "", string shortname = "", string feedback = "", string user = "", bool? failure = null)
        {
            var tFeedback = Task.Run(() =>
            {
                string ok = "false";
                if (failure == false)
                    ok = "true";

                if(failure == true)
                {
                    Console.WriteLine("Failure: " + name + " " + ver + "  Error:" + feedback);
                }
                else
                {
                    Console.WriteLine("Success: " + name + " " + ver);
                }
                
                RZRestAPIv2.StoreFeedback(name, ver, man, ok, user, feedback);
                return true;
            });

            return tFeedback;
        }

        public void PushBullet(string Message, string Body)
        {
        }

        public Task<bool> SendNotification(string message = "", string body = "")
        {
            Task<bool> t = Task<bool>.Run(() =>
            {
                PushBullet(message, body);
                return true;
            });

            return t;
        }
    }
}
