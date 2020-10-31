using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json.Linq;
using RZ.Server.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;


namespace RZ.Server
{
    public static class Base
    {
        public static IMemoryCache _cache;
        public static string localURL = "";

        public static bool Approve(string Software, string customerid = "")
        {
            bool bResult = true;
            try
            {
                foreach (var item in Plugins._SoftwarePlugins.OrderBy(t => t.Key))
                {
                    try
                    {
                        return item.Value.Approve(Software);
                    }
                    catch { bResult = false; }
                }
            }
            catch { }

            return bResult;
        }

        public static string clean(string filename)
        {
            if (string.IsNullOrEmpty(filename))
                return "";

            return (string.Join("", filename.Split(Path.GetInvalidFileNameChars()))).Trim().TrimEnd('.');
        }

        public static bool Decline(string Software, string customerid = "")
        {
            bool bResult = true;
            try
            {
                foreach (var item in Plugins._SoftwarePlugins.OrderBy(t => t.Key))
                {
                    try
                    {
                        return item.Value.Decline(Software);
                    }
                    catch { bResult = false; }
                }
            }
            catch { }

            return bResult;
        }

        public static JArray GetCatalog(string customerid = "", bool nocache = false)
        {
            JArray jResult = new JArray();

            if (customerid == "V1")
            {
                if (!nocache) //skip cache ?!
                {
                    //Try to get value from Memory
                    if (_cache.TryGetValue("swcatv1", out jResult))
                    {
                        return jResult;
                    }

                    jResult = new JArray();
                }

                try
                {
                    foreach (var item in Plugins._CatalogPlugins.OrderBy(t => t.Key))
                    {
                        try
                        {
                            jResult.Merge(item.Value.GetCatalog(customerid, nocache), new JsonMergeSettings
                            {
                                MergeArrayHandling = MergeArrayHandling.Union
                            });
                        }
                        catch { }
                    }

                    List<string> lShortNames = new List<string>() { "RuckZuck", "RuckZuck provider for OneGet", "RuckZuck for Configuration Manager", "SCCMCliCtr", "OneGet" };
                    foreach (var oCatItem in jResult.ToArray())
                    {
                        if (!lShortNames.Contains(oCatItem["ShortName"].ToString()))
                        {
                            jResult.Remove(oCatItem);
                        }
                    }

                    var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(30)); //cache catalog for 30 Minutes
                    _cache.Set("swcatv1", jResult, cacheEntryOptions);

                    return jResult;
                }
                catch { }
            }


            if (!nocache) //skip cache ?!
            {
                //Try to get value from Memory
                if (_cache.TryGetValue("swcat" + customerid, out jResult))
                {
                    return jResult;
                }

                jResult = new JArray();
            }

            try
            {
                foreach (var item in Plugins._CatalogPlugins.OrderBy(t => t.Key))
                {
                    try
                    {
                        var oNewCat = item.Value.GetCatalog(customerid, nocache);
                        foreach (JObject oNewItem in oNewCat)
                        {
                            if (jResult.FirstOrDefault(t => t["ShortName"].Value<string>() == oNewItem["ShortName"].Value<string>()) == null)
                            {
                                jResult.Add(oNewItem); //Only add non existing Items
                            }
                        }
                        //jResult.Merge(oNewCat, new JsonMergeSettings
                        //{
                        //    MergeArrayHandling = MergeArrayHandling.Union
                        //});
                    }
                    catch { }
                }

                //Cleanup Items (this allows local JSON Files to remove an Item in the Catalog)
                foreach (var oItem in jResult.ToList())
                {
                    if (string.IsNullOrEmpty(oItem["ProductName"].Value<string>()) && string.IsNullOrEmpty(oItem["Manufacturer"].Value<string>()) && string.IsNullOrEmpty(oItem["ProductVersion"].Value<string>()))
                        oItem.Remove();
                }

                var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(30)); //cache catalog for 30 Minutes
                _cache.Set("swcat" + customerid, jResult, cacheEntryOptions);

                return jResult;
            }
            catch { }

