using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace RZ.Server.Interfaces
{
    public interface ICatalog
    {
        void Init(string PluginPath);

        string Name { get; }

        Dictionary<string, string> Settings { get; set; }

        JArray GetCatalog(string customerid = "", bool nocache = false);

    }

    public interface ISoftware
    {
        void Init(string PluginPath);

        string Name { get; }

        Dictionary<string, string> Settings { get; set; }

        Task<Stream> GetIcon(string shortname, string customerid = "", int size = 0);

        Task<Stream> GetIcon(Int32 iconid = 0, string iconhash = "", string customerid = "", int size = 0);

        JArray GetSoftwares(string shortname, string customerid = "");

        JArray GetSoftwares(string name = "", string ver = "", string man = "", string customerid = "");

        string GetShortname(string name = "", string ver = "", string man = "", string customerid = ""); 

        //Upload SW without approval
        bool UploadSoftware(JArray Software, string customerid = "");

        //Upload SW but wait for approval
        bool UploadSoftwareWaiting(JArray Software, string customerid = "");

        //Get list of pending approvals
        List<string> GetPendingApproval(string customerid = "");

        //Approve a pending Software
        bool Approve(string Software, string customerid = "");

        //Decline a pending Software
        bool Decline(string Software, string customerid = "");

        //Get JSON of a pending Software
        string GetPending(string Software, string customerid = "");

        Task<IActionResult> GetFile(string FilePath, string customerid = "");

        bool IncCounter(string shortname = "", string counter = "", string customerid = "");
    }

    public interface ISWLookup
    {
        void Init(string PluginPath);

        string Name { get; }

        bool Forward { get;  }

        Dictionary<string, string> Settings { get; set; }

        string GetShortname(string name = "", string ver = "", string man = "", string customerid = "");

        bool SetShortname(string name = "", string ver = "", string man = "", string shortname = "", string customerid = "");

        IEnumerable<string> SWLookupItems(string filter, string customerid = "");

        JArray CheckForUpdates(JArray Softwares, string customerid = "");

    }

    public interface IFeedback
    {
        void Init(string PluginPath);

        string Name { get; }

        Dictionary<string, string> Settings { get; set; }

        Task<bool>  StoreFeedback(string name = "", string ver = "", string man = "", string shortname = "", string feedback = "", string user = "", bool? failure = null, string ip = "", string customerid = "");

        Task<bool> SendNotification(string message = "", string body = "", string customerid = "");
    }

    public interface ILog
    {
        void Init(string PluginPath);

        string Name { get; }

        Dictionary<string, string> Settings { get; set; }

        void WriteLog(string Text, string clientip, int EventId = 0, string customerid = "");
    }

    public interface ICustomer
    {
        void Init(string PluginPath);

        string Name { get; }

        Dictionary<string, string> Settings { get; set; }

        string GetURL(string customerid = "", string ip = "");
    }
}
