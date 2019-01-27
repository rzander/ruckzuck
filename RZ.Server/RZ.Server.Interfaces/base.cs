using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json.Linq;
using RZ.Server.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace RZ.Server
{
    public class Plugins
    {
        internal static Dictionary<string, ICatalog> _CatalogPlugins = new Dictionary<string, ICatalog>();
        internal static Dictionary<string, ISoftware> _SoftwarePlugins = new Dictionary<string, ISoftware>();
        internal static Dictionary<string, ISWLookup> _SWLookupPlugins = new Dictionary<string, ISWLookup>();

        public static void loadPlugins(string PluginPath = "")
        {
            if (string.IsNullOrEmpty(PluginPath))
                PluginPath = AppDomain.CurrentDomain.BaseDirectory;

            _CatalogPlugins.Clear();
            _SoftwarePlugins.Clear();
            _SWLookupPlugins.Clear();
            
            //Check if MemoryCache is initialized
            if (Base._cache == null)
            {
                Base._cache = new MemoryCache(new MemoryCacheOptions());
            }

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

            Console.WriteLine("");
        }
    }

    public static class Base
    {
        public static IMemoryCache _cache;
        public static string localURL = "";

        public static bool UploadSoftware(JArray Software)
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

        public static bool UploadSoftwareWaiting(JArray Software)
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

        public static List<string> GetPendingApproval()
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

        public static bool Approve(string Software)
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

        public static JArray GetSoftwares(string shortname)
        {
            try
            {
                foreach (var item in Plugins._SoftwarePlugins.OrderBy(t => t.Key))
                {
                    try
                    {
                        return item.Value.GetSoftwares(shortname);
                    }
                    catch { }
                }
            }
            catch { }

            return null;
        }

        public static JArray GetSoftwares(string name = "", string ver = "", string man = "_unknown")
        {
            try
            {
                foreach (var item in Plugins._SoftwarePlugins.OrderBy(t => t.Key))
                {
                    try
                    {
                        return item.Value.GetSoftwares(name, ver, man);
                    }
                    catch { }
                }
            }
            catch { }

            return null;
        }

        public static JArray GetCatalog(string customerid = "", bool nocache = false)
        {
            try
            {
                foreach (var item in Plugins._CatalogPlugins.OrderBy(t => t.Key))
                {
                    try
                    {
                        return item.Value.GetCatalog(customerid, nocache);
                    }
                    catch { }
                }
            }
            catch { }

            return null;
        }

        public static async Task<Stream> GetIcon(string shortname)
        {
            try
            {
                foreach (var item in Plugins._SoftwarePlugins.OrderBy(t => t.Key))
                {
                    try
                    {
                        return await item.Value.GetIcon(shortname);
                    }
                    catch { }
                }
            }
            catch { }

            return null;
        }

        public static async Task<Stream> GetIcon(Int32 iconid = 0, string iconhash = "")
        {
            try
            {
                foreach (var item in Plugins._SoftwarePlugins.OrderBy(t => t.Key))
                {
                    try
                    {
                        return await item.Value.GetIcon(iconid, iconhash);
                    }
                    catch { }
                }
            }
            catch { }

            return null;
        }

        public static async Task<Stream> GetFile(string FilePath)
        {
            try
            {
                foreach (var item in Plugins._SoftwarePlugins.OrderBy(t => t.Key))
                {
                    try
                    {
                        return await item.Value.GetFile(FilePath);
                    }
                    catch { }
                }
            }
            catch { }

            return null;
        }


        #region SWLookup
        public static string GetShortname(string name = "", string ver = "", string man = "")
        {
            string sResult = "";

            try
            {
                //1st check Catalog
                try
                {
                    JArray oCat = GetCatalog("", false);

                    var jobj = oCat.SelectTokens("[*]").Where(t => t["ProductName"].ToString().ToLower() == name.ToLower() && t["Manufacturer"].ToString().ToLower() == man.ToLower() && t["ProductVersion"].ToString().ToLower() == ver.ToLower());
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
                        if (!string.IsNullOrEmpty(sResult))
                            return sResult;
                    }
                    catch { }
                }


            }
            catch { }

            return null; //Not it SWLookup
        }

        public static bool SetShortname(string name = "", string ver = "", string man = "", string shortname = "")
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

        public static JArray CheckForUpdates(JArray Softwares)
        {
            JArray jResult = new JArray();

            foreach (JObject jObj in Softwares)
            {
                bool bFound = false;
                try
                {
                    string manufacturer = Base.clean(jObj["Manufacturer"].ToString().ToLower());
                    string productname = Base.clean(jObj["ProductName"].ToString().ToLower());
                    string productversion = Base.clean(jObj["ProductVersion"].ToString().ToLower());

                    string sID = Hash.CalculateMD5HashString((manufacturer+productname+productversion).Trim());

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
                        JArray oCat = GetCatalog("", false);

                        var jobj = oCat.SelectTokens("[*].ShortName").Where(t => t.ToString().ToLower() == shortname.ToLower());
                        if (jobj.FirstOrDefault() == null)
                        {
                            var cacheEntryOptions2 = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(900)); //cache result for 15min
                            _cache.Set("noupd-" + sID, "no", cacheEntryOptions2);
                            continue;
                        }

                        JObject jSW = jobj.FirstOrDefault().Parent.Parent as JObject;

                        try
                        {
                            string sRZVersion = jSW["ProductVersion"].ToString();

                            if (string.IsNullOrEmpty(sRZVersion))
                                if (productversion == sRZVersion) //same version...
                                    continue;

                            try
                            {
                                if (Version.Parse(productversion) >= Version.Parse(sRZVersion)) //version is newer or same
                                    continue;
                            }
                            catch { }

                            JObject oCatItem = new JObject();
                            oCatItem.Add("ShortName", jSW["ShortName"]);
                            oCatItem.Add("Description", jSW["Description"]);
                            oCatItem.Add("Manufacturer", jSW["Manufacturer"]);
                            oCatItem.Add("ProductName", jSW["ProductName"]);
                            oCatItem.Add("ProductVersion", jSW["ProductVersion"]);
                            oCatItem.Add("ProductURL", jSW["ProductURL"]);
                            oCatItem.Add("MSIProductID", productversion); //to show the old version in RuckZuck.exe


                            //JArray jCategories = JArray.FromObject(new string[] { "Other" });
                            //try
                            //{
                            //    if (!string.IsNullOrEmpty(jSW["Category"].ToString()))
                            //        jCategories = JArray.FromObject(jSW["Category"].Value<string>().Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries));
                            //}
                            //catch { }
                            //oCatItem.Add("Categories", jCategories);

                            if(jSW["Downloads"] == null)
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
                            break;
                        }
                        catch { }

                        //JArray jItems = GetSoftwares(shortname);
                        //foreach (JObject jSW in jItems)
                        //{

                        //}
                    }
                    else
                    {
                        if (shortname == null)
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
                    ex.Message.ToString();
                }
            }

            return jResult;
        }

        public static IEnumerable<string> SWLookupItems()
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

        public static string clean(string filename)
        {
            if (string.IsNullOrEmpty(filename))
                return "";

            return (string.Join("", filename.Split(Path.GetInvalidFileNameChars()))).Trim().TrimEnd('.');
        }

        public static bool IncCounter(string ShortName = "", string counter = "DL", string Customer = "known")
        {
            try
            {
                foreach (var item in Plugins._SoftwarePlugins.OrderBy(t => t.Key))
                {
                    try
                    {
                        return item.Value.IncCounter(ShortName, counter, Customer);
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

                ICollection<T> plugins = new List<T>(pluginTypes.Count);
                foreach (Type type in pluginTypes)
                {
                    T plugin = (T)Activator.CreateInstance(type);
                    plugins.Add(plugin);
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
}