            return null;
        }

        public static async Task<IActionResult> GetFile(string FilePath, string customerid = "")
        {
            try
            {
                foreach (var item in Plugins._SoftwarePlugins.OrderBy(t => t.Key))
                {
                    try
                    {
                        var oRes = await item.Value.GetFile(FilePath, customerid);

                        if (oRes != null)
                            return oRes;
                    }
                    catch { }
                }
            }
            catch { }

            return null;
        }

        public static async Task<Stream> GetIcon(string shortname, string customerid = "", int size = 0)
        {
            try
            {
                foreach (var item in Plugins._SoftwarePlugins.OrderBy(t => t.Key))
                {
                    try
                    {
                        var oRes = await item.Value.GetIcon(shortname, customerid, size);

                        if (oRes != null)
                            return oRes;
                    }
                    catch { }
                }
            }
            catch { }

            return null;
        }

        public static async Task<Stream> GetIcon(Int32 iconid = 0, string iconhash = "", string customerid = "", int size = 0)
        {
            try
            {
                foreach (var item in Plugins._SoftwarePlugins.OrderBy(t => t.Key))
                {
                    try
                    {
                        var oRes = await item.Value.GetIcon(iconid, iconhash, customerid, size);

                        if (oRes != null)
                            return oRes;
                    }
                    catch { }
                }
            }
            catch { }

            return null;
        }

        public static string GetPending(string Software, string customerid = "")
        {
            try
            {
                foreach (var item in Plugins._SoftwarePlugins.OrderBy(t => t.Key))
                {
                    try
                    {
                        return item.Value.GetPending(Software);
                    }
                    catch { }
                }
            }
            catch { }

            return "";
        }

        public static List<string> GetPendingApproval(string customerid = "")
        {
            List<string> bResult = new List<string>(); ;
            try
            {
                foreach (var item in Plugins._SoftwarePlugins.OrderBy(t => t.Key))
                {
                    try
                    {
                        return item.Value.GetPendingApproval();
                    }
                    catch { }
                }
            }
            catch { }

            return bResult;
        }

        public static JArray GetSoftwares(string shortname, string customerid = "")
        {
            try
            {
                foreach (var item in Plugins._SoftwarePlugins.OrderBy(t => t.Key))
                {
                    try
                    {
                        var oRes = item.Value.GetSoftwares(shortname, customerid);
                        if (oRes != null && oRes.Count > 0)
                            return oRes;
                    }
                    catch { }
                }
            }
            catch { }

            return new JArray();
        }

        public static JArray GetSoftwares(string name = "", string ver = "", string man = "_unknown", string customerid = "")
        {
            try
            {
                if (string.IsNullOrEmpty(man))
                    man = "_unknown";

                foreach (var item in Plugins._SoftwarePlugins.OrderBy(t => t.Key))
                {
                    try
                    {
                        var oRes = item.Value.GetSoftwares(name, ver, man, customerid);
                        if (oRes != null && oRes.Count > 0)
                            return oRes;
                    }
                    catch { }
                }
            }
            catch { }

            return new JArray();
        }

        public static string GetURL(string customerid, string clientip)
        {
            try
            {
                foreach (var item in Plugins._CustomerPlugins.OrderBy(t => t.Key))
                {
                    try
                    {
                        return item.Value.GetURL(customerid, clientip);
                    }
                    catch { }
                }
            }
            catch { }

            return "https://ruckzuck.azurewebsites.net";
        }

        public static bool IncCounter(string ShortName = "", string counter = "DL", string customerid = "known")
        {
            try
            {
                foreach (var item in Plugins._SoftwarePlugins.OrderBy(t => t.Key))
                {
                    try
                    {
                        return item.Value.IncCounter(ShortName, counter, customerid);
                    }
                    catch { }
                }
            }
            catch { }

            return false;
        }

        public static bool ResetMemoryCache()
        {
            try
            {
                _cache.Dispose();
                _cache = new MemoryCache(new MemoryCacheOptions());
                return true;
            }
            catch { }
            return false;
        }

        public static void SendNotification(string message = "", string body = "", string customerid = "")
        {
            try
            {
                foreach (var item in Plugins._FeedbackPlugins.OrderBy(t => t.Key))
                {
                    try
                    {
                        item.Value.SendNotification(message, body);
                    }
                    catch { }
                }
            }
            catch { }

            return;
        }

        public static void SetValidIP(string clientip, string customerid = "")
        {
            if (string.IsNullOrEmpty(clientip))
                return;
            Base._cache.Set(clientip, "ip", new TimeSpan(0, 60, 0));
            return;
        }

        public static void StoreFeedback(string name = "", string ver = "", string man = "", string shortname = "", string feedback = "", string user = "", bool? failure = null, string ip = "", string customerid = "")
        {
            try
            {
                foreach (var item in Plugins._FeedbackPlugins.OrderBy(t => t.Key))
                {
                    try
                    {
                        item.Value.StoreFeedback(name, ver, man, shortname, feedback, user, failure, ip, customerid);
                    }
                    catch { }
                }
            }
            catch { }

            return;
        }

        public static Version TrimVersion(Version ProductVersion)
        {
            int major = ProductVersion.Major;
            int minor = ProductVersion.Minor;
            int build = ProductVersion.Build;
            int rev = ProductVersion.Revision;

            if (major == -1)
                major = 0;
            if (minor == -1)
                minor = 0;
            if (build == -1)
                build = 0;
            if (rev == -1)
                rev = 0;

            return new Version(major, minor, build, rev);
        }

        public static bool UploadSoftware(JArray Software, string customerid = "")
        {
            bool bResult = true;
            try
            {
                foreach (var item in Plugins._SoftwarePlugins.OrderBy(t => t.Key))
                {
                    try
                    {
                        return item.Value.UploadSoftware(Software);
                    }
                    catch { bResult = false; }
                }
            }
            catch { }

            return bResult;
        }

        public static bool UploadSoftwareWaiting(JArray Software, string customerid = "")
        {
            bool bResult = true;
            try
            {
                foreach (var item in Plugins._SoftwarePlugins.OrderBy(t => t.Key))
                {
                    try
                    {
                        return item.Value.UploadSoftwareWaiting(Software);
                    }
                    catch { bResult = false; }
                }
            }
            catch { }

            return bResult;
        }
        #region SWLookup
        internal static bool? bForward = null;

        public static JArray CheckForUpdates(JArray Softwares, string customerid = "")
        {
            if (Plugins._SWLookupPlugins.Count == 0)
                return new JArray();

            if (bForward == null)
            {
                try
                {
                    foreach (var item in Plugins._SWLookupPlugins.OrderBy(t => t.Key))
                    {
                        try
                        {
                            if (item.Value.Forward)
                                bForward = true;
                        }
                        catch { }
                    }
                }
                catch { bForward = false; }

                if (bForward == null)
                    bForward = false;
            }

            if (bForward == true)
            {
                try
                {
                    foreach (var item in Plugins._SWLookupPlugins.OrderBy(t => t.Key))
                    {
                        try
                        {
                            return item.Value.CheckForUpdates(Softwares, customerid);
                        }
                        catch { }
                    }
                }
                catch { }
            }

            //check if softwares do not et updates..
            JArray jResult = new JArray();
            JArray SWsorted = new JArray(Softwares.OrderBy(obj => (string)obj["ProductName"]));
            string lID = Hash.CalculateMD5HashString(SWsorted.ToString());
            string sOut = "";
            if (_cache.TryGetValue("noupd-" + lID, out sOut))
            {
                return jResult;
            }
            if (_cache.TryGetValue("upd-" + lID, out sOut))
            {
                return JArray.Parse(sOut);
            }

            JArray oCat = GetCatalog("", false);

            foreach (JObject jObj in Softwares)
            {
                bool bFound = false;
                try
                {
                    string manufacturer = Base.clean(jObj["Manufacturer"].ToString().ToLower());
                    string productname = Base.clean(jObj["ProductName"].ToString().ToLower());
                    string productversion = Base.clean(jObj["ProductVersion"].ToString().ToLower());

                    string sID = Hash.CalculateMD5HashString((manufacturer + productname + productversion).Trim());

                    JObject oRes = new JObject();
                    string sRes = "";
                    //Try to get value from Memory
                    if (_cache.TryGetValue("noupd-" + sID, out sRes))
                    {
                        continue;
                    }

                    //Try to get value from Memory
                    if (_cache.TryGetValue("upd-" + sID, out oRes))
                    {
                        jResult.Add(oRes);
                        continue;
                    }

                    string shortname = GetShortname(productname, productversion, manufacturer);

                    #region compare versions
                    if (!string.IsNullOrEmpty(shortname))
                    {
                        try
                        {
                            var jobj = oCat.SelectTokens("[*].ShortName").Where(t => t.ToString().ToLower() == shortname.ToLower());
                            if (jobj.FirstOrDefault() == null)
                            {
                                var cacheEntryOptions2 = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(900)); //cache result for 15min
                                _cache.Set("noupd-" + sID, "no", cacheEntryOptions2);
                                continue;
                            }

                            JObject jSW = jobj.FirstOrDefault().Parent.Parent as JObject;


                            string sRZVersion = jSW["ProductVersion"].ToString();

                            try
                            {
                                if (!string.IsNullOrEmpty(sRZVersion))
                                    if (productversion.ToLower() == sRZVersion.ToLower()) //same version...
                                    {
                                        var cacheEntryOptions2 = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(900)); //cache result for 15min
                                        _cache.Set("noupd-" + sID, "no", cacheEntryOptions2);
                                        continue;
                                    }
                            }
                            catch { }

                            try
                            {
                                productversion = productversion.Replace("_", ".");
                                productversion = productversion.Replace("-", ".");
                                productversion = productversion.TrimStart('v');
                                productversion = productversion.Replace(" ", "");
                                sRZVersion = sRZVersion.Replace("_", ".");
                                sRZVersion = sRZVersion.Replace("-", ".");
                                sRZVersion = sRZVersion.TrimStart('v');
                                sRZVersion = sRZVersion.Replace(" ", "");
                                if (TrimVersion(Version.Parse(productversion)) > TrimVersion(Version.Parse(sRZVersion))) //version is newer
                                {
                                    Base.SetShortname(jSW["ProductName"].Value<string>(), productversion, jSW["Manufacturer"].Value<string>(), jSW["ShortName"].Value<string>());
                                    Base.StoreFeedback(jSW["ProductName"].Value<string>(), productversion, jSW["Manufacturer"].Value<string>(), jSW["ShortName"].Value<string>(), "NEW Version ?!", "Version", true);
                                    var cacheEntryOptions2 = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(900)); //cache result for 15min
                                    _cache.Set("noupd-" + sID, "no", cacheEntryOptions2);
                                    continue;
                                }
                                if (TrimVersion(Version.Parse(productversion)) == TrimVersion(Version.Parse(sRZVersion))) //version is  same
                                {
                                    var cacheEntryOptions2 = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(900)); //cache result for 15min
                                    _cache.Set("noupd-" + sID, "no", cacheEntryOptions2);
                                    continue;
                                }
                            }
                            catch
                            {
                                try
                                {
                                    //If version is string detect all differences as update
                                    if (string.Compare(productversion, sRZVersion, true) == 0)
                                    {
                                        var cacheEntryOptions2 = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(900)); //cache result for 15min
                                        _cache.Set("noupd-" + sID, "no", cacheEntryOptions2);
                                        continue;
                                    } 
                                    else
                                    {
                                        Base.SetShortname(jSW["ProductName"].Value<string>(), productversion, jSW["Manufacturer"].Value<string>(), jSW["ShortName"].Value<string>());
                                        Base.StoreFeedback(jSW["ProductName"].Value<string>(), productversion, jSW["Manufacturer"].Value<string>(), jSW["ShortName"].Value<string>(), "NEW Version ?!", "String", true);
                                    }
                                }
                                catch { }
                            }



                            JObject oCatItem = new JObject();
                            oCatItem.Add("ShortName", jSW["ShortName"]);
                            oCatItem.Add("Description", jSW["Description"]);
                            oCatItem.Add("Manufacturer", jSW["Manufacturer"]);
                            oCatItem.Add("ProductName", jSW["ProductName"]);
                            oCatItem.Add("ProductVersion", jSW["ProductVersion"]);
                            oCatItem.Add("ProductURL", jSW["ProductURL"]);
                            oCatItem.Add("MSIProductID", productversion); //to show the old version in RuckZuck.exe


                            if (jSW["Downloads"] == null)
                                oCatItem.Add("Downloads", 0);
                            else
                                oCatItem.Add("Downloads", jSW["Downloads"]);

                            if (jSW["SWId"] != null)
                            {
                                if (!string.IsNullOrEmpty(jSW["SWId"].ToString()))
                                {
                                    oCatItem.Add("IconId", jSW["SWId"].Value<Int32>()); //for old older Versions only...
                                    oCatItem.Add("SWId", jSW["SWId"].Value<Int32>());
                                }
                            }

                            if (!string.IsNullOrEmpty(jSW["IconHash"].ToString()))
                                oCatItem.Add("IconHash", jSW["IconHash"].ToString());

                            //Cache result
                            var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(930)); //cache result for 15min
                            _cache.Set("upd-" + sID, oCatItem, cacheEntryOptions);

                            jResult.Add(oCatItem);
                            bFound = true;
                            continue;
                        }
                        catch
                        {
                            //var cacheEntryOptions2 = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(900)); //cache result for 15min
                            //_cache.Set("noupd-" + sID, "no", cacheEntryOptions2);
                        }

                        //JArray jItems = GetSoftwares(shortname);
                        //foreach (JObject jSW in jItems)
                        //{

                        //}
                    }
                    else
                    {
                        //var cacheEntryOptions2 = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(900)); //cache result for 15min
                        //_cache.Set("noupd-" + sID, "no", cacheEntryOptions2);

                        if (shortname == null) //if shortname = ""; it's in SWLookup but without a shortname so we dont need to store it...
                        {
                            ThreadPool.QueueUserWorkItem(s =>
                            {
                                //Add Item to Catalog
                                SetShortname(productname, productversion, manufacturer, ""); //No Shortname
                            });
                        }
                    }
                    #endregion

                    if (!bFound)
                    {
                        var cacheEntryOptions2 = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(900)); //cache result for 15min
                        _cache.Set("noupd-" + sID, "no", cacheEntryOptions2);
                    }

                }
                catch (Exception ex)
                {
                    jObj.ToString();
                    Console.WriteLine(ex.Message);
                }
            }

            if (jResult.Count == 0)
            {
                var cacheEntryOptions2 = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(900)); //cache result for 15min
                _cache.Set("noupd-" + lID, "no", cacheEntryOptions2);
            }
            else
            {
                var cacheEntryOptions2 = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(120)); //cache result for 3min
                _cache.Set("upd-" + lID, jResult.ToString(Newtonsoft.Json.Formatting.None), cacheEntryOptions2);
            }


            return jResult;
        }

        public static string GetShortname(string name = "", string ver = "", string man = "", string customerid = "")
        {
            string sResult = "";

            try
            {
                //1st check Catalog
                try
                {
                    JArray oCat = GetCatalog("", false);

                    var jobj = oCat.SelectTokens("[*]").Where(t => t["ProductName"].ToString().ToLower() == name.ToLower() && t["Manufacturer"].ToString().ToLower() == man.ToLower() && t["ProductVersion"].ToString().ToLower() == ver.ToLower());
                    if (jobj.FirstOrDefault() != null)
                        return jobj.FirstOrDefault()["ShortName"].ToString();
                }
                catch { }

                //2nd check old Catalog Items
                foreach (var item in Plugins._SoftwarePlugins.OrderBy(t => t.Key))
                {
                    try
                    {
                        sResult = item.Value.GetShortname(name, ver, man);
                        if (!string.IsNullOrEmpty(sResult))
                            return sResult;
                    }
                    catch { }
                }

                //3rd check in SWLookup Repository
                foreach (var item in Plugins._SWLookupPlugins.OrderBy(t => t.Key))
                {
                    try
                    {
                        sResult = item.Value.GetShortname(name, ver, man);
                        return sResult;
                        //if (!string.IsNullOrEmpty(sResult))
                        //    return sResult;
                    }
                    catch { }
                }


            }
            catch { }

            return null; //Not in SWLookup
        }

        public static bool SetShortname(string name = "", string ver = "", string man = "", string shortname = "", string customerid = "")
        {
            foreach (var item in Plugins._SWLookupPlugins.OrderBy(t => t.Key))
            {
                try
                {
                    return item.Value.SetShortname(name, ver, man, shortname);
                }
                catch { }
            }

            return false;
        }
        public static IEnumerable<string> SWLookupItems(string customerid = "")
        {
            foreach (var item in Plugins._SWLookupPlugins.OrderBy(t => t.Key))
            {
                try
                {
                    return item.Value.SWLookupItems("*.nop");
                }
                catch { }
            }

            return new List<string>();
        }
        #endregion
        public static bool ValidateIP(string clientip, string customerid = "")
        {
            if (string.IsNullOrEmpty(clientip))
                return false;
            return Base._cache.TryGetValue(clientip, out _);
        }

        public static void WriteLog(string Text, string clientip, int EventID = 0, string customerid = "")
        {
            try
            {
                foreach (var item in Plugins._LogPlugins.OrderBy(t => t.Key))
                {
                    try
                    {
                        item.Value.WriteLog(Text, clientip, EventID, customerid);
                    }
                    catch { }
                }
            }
            catch { }

            return;
        }
    }

    public static class GenericPluginLoader<T>
    {
        public static string PluginDirectory;

        public static ICollection<T> LoadPlugins(string path, string prefix)
        {
            List<string> dllFileNames = null;
            PluginDirectory = path;

            if (Directory.Exists(path))
            {
                dllFileNames = Directory.GetFiles(path, prefix + "*.dll").OrderBy(t => t).ToList();

                if (dllFileNames.Count() == 0)
                {
                    dllFileNames = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, prefix + "*.dll").OrderBy(t => t).ToList();
                }

                ICollection<Assembly> assemblies = new List<Assembly>(dllFileNames.Count());
                AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
                foreach (string dllFile in dllFileNames)
                {
                    AssemblyName an = AssemblyName.GetAssemblyName(dllFile);
                    //Assembly assembly = Assembly.Load(an);
                    Assembly assembly = Assembly.LoadFile(dllFile);
                    assemblies.Add(assembly);
                }

                Type pluginType = typeof(T);
                ICollection<Type> pluginTypes = new List<Type>();
                foreach (Assembly assembly in assemblies)
                {
                    try
                    {
                        if (assembly != null)
                        {
                            Type[] types = assembly.GetTypes();

                            foreach (Type type in types)
                            {
                                if (type.IsInterface || type.IsAbstract)
                                {
                                    continue;
                                }
                                else
                                {
                                    if (type.GetInterface(pluginType.FullName) != null)
                                    {
                                        pluginTypes.Add(type);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }

                ICollection<T> plugins = new List<T>(pluginTypes.Count);
                foreach (Type type in pluginTypes)
                {
                    try
                    {
                        T plugin = (T)Activator.CreateInstance(type);
                        plugins.Add(plugin);
                    }
                    catch { }
                }

                return plugins;
            }

            return null;
        }

        private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            try
            {

                var allDlls = new DirectoryInfo(PluginDirectory).GetFiles("*.dll");

                var dll = allDlls.FirstOrDefault(fi => fi.Name == args.Name.Split(',')[0] + ".dll");
                if (dll == null)
                {
                    return null;
                }

                //return Assembly.LoadFrom(dll.FullName);
                return Assembly.LoadFile(dll.FullName);
            }
            catch (Exception ex)
            {
                ex.Message.ToString();
            }

            return null;
        }
    }

    public class Plugins
    {
        internal static Dictionary<string, ICatalog> _CatalogPlugins = new Dictionary<string, ICatalog>();
        internal static Dictionary<string, ICustomer> _CustomerPlugins = new Dictionary<string, ICustomer>();
        internal static Dictionary<string, IFeedback> _FeedbackPlugins = new Dictionary<string, IFeedback>();
        internal static Dictionary<string, ILog> _LogPlugins = new Dictionary<string, ILog>();
        internal static Dictionary<string, ISoftware> _SoftwarePlugins = new Dictionary<string, ISoftware>();
        internal static Dictionary<string, ISWLookup> _SWLookupPlugins = new Dictionary<string, ISWLookup>();
        public static void loadPlugins(string PluginPath = "")
        {
            if (string.IsNullOrEmpty(PluginPath))
                PluginPath = AppDomain.CurrentDomain.BaseDirectory;

            _CatalogPlugins.Clear();
            _SoftwarePlugins.Clear();
            _SWLookupPlugins.Clear();
            _FeedbackPlugins.Clear();
            _LogPlugins.Clear();
            _CustomerPlugins.Clear();

            if (Base._cache != null)
            {
                Base._cache.Dispose(); //Clear Cache...
            }
            Base._cache = new MemoryCache(new MemoryCacheOptions());

            Dictionary<string, string> dSettings = new Dictionary<string, string>();

            //Load config.json
            if (File.Exists(Path.Combine(PluginPath, "config.json")))
            {
                JArray jConfig = JArray.Parse(File.ReadAllText(Path.Combine(PluginPath, "config.json")));
                foreach (JObject jPermission in jConfig)
                {
                    string sName = jPermission["Name"].Value<string>();
                    dSettings.Add(sName + "URL", jPermission["URL"].Value<string>());
                    dSettings.Add(sName + "SAS", jPermission["SAS"].Value<string>().TrimStart('?'));
                }
            }

            ICollection<ICatalog> CATplugins = GenericPluginLoader<ICatalog>.LoadPlugins(PluginPath, "RZ.Plugin.Catalog");
            foreach (var item in CATplugins)
            {
                _CatalogPlugins.Add(item.Name, item);
                Console.WriteLine(item.Name);
                item.Settings = new Dictionary<string, string>();
                item.Settings.Add("wwwPath", Directory.GetParent(PluginPath).FullName);
                item.Init(PluginPath);
                dSettings.ToList().ForEach(x => item.Settings.Add(x.Key, x.Value));
            }

            ICollection<ISoftware> SWplugins = GenericPluginLoader<ISoftware>.LoadPlugins(PluginPath, "RZ.Plugin.Software");
            foreach (var item in SWplugins)
            {
                _SoftwarePlugins.Add(item.Name, item);
                Console.WriteLine(item.Name);
                item.Settings = new Dictionary<string, string>();
                item.Settings.Add("wwwPath", Directory.GetParent(PluginPath).FullName);
                item.Init(PluginPath);
                dSettings.ToList().ForEach(x => item.Settings.Add(x.Key, x.Value));
            }

            ICollection<ISWLookup> Lookupplugins = GenericPluginLoader<ISWLookup>.LoadPlugins(PluginPath, "RZ.Plugin.SWLookup");
            foreach (var item in Lookupplugins)
            {
                _SWLookupPlugins.Add(item.Name, item);
                Console.WriteLine(item.Name);
                item.Settings = new Dictionary<string, string>();
                item.Settings.Add("wwwPath", Directory.GetParent(PluginPath).FullName);
                item.Init(PluginPath);
                dSettings.ToList().ForEach(x => item.Settings.Add(x.Key, x.Value));
            }

            ICollection<IFeedback> Feedbackplugins = GenericPluginLoader<IFeedback>.LoadPlugins(PluginPath, "RZ.Plugin.Feedback");
            foreach (var item in Feedbackplugins)
            {
                _FeedbackPlugins.Add(item.Name, item);
                Console.WriteLine(item.Name);
                item.Settings = new Dictionary<string, string>();
                item.Settings.Add("wwwPath", Directory.GetParent(PluginPath).FullName);
                item.Init(PluginPath);
                dSettings.ToList().ForEach(x => item.Settings.Add(x.Key, x.Value));
            }

            ICollection<ILog> Logplugins = GenericPluginLoader<ILog>.LoadPlugins(PluginPath, "RZ.Plugin.Log");
            foreach (var item in Logplugins)
            {
                _LogPlugins.Add(item.Name, item);
                Console.WriteLine(item.Name);
                item.Settings = new Dictionary<string, string>();
                item.Settings.Add("wwwPath", Directory.GetParent(PluginPath).FullName);
                item.Init(PluginPath);
                dSettings.ToList().ForEach(x => item.Settings.Add(x.Key, x.Value));
            }

            ICollection<ICustomer> Customerplugins = GenericPluginLoader<ICustomer>.LoadPlugins(PluginPath, "RZ.Plugin.Customer");
            foreach (var item in Customerplugins)
            {
                _CustomerPlugins.Add(item.Name, item);
                Console.WriteLine(item.Name);
                item.Settings = new Dictionary<string, string>();
                item.Settings.Add("wwwPath", Directory.GetParent(PluginPath).FullName);
                item.Init(PluginPath);
                dSettings.ToList().ForEach(x => item.Settings.Add(x.Key, x.Value));
            }
        }
    }
}
