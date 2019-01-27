using Microsoft.AspNetCore.Hosting;
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

        Task<Stream> GetIcon(string shortname);

        Task<Stream> GetIcon(Int32 iconid = 0, string iconhash = "");

        JArray GetSoftwares(string shortname);

        JArray GetSoftwares(string name = "", string ver = "", string man = "");

        string GetShortname(string name = "", string ver = "", string man = ""); 

        //Upload SW without approval
        bool UploadSoftware(JArray Software);

        //Upload SW but wait for approval
        bool UploadSoftwareWaiting(JArray Software);

        //Get list of pending approvals
        List<string> GetPendingApproval();

        //Approve a pending Software
        bool Approve(string Software);

        Task<Stream> GetFile(string FilePath);

        bool IncCounter(string shortname = "", string counter = "", string Customer = "");
    }

    public interface ISWLookup
    {
        void Init(string PluginPath);

        string Name { get; }

        Dictionary<string, string> Settings { get; set; }

        string GetShortname(string name = "", string ver = "", string man = "");

        bool SetShortname(string name = "", string ver = "", string man = "", string shortname = "");

        IEnumerable<string> SWLookupItems(string filter);

    }
}
