using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.ConfigurationManagement.DesiredConfigurationManagement;
using Microsoft.ConfigurationManagement.DesiredConfigurationManagement.ExpressionOperators;
using Microsoft.SystemsManagementServer.DesiredConfigurationManagement.Expressions;
using Microsoft.SystemsManagementServer.DesiredConfigurationManagement.Rules;
using Microsoft.ConfigurationManagement.AdminConsole.AppManFoundation;
using Microsoft.ConfigurationManagement.ApplicationManagement;
using Microsoft.ConfigurationManagement.ManagementProvider;
using Microsoft.ConfigurationManagement.ManagementProvider.WqlQueryEngine;
using System.Data.SqlClient;

using System.IO;
using System.Net;
using System.Management.Automation;

using System.Xml;
using RuckZuck_Tool;
using System.Threading;
using RuckZuck.Base;
using RZUpdate;

namespace RuckZuck_Tool
{
    class CMAPI
    {
        //public RZ4CM.MainWindow.MyTraceListener Listener;
        public CMAPI()
        {
            try
            {
                //if (string.IsNullOrEmpty(RZRestAPI.Token))
                //{
                //    if (string.IsNullOrEmpty(RuckZuck_Tool.Properties.Settings.Default.UserKey))
                //    {
                //        RZRestAPI.GetAuthToken("FreeRZ", GetTimeToken());
                //    }
                //    else
                //    {
                //        RZRestAPI.GetAuthToken(RuckZuck_Tool.Properties.Settings.Default.UserKey, RuckZuck_Tool.Properties.Settings.Default.UserPW);
                //    }
                //}

                if (!string.IsNullOrEmpty(RuckZuck_Tool.Properties.Settings.Default.CM_Server))
                    _CMSiteServer = RuckZuck_Tool.Properties.Settings.Default.CM_Server;

                connectionManager = new WqlConnectionManager();

                try
                {
                    //connectionManager.Connect(_CMSiteServer, "WP01\\roger.zander", "password123$");
                    connectionManager.Connect(_CMSiteServer);

                    CM12SiteCode = connectionManager.NamedValueDictionary["ConnectedSiteCode"].ToString();
                }
                catch (SmsException ex)
                {
                    ex.Message.ToString();
                }




                string sSQL = _CMSiteServer;
                string sSQLDB = "CM_" + CM12SiteCode;

                //get SQL / DB from Config file...
                if (!string.IsNullOrEmpty(RuckZuck_Tool.Properties.Settings.Default.CM_SQLServerDBName))
                    sSQLDB = RuckZuck_Tool.Properties.Settings.Default.CM_SQLServerDBName;
                if (!string.IsNullOrEmpty(RuckZuck_Tool.Properties.Settings.Default.CM_SQLServer))
                    sSQL = RuckZuck_Tool.Properties.Settings.Default.CM_SQLServer;

                //CM12SQLConnectionString = "Data Source=" + sSQL + ";Initial Catalog=" + sSQLDB +"; Persist Security Info = True; User ID = sa; Password = kerb7eros";
                CM12SQLConnectionString = "Data Source=" + sSQL + ";Initial Catalog=" + sSQLDB + ";Integrated Security=True";

                //sADGroup =  Environment.UserDomainName + "\\Domain Users";
                sADGroup = Environment.ExpandEnvironmentVariables(RuckZuck_Tool.Properties.Settings.Default.DefaultADGroup);
            }
            catch (Exception ex)
            {
                System.IO.File.AppendAllLines(Environment.ExpandEnvironmentVariables("%TEMP%\\RZError.txt"), new string[] { DateTime.Now.ToString() + ";CM12Mgmt;E62" + ex.Message });
            }
        }

        internal string _CMSiteServer = "";
        //string sAuthToken = "";
        internal string CM12SQLConnectionString = "";
        string SQLSWIDLookup = ";WITH XMLNAMESPACES ( DEFAULT 'http://schemas.microsoft.com/SystemCenterConfigurationManager/2009/AppMgmtDigest')   SELECT * FROM  ( SELECT  SDMPackageDigest.value('(/AppMgmtDigest/Application/DisplayInfo[1]/Info[1]/Title)[1]', 'nvarchar(MAX)') + ' ' + SDMPackageDigest.value('(/AppMgmtDigest/Application/SoftwareVersion)[1]', 'nvarchar(MAX)') + ' (' + SDMPackageDigest.value('(/AppMgmtDigest/Application/CustomId)[1]', 'nvarchar(MAX)') + ')' as [DisplayName],  SDMPackageDigest.value('(/AppMgmtDigest/Application/DisplayInfo[1]/Info[1]/Title)[1]', 'nvarchar(MAX)') as Name,  SDMPackageDigest.value('(/AppMgmtDigest/Application/SoftwareVersion)[1]', 'nvarchar(MAX)') as [Version],  SDMPackageDigest.value('(/AppMgmtDigest/Application/CustomId)[1]', 'nvarchar(MAX)') [SoftwareID], vCI.CI_ID   FROM v_ConfigurationItems as vCI WHERE CIType_ID = 10 and IsLatest = 1  ) as app WHERE SoftwareID = @SWID";
        //string SQLSRZIDLookup = ";WITH XMLNAMESPACES ( DEFAULT 'http://schemas.microsoft.com/SystemCenterConfigurationManager/2009/AppMgmtDigest')   SELECT * FROM  ( SELECT  SDMPackageDigest.value('(/AppMgmtDigest/Application/DisplayInfo[1]/Info[1]/Title)[1]', 'nvarchar(MAX)') + ' ' + SDMPackageDigest.value('(/AppMgmtDigest/Application/SoftwareVersion)[1]', 'nvarchar(MAX)') + ' (' + SDMPackageDigest.value('(/AppMgmtDigest/Application/CustomId)[1]', 'nvarchar(MAX)') + ')' as [DisplayName],  SDMPackageDigest.value('(/AppMgmtDigest/Application/DisplayInfo[1]/Info[1]/Title)[1]', 'nvarchar(MAX)') as Name,  SDMPackageDigest.value('(/AppMgmtDigest/Application/SoftwareVersion)[1]', 'nvarchar(MAX)') as [Version],  SDMPackageDigest.value('(/AppMgmtDigest/Application/CustomId)[1]', 'nvarchar(MAX)') [SoftwareID], RIGHT(SDMPackageDigest.value('(/AppMgmtDigest/Application/CustomId)[1]', 'nvarchar(MAX)'), LEN(SDMPackageDigest.value('(/AppMgmtDigest/Application/CustomId)[1]', 'nvarchar(MAX)')) - 2) AS RZID, vCI.CI_ID   FROM v_ConfigurationItems as vCI WHERE CIType_ID = 10 and IsLatest = 1  ) as app WHERE SoftwareID like 'RZ%' ORDER BY DisplayName";
        string SQLSRZIDLookup = ";WITH XMLNAMESPACES ( DEFAULT 'http://schemas.microsoft.com/SystemCenterConfigurationManager/2009/AppMgmtDigest')   SELECT RZID, Shortname, Bootstrap, [Version] FROM  ( SELECT  SDMPackageDigest.value('(/AppMgmtDigest/Application/SoftwareVersion)[1]', 'nvarchar(MAX)') as [Version], SDMPackageDigest.value('(/AppMgmtDigest/Application/CustomProperties/Property[@Name=\"Bootstrap\"]/@Value)[1]', 'nvarchar(MAX)') [Bootstrap], SDMPackageDigest.value('(/AppMgmtDigest/Application/CustomProperties/Property[@Name=\"Shortname\"]/@Value)[1]', 'nvarchar(MAX)') [Shortname],  RIGHT(SDMPackageDigest.value('(/AppMgmtDigest/Application/CustomProperties/Property[@Name=\"SWID\"]/@Value)[1]', 'nvarchar(MAX)'), LEN(SDMPackageDigest.value('(/AppMgmtDigest/Application/CustomProperties/Property[@Name=\"SWID\"]/@Value)[1]', 'nvarchar(MAX)')) - 2) AS RZID, vCI.CI_ID FROM v_ConfigurationItems as vCI WHERE CIType_ID = 10 and IsLatest = 1 and LEN(SDMPackageDigest.value('(/AppMgmtDigest/Application/CustomId)[1]', 'nvarchar(MAX)')) > 2  ) as app WHERE RZID is not null";
        internal string NewAppSecurityScopeName = Properties.Settings.Default.CMSecurityScope;
        List<string> AppOSRequirements = new List<string>();
        string GlobalConditionName = "";

        internal string sADGroup = "";
        internal string SQLCollQueryLookup = "SELECT [SiteID] as [CollectionID],[CollectionName] FROM [vCollections] WHERE CollectionComment like @SWID";
        internal string LimitingUserCollectionID = Properties.Settings.Default.LimitingUserCollectionID;
        internal string LimitingDeviceCollectionID = "SMS00001";
        internal string DPGroup = Properties.Settings.Default.DPGroup;
        internal string CollFolder = Properties.Settings.Default.CollectionFolder ?? "RuckZuck";
        internal string CM12SiteCode = "";
        internal string sSourcePath = Properties.Settings.Default.CMContentSourceUNC;
        internal bool bPrimaryUserRequired = Properties.Settings.Default.PrimaryUserRequired;

        internal WqlConnectionManager connectionManager = new WqlConnectionManager();


        private string GetTimeToken()
        {
            byte[] time = BitConverter.GetBytes(DateTime.UtcNow.ToBinary());
            byte[] key = Guid.NewGuid().ToByteArray();
            return Convert.ToBase64String(time.Concat(key).ToArray());
        }

        private async Task<List<GetSoftware>> GetRZSoftware(string ProdName, string ProdVersion, string Manufacturer)
        {
            List<GetSoftware> oResult = new List<GetSoftware>();
            oResult.AddRange((await RZRestAPIv2.GetCatalogAsync()).Where(t => t.ProductName == ProdName && t.Manufacturer == Manufacturer && t.ProductVersion == ProdVersion));

            return oResult;
        }

        private IResultObject getCategory(string CategoryName)
        {
            IResultObject oCategegories = connectionManager.QueryProcessor.ExecuteQuery(string.Format("select * from SMS_CategoryInstance where CategoryTypeName='AppCategories' and LocalizedCategoryInstanceName = '{0}'", CategoryName));

            foreach (IResultObject oCat in oCategegories)
            {
                return oCat;
            }

            return NewCategory(CategoryName);
        }

        private IResultObject NewCategory(string CategoryName)
        {
            try
            {
                IResultObject oLocalized = connectionManager.CreateEmbeddedObjectInstance("SMS_Category_LocalizedProperties");
                oLocalized.Properties["CategoryInstanceName"].StringValue = CategoryName;
                oLocalized.Properties["LocaleID"].IntegerValue = 0;


                IResultObject oCategory = connectionManager.CreateInstance("SMS_CategoryInstance");
                oCategory.Properties["CategoryTypeName"].StringValue = "AppCategories";
                oCategory.Properties["SourceSite"].StringValue = CM12SiteCode;
                oCategory.SetArrayItems("LocalizedInformation", new List<IResultObject>() { oLocalized });

                oCategory.Properties["CategoryInstance_UniqueID"].StringValue = "AppCategories:" + Guid.NewGuid().ToString();
                oCategory.Put();
                oCategory.Get();

                return oCategory;
            }
            catch { }

            return null;
        }

        private IResultObject NewUserCategory(string CategoryName)
        {
            try
            {
                IResultObject oLocalized = connectionManager.CreateEmbeddedObjectInstance("SMS_Category_LocalizedProperties");
                oLocalized.Properties["CategoryInstanceName"].StringValue = CategoryName;
                oLocalized.Properties["LocaleID"].IntegerValue = 0;


                IResultObject oCategory = connectionManager.CreateInstance("SMS_CategoryInstance");
                oCategory.Properties["CategoryTypeName"].StringValue = "CatalogCategories";
                oCategory.Properties["SourceSite"].StringValue = CM12SiteCode;
                oCategory.SetArrayItems("LocalizedInformation", new List<IResultObject>() { oLocalized });

                oCategory.Properties["CategoryInstance_UniqueID"].StringValue = "CatalogCategories:" + Guid.NewGuid().ToString();
                oCategory.Put();
                oCategory.Get();

                return oCategory;
            }
            catch { }

            return null;
        }

        private List<string> categoryIDs(List<string> CategoryNames)
        {
            List<string> lResult = new List<string>();
            if (CategoryNames != null)
            {
                foreach (string sCategory in CategoryNames)
                {
                    try
                    {
                        string sID = getCategory(sCategory).Properties["CategoryInstance_UniqueID"].StringValue;
                        if (!string.IsNullOrEmpty(sID))
                        {
                            lResult.Add(sID);
                        }
                    }
                    catch (Exception ex)
                    {

                        System.IO.File.AppendAllLines(Environment.ExpandEnvironmentVariables("%TEMP%\\RZError.txt"), new string[] { DateTime.Now.ToString() + ";CM12Mgmt;E105" + ex.Message });

                    }

                }
            }

            return lResult;
        }

        private Application createApplication(string title, string description, string language, string version, string Manufacturer, string softwareID, string[] CategoryNames, bool save)
        {
            ApplicationFactory factory = new ApplicationFactory();
            AppManWrapper wrapper = AppManWrapper.Create(connectionManager, factory) as AppManWrapper;
            string sDispName = title;
            Application app = new Application();
            IResultObject rawapp = null;

            //Check if SoftwareId is specified
            if (!string.IsNullOrEmpty(softwareID))
            {
                rawapp = getApplicationFromSWID(softwareID);
            }

            //SoftwareId already exists
            if (rawapp != null)
            {
                //Existing App....
                ApplicationFactory afactory = new ApplicationFactory();
                AppManWrapper awrapper = AppManWrapper.Create(this.connectionManager, afactory) as AppManWrapper;

                //App already exists !
                wrapper = factory.WrapExisting(rawapp);

                app = wrapper.InnerAppManObject as Application;
                app.Title = title + ' ' + version;
            }
            else
            {
                //Create new App
                app = new Application { Title = title + ' ' + version };
            }

            app.DisplayInfo.DefaultLanguage = language;
            AppDisplayInfo AppInfo = new AppDisplayInfo { Title = sDispName, Description = description, Language = language };

            app.DisplayInfo.Add(AppInfo);
            app.AutoInstall = false;  //allow this application to be installed from the Install Application task sequence action
            app.SoftwareVersion = version;
            app.CustomId = softwareID;
            app.AutoDistribute = true;
            app.SendToProtectedDP = true;
            app.DownloadDelta = true;
            app.Publisher = Manufacturer;

            if (CategoryNames != null)
            {
                wrapper.CategoryIds = categoryIDs(CategoryNames.ToList()).ToArray();
            }

            if (save & app.IsChanged)
            {
                wrapper.InnerAppManObject = app;
                factory.PrepareResultObject(wrapper);
                wrapper.InnerResultObject.Put();
            }

            return wrapper.InnerAppManObject as Application;
        }

        private IResultObject getApplicationFromSWID(string SWID)
        {

            SqlConnection sqlConnection = new SqlConnection(CM12SQLConnectionString);
            try
            {
                sqlConnection.Open();

                //Connect SQL and run query to get List of active local admin usernames
                SqlCommand sqlCommand = new SqlCommand(SQLSWIDLookup, sqlConnection);

                //Add paremeter for the SQL Query
                sqlCommand.Parameters.Add(new SqlParameter("SWID", SWID));

                //Execute SQL Command
                SqlDataReader sqlReader = sqlCommand.ExecuteReader(System.Data.CommandBehavior.SingleRow);

                //Loop trough each returning row
                while (sqlReader.Read())
                {
                    try
                    {
                        //Add Username to result list
                        string sCI_ID = sqlReader["CI_ID"].ToString();

                        return getApplicationFromCIID(sCI_ID);
                    }
                    catch { }
                }

                sqlReader.Close();

            }
            catch (Exception ex)
            {
                System.IO.File.AppendAllLines(Environment.ExpandEnvironmentVariables("%TEMP%\\RZError.txt"), new string[] { DateTime.Now.ToString() + ";F239E1" + ex.Message, CM12SQLConnectionString, SWID });

            }
            finally
            {
                sqlConnection.Close();
            }

            return null;
        }

        public List<SQLRZ> getRZIDs()
        {
#if DEBUG
            System.IO.File.AppendAllLines(Environment.ExpandEnvironmentVariables("%TEMP%\\RZDebug.txt"), new string[] { DateTime.Now.ToString() + ";R1;" + "Getting RuckZuck Id's from SQL.." });
#endif
            SqlConnection sqlConnection = new SqlConnection(CM12SQLConnectionString);
            List<SQLRZ> lResult = new List<SQLRZ>();

            try
            {
                sqlConnection.Open();

#if DEBUG
                System.IO.File.AppendAllLines(Environment.ExpandEnvironmentVariables("%TEMP%\\RZDebug.txt"), new string[] { DateTime.Now.ToString() + ";R2;" + "SQL Connected based on ConnectionString: " + CM12SQLConnectionString });
#endif
                //Connect SQL and run query to get List of active local admin usernames
                SqlCommand sqlCommand = new SqlCommand(SQLSRZIDLookup, sqlConnection);

#if DEBUG
                System.IO.File.AppendAllLines(Environment.ExpandEnvironmentVariables("%TEMP%\\RZDebug.txt"), new string[] { DateTime.Now.ToString() + ";R3;" + "running Query: ", SQLSRZIDLookup });
#endif
                //Execute SQL Command
                SqlDataReader sqlReader = sqlCommand.ExecuteReader();

                //Loop trough each returning row
                while (sqlReader.Read())
                {
                    try
                    {
                        SQLRZ oRes = new SQLRZ();
                        oRes.RZID = long.Parse(sqlReader["RZID"] as string);
                        oRes.Shortname = sqlReader["Shortname"] as string;

                        bool bBootstrap = false;
                        try
                        {
                            string sBootstrap = sqlReader["Bootstrap"] as string;
                            bool.TryParse(sBootstrap, out bBootstrap);
                        }
                        catch { }
                        oRes.Bootstrap = bBootstrap;

                        oRes.Version = sqlReader["Version"] as string;

#if DEBUG
                        System.IO.File.AppendAllLines(Environment.ExpandEnvironmentVariables("%TEMP%\\RZDebug.txt"), new string[] { DateTime.Now.ToString() + ";R4;" + "SW found: ", oRes.RZID.ToString(), oRes.Shortname, oRes.Version, oRes.Bootstrap.ToString() });
#endif

                        lResult.Add(oRes);
                    }
                    catch (Exception ex)
                    {
                        System.IO.File.AppendAllLines(Environment.ExpandEnvironmentVariables("%TEMP%\\RZError.txt"), new string[] { DateTime.Now.ToString() + ";F230E1" + ex.Message, "SQL:" + SQLSRZIDLookup, "Connection:" + CM12SQLConnectionString });
                    }
                }

                sqlReader.Close();
                return lResult;
            }
            catch (Exception ex)
            {
                System.IO.File.AppendAllLines(Environment.ExpandEnvironmentVariables("%TEMP%\\RZError.txt"), new string[] { DateTime.Now.ToString() + ";F239E1" + ex.Message, "SQL:" + SQLSRZIDLookup, "Connection:" + CM12SQLConnectionString });
            }
            finally
            {
                sqlConnection.Close();
            }

            return lResult;
        }

        private IResultObject getApplicationFromCIID(string CIID)
        {
            return connectionManager.GetInstance("SMS_Application.CI_ID=" + CIID);
        }

        private IResultObject getCategoryUser(string CategoryName)
        {
            IResultObject oCategegories = connectionManager.QueryProcessor.ExecuteQuery(string.Format("select * from SMS_CategoryInstance where CategoryTypeName='CatalogCategories' and LocalizedCategoryInstanceName = '{0}'", CategoryName));
            foreach (IResultObject oCat in oCategegories)
            {
                return oCat;
            }

            return NewUserCategory(CategoryName);
        }

        private string getScopeID(string ScopeName)
        {
            foreach (IResultObject oScope in connectionManager.QueryProcessor.ExecuteQuery(string.Format("SELECT * FROM SMS_SecuredCategory WHERE CategoryName = '{0}'", ScopeName)))
            {
                return oScope.Properties["CategoryID"].StringValue;
            }

            return "";
        }

        private void addObjectScope(string scopeId, string objectKey, int objectTypeId)
        {
            // Create a new instance of the scope assignment.
            IResultObject assignment = connectionManager.CreateInstance("SMS_SecuredCategoryMembership");
            // Configure the assignment
            assignment.Properties["CategoryID"].StringValue = scopeId;
            assignment.Properties["ObjectKey"].StringValue = objectKey;
            assignment.Properties["ObjectTypeID"].IntegerValue = objectTypeId;
            // Commit the assignment
            assignment.Put();
        }

        private bool addApplicationScope(string ScopeName, Application aApp)
        {
            try
            {
                string sScopeID = getScopeID(ScopeName);
                if (!string.IsNullOrEmpty(sScopeID))
                {
                    addObjectScope(sScopeID, aApp.Id.Scope + "/" + aApp.Id.Name, 31);
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.IO.File.AppendAllLines(Environment.ExpandEnvironmentVariables("%TEMP%\\RZError.txt"), new string[] { DateTime.Now.ToString() + ";F389E1" + ex.Message });
            }
            return false;
        }

        private DeploymentType createScriptDt(string title, string description, string installCommandLine, string uninstallCommandLine, string detectionPS, string contentFolder)
        {
            ScriptInstaller installer = new ScriptInstaller();

            //http://blogs.technet.com/b/configurationmgr/archive/2015/09/30/configmgr-2012-support-tip-migrating-an-application-to-a-new-hierarchy-creates-new-content-id.aspx
            //Content-Folder must have a trailing '\' appended...
            if (!contentFolder.EndsWith("\\", StringComparison.InvariantCultureIgnoreCase))
                contentFolder = contentFolder + "\\";

            installer.InstallCommandLine = installCommandLine;
            installer.UninstallCommandLine = uninstallCommandLine;

            bool fakeCode = false;


            installer.DetectionMethod = DetectionMethod.Script;
            Script oDetScript = new Script();
            if (string.IsNullOrEmpty(detectionPS))
            {
                detectionPS = "$null";
                fakeCode = true;
            }

            oDetScript.Text = detectionPS;
            oDetScript.Language = ScriptingLanguage.PowerShell.ToString();
            installer.DetectionScript = oDetScript;

            //if detection script contians HKCU run App as User
            if (detectionPS.IndexOf("hkcu", StringComparison.CurrentCultureIgnoreCase) >= 0)
            {
                installer.ExecutionContext = Microsoft.ConfigurationManagement.ApplicationManagement.ExecutionContext.User;
                installer.UserInteractionMode = UserInteractionMode.Hidden;
            }
            else
            {
                installer.ExecutionContext = Microsoft.ConfigurationManagement.ApplicationManagement.ExecutionContext.System;
                installer.UserInteractionMode = UserInteractionMode.Hidden;
            }

            // Only add content if specified and exists.
            if (Directory.Exists(contentFolder) == true)
            {
                Content installerContent = ContentImporter.CreateContentFromFolder(contentFolder);

                installerContent.FallbackToUnprotectedDP = true;
                installerContent.PeerCache = true;
                installerContent.OnSlowNetwork = ContentHandlingMode.Download;
                installerContent.OnFastNetwork = ContentHandlingMode.Download;

                installerContent.Validate();

                if (installerContent != null)
                {
                    installer.Contents.Add(installerContent);
                }

                ContentRef oRef = new ContentRef();
                oRef.Id = installerContent.Id;
                installer.InstallContent = oRef;
            }

            installer.Validate();

            DeploymentType dt = new DeploymentType(installer, ScriptDeploymentTechnology.TechnologyId, NativeHostingTechnology.TechnologyId);
            dt.Title = title;

            //Mark DT Title
            if (fakeCode)
                dt.Title = title + " WARNING, Detection Rule not valid !";

            //dt.Requirements.Add(Rule_All_x64_Windows_7_Client());
            //dt.Requirements.Add(_rule_PrimaryDevice(true));
            //dt.Requirements.Add(Rule_MachineOU("OU=Workplace,DC=corp,DC=gwpnet,DC=com", true));
            //dt.Requirements.Add(_ruleGlobalBoolean("SRWP"));

            return dt;
        }

        private Rule rule_OSExpression(List<string> OSExpressions)
        {
            //Create OS Requirement Rule
            CustomCollection<RuleExpression> ruleCollection = new CustomCollection<RuleExpression>();
            string sRule = "";
            foreach (String sExpr in OSExpressions)
            {
                RuleExpression ruleExpression = new RuleExpression(sExpr);
                ruleCollection.Add(ruleExpression);
                if (string.IsNullOrEmpty(sRule))
                {
                    sRule = sExpr.Replace("Windows/", "");
                }
                else
                {
                    sRule = sRule + ";" + sExpr.Replace("Windows/", "");
                }

            }

            OperatingSystemExpression osExpression = new OperatingSystemExpression(ExpressionOperator.OneOf, ruleCollection);
            Microsoft.SystemsManagementServer.DesiredConfigurationManagement.Rules.Rule rule = new Microsoft.SystemsManagementServer.DesiredConfigurationManagement.Rules.Rule
    ("Rule_" + Guid.NewGuid().ToString("D"), NoncomplianceSeverity.None, new Annotation("Operating system One of {" + sRule + "}", null, null, null), osExpression);

            return rule;
        }

        private Rule rule_PrimaryDevice(bool PrimaryDevice)
        {
            CustomCollection<ExpressionBase> operands = new CustomCollection<ExpressionBase>();

            ExpressionBase primExpression = new GlobalSettingReference("GLOBAL", "PrimaryDevice", DataType.Boolean, "PrimaryDevice_Setting_LogicalName", ConfigurationItemSettingSourceType.CIM);
            operands.Add(primExpression);
            ExpressionBase primExpression2 = new ConstantValue(PrimaryDevice.ToString().ToUpper(), DataType.Boolean);
            operands.Add(primExpression2);
            Expression expression = new Expression(ExpressionOperator.IsEquals, operands);

            Microsoft.SystemsManagementServer.DesiredConfigurationManagement.Rules.Rule rule = new Microsoft.SystemsManagementServer.DesiredConfigurationManagement.Rules.Rule
            ("Rule_" + Guid.NewGuid().ToString("D"), NoncomplianceSeverity.None, new Annotation("Primary device Equals " + PrimaryDevice.ToString().ToUpper(), null, null, null), expression);

            return rule;
        }

        private Rule ruleGlobalBoolean(string DisplayName)
        {
            string sScopeID = "";
            string sLogicalName = "";
            foreach (IResultObject oRes in connectionManager.QueryProcessor.ExecuteQuery("SELECT * FROM  SMS_GlobalCondition WHERE IsLatest = 'TRUE' and LocalizedDisplayName like '" + DisplayName + "%'"))
            {
                try
                {
                    string sID = oRes.Properties["CI_UniqueID"].StringValue;
                    sScopeID = sID.Split('/')[0];
                    sLogicalName = sID.Split('/')[1];
                }
                catch { }

                continue;
            }

            if (!string.IsNullOrEmpty(sScopeID))
            {
                CustomCollection<ExpressionBase> operands = new CustomCollection<ExpressionBase>();
                ExpressionBase globalExpression = new GlobalExpression(sScopeID, sLogicalName);
                ExpressionBase constValue = new ConstantValue("True", DataType.Boolean);
                operands.Add(globalExpression);
                operands.Add(constValue);
                Expression expression = new Expression(ExpressionOperator.IsEquals, operands);

                Microsoft.SystemsManagementServer.DesiredConfigurationManagement.Rules.Rule rule = new Microsoft.SystemsManagementServer.DesiredConfigurationManagement.Rules.Rule
                ("Rule_" + Guid.NewGuid().ToString("D"), NoncomplianceSeverity.None, new Annotation(DisplayName + " Equals True", null, null, null), expression);

                return rule;
            }

            return null;

        }

        private IResultObject getApplicationFromCI_UniqueID(string CI_UniqueID)
        {
            IResultObject iApp = null;

            IResultObject oAppCheck;


            oAppCheck = connectionManager.QueryProcessor.ExecuteQuery(string.Format("select * from SMS_Application where CI_UniqueID like '{0}/%' and IsLatest = 'TRUE'", CI_UniqueID));

            foreach (IResultObject oAppExists in oAppCheck)
            {
                iApp = oAppExists;

                break;
            }

            return iApp;
        }

        private IResultObject getCollection(string CollectionID)
        {
            List<IResultObject> oResult = new List<IResultObject>();

            IResultObject oColl = connectionManager.GetInstance("SMS_Collection.CollectionID='" + CollectionID + "'");
            return oColl;

        }

        private IResultObject getCollectionFromSWID(string SWID, string Suffix)
        {
            SqlConnection sqlConnection = new SqlConnection(CM12SQLConnectionString);
            try
            {
                sqlConnection.Open();

                //Connect SQL and run query to get List of active local admin usernames
                SqlCommand sqlCommand = new SqlCommand(SQLCollQueryLookup, sqlConnection);

                //Add paremeter for the SQL Query
                sqlCommand.Parameters.Add(new SqlParameter("SWID", SWID + Suffix));

                //Execute SQL Command
                SqlDataReader sqlReader = sqlCommand.ExecuteReader(System.Data.CommandBehavior.SingleRow);

                //Loop trough each returning row
                while (sqlReader.Read())
                {
                    try
                    {
                        //Add Username to result list
                        string sCollID = sqlReader["CollectionID"].ToString();

                        return getCollection(sCollID);
                    }
                    catch { }
                }

                sqlReader.Close();

            }
            catch { }
            finally
            {
                sqlConnection.Close();
            }

            return null;
        }

        private IResultObject getUserCollections(string collectionName)
        {
            try
            {
                IResultObject collections = connectionManager.QueryProcessor.ExecuteQuery(string.Format("SELECT * FROM SMS_COLLECTION WHERE Name = '{0}' AND CollectionType = 1", collectionName));
                return collections;
            }
            catch { }

            return null;
        }

        private IResultObject createUserCollection(string CollectionName, string LimitingCollID)
        {
            try
            {
                foreach (IResultObject oexistingColl in getUserCollections(CollectionName))
                {
                    oexistingColl.Get();
                    return oexistingColl;
                }

                //No existing Collection found...
                IResultObject collection = connectionManager.CreateInstance("SMS_Collection");
                collection["CollectionType"].IntegerValue = 1; //1=User Collection; 2=Device; 0=Other
                collection["Name"].StringValue = CollectionName;
                collection["LimitToCollectionID"].StringValue = LimitingCollID;
                collection["Comment"].StringValue = "generated by script";
                collection.Put();
                collection.Get();
                return collection;
            }
            catch { }

            return null;
        }

        private List<int> getUserGroupResourceID(string GroupName)
        {
            List<int> iResult = new List<int>();
            try
            {
                IResultObject oUsers = connectionManager.QueryProcessor.ExecuteQuery(string.Format("SELECT * FROM SMS_R_UserGroup WHERE UniqueUsergroupName = '{0}'", GroupName.Replace(@"\", @"\\")));
                foreach (IResultObject oUser in oUsers)
                {
                    try
                    {
                        iResult.Add(oUser.Properties["ResourceId"].IntegerValue);
                    }
                    catch { }
                }
            }
            catch (SmsException ex)
            {
                ex.Message.ToString();
            }

            return iResult;
        }

        private IResultObject createDirectRuleUserGroup(int ResourceID, string GroupName)
        {
            try
            {
                IResultObject collRule = connectionManager.CreateEmbeddedObjectInstance("SMS_CollectionRuleDirect");
                collRule["ResourceClassName"].StringValue = "SMS_R_UserGroup";
                collRule["ResourceID"].IntegerValue = ResourceID;
                collRule["RuleName"].StringValue = GroupName;
                return collRule;
            }
            catch { }

            return null;
        }

        private IResultObject createDirectRuleUserGroup(string GroupName)
        {
            try
            {
                int iResourceID = getUserGroupResourceID(GroupName).First();
                return createDirectRuleUserGroup(iResourceID, GroupName);
            }
            catch { }

            return null;
        }

        private bool addMembershipRule(IResultObject Collection, IResultObject MembershipRule)
        {
            try
            {
                Dictionary<string, object> collectionRuleParameters = new Dictionary<string, object>();
                collectionRuleParameters.Add("collectionRule", MembershipRule);

                IResultObject oResult = Collection.ExecuteMethod("AddMembershipRule", collectionRuleParameters);

                if (oResult.Properties["ReturnValue"].StringValue == "0")
                    return true;
            }
            catch (SmsException ex)
            {
                ex.Message.ToString();
            }

            return false;
        }

        private List<IResultObject> getDeployments(string ObjectID)
        {
            List<IResultObject> oResult = new List<IResultObject>();
            IResultObject oAppDeployments;

            //ConnectionManager.Connect(CM12SiteServer);

            oAppDeployments = connectionManager.QueryProcessor.ExecuteQuery(string.Format("select * from SMS_ApplicationAssignment where AssignedCI_UniqueID like '{0}/%'", ObjectID));

            foreach (IResultObject oDeployment in oAppDeployments)
            {
                oResult.Add(oDeployment);
            }

            return oResult;
        }

        private IResultObject createAppDeployment(string collectionID, IResultObject oNewApp, bool Required, bool save, bool Enabled = true)
        {
            //Reload Object to have the latest CI_ID...
            oNewApp.Get();

            string sDateNow = System.Management.ManagementDateTimeConverter.ToDmtfDateTime(DateTime.Now - new TimeSpan(2, 0, 0));
            IResultObject appInstance = connectionManager.CreateInstance("SMS_ApplicationAssignment");

            appInstance.Properties["ApplicationName"].StringValue = oNewApp["LocalizedDisplayName"].StringValue;
            appInstance.Properties["AssignedCI_UniqueID"].StringValue = oNewApp.Properties["CI_UniqueID"].StringValue;
            appInstance.Properties["AssignedCIs"].ObjectValue = new int[] { oNewApp.Properties["CI_ID"].IntegerValue };
            appInstance.Properties["AssignmentAction"].IntegerValue = 2; //1 = Detect ; 2 = Apply
            appInstance.Properties["AssignmentDescription"].StringValue = "";
            appInstance.Properties["AssignmentName"].StringValue = oNewApp["LocalizedDisplayName"].StringValue + " Install";
            appInstance.Properties["AssignmentType"].IntegerValue = 2; // 0 = DCM Baseline ; 1 = Update ; 2 = Application ; 5 = Update Group ; 8 = Policy
            appInstance.Properties["ContainsExpiredUpdates"].BooleanValue = false;
            appInstance.Properties["CreationTime"].StringValue = sDateNow;
            appInstance.Properties["DesiredConfigType"].IntegerValue = 1; //1= Install ; 2 = Uninstall
            appInstance.Properties["DisableMomAlerts"].BooleanValue = false;
            appInstance.Properties["DPLocality"].IntegerValue = 80;
            appInstance.Properties["Enabled"].BooleanValue = Enabled;

            if (Required)
            {
                appInstance.Properties["OfferTypeID"].IntegerValue = 0; // 2 = Available
                appInstance.Properties["EnforcementDeadline"].StringValue = sDateNow;
                appInstance.Properties["OfferFlags"].IntegerValue = 1; // 1 = Predeploy
            }
            else
            {
                appInstance.Properties["OfferTypeID"].IntegerValue = 2; // 2 = Available
                //appInstance.Properties["EnforcementDeadline"].StringValue = "";
                appInstance.Properties["OfferFlags"].IntegerValue = 0;
            }

            appInstance.Properties["LastModificationTime"].StringValue = sDateNow;
            appInstance.Properties["LastModifiedBy"].StringValue = System.Environment.UserName;
            appInstance.Properties["LocaleID"].IntegerValue = 1033;
            appInstance.Properties["LogComplianceToWinEvent"].BooleanValue = false;
            appInstance.Properties["NotifyUser"].BooleanValue = false; //27.6.2016 -> hide notifications
            appInstance.Properties["OverrideServiceWindows"].BooleanValue = false;
            appInstance.Properties["Priority"].IntegerValue = 1; // 0 = Low ; 1 = Medium ; 2 = High
            appInstance.Properties["RaiseMomAlertsOnFailure"].BooleanValue = false;
            appInstance.Properties["RebootOutsideOfServiceWindows"].BooleanValue = false;
            appInstance.Properties["RequireApproval"].BooleanValue = false;
            appInstance.Properties["SendDetailedNonComplianceStatus"].BooleanValue = false;
            appInstance.Properties["SourceSite"].StringValue = CM12SiteCode;
            appInstance.Properties["StartTime"].StringValue = sDateNow;
            appInstance.Properties["StateMessagePriority"].IntegerValue = 5;
            appInstance.Properties["SuppressReboot"].IntegerValue = 0;
            appInstance.Properties["TargetCollectionID"].StringValue = collectionID;
            appInstance.Properties["UpdateSupersedence"].BooleanValue = false;
            appInstance.Properties["UseGMTTimes"].BooleanValue = false;
            appInstance.Properties["UserUIExperience"].BooleanValue = true;
            appInstance.Properties["WoLEnabled"].BooleanValue = false;

            if (save)
            {
                appInstance.Put();
            }

            return appInstance;

        }

        private IResultObject getDeviceCollections(string collectionName)
        {
            try
            {
                IResultObject collections = connectionManager.QueryProcessor.ExecuteQuery(string.Format("SELECT * FROM SMS_COLLECTION WHERE Name = '{0}' AND CollectionType = 2", collectionName));
                return collections;
            }
            catch { }

            return null;
        }

        private IResultObject createDeviceCollection(string CollectionName, string LimitingCollID)
        {
            try
            {
                foreach (IResultObject oexistingColl in getDeviceCollections(CollectionName))
                {
                    oexistingColl.Get();
                    return oexistingColl;
                }

                //No existing Collection found...
                IResultObject collection = connectionManager.CreateInstance("SMS_Collection");
                collection["CollectionType"].IntegerValue = 2; //1=User Collection; 2=Device; 0=Other
                collection["Name"].StringValue = CollectionName;
                collection["LimitToCollectionID"].StringValue = LimitingCollID;
                collection["Comment"].StringValue = "generated by script";
                collection.Put();
                collection.Get();
                return collection;
            }
            catch { }

            return null;


        }

        /// <summary>
        /// Assign a Package or Application to a DP Group
        /// </summary>
        /// <param name="DPGroupName">Name of the DP group</param>
        /// <param name="PackageID">PackageID; Note: also Applications do have a PackageID !</param>
        /// <returns>result of the addPackages wmi method </returns>
        private IResultObject addDPgroupConentInfo(string DPGroupName, string PackageID)
        {
            foreach (IResultObject oDPGroup in connectionManager.QueryProcessor.ExecuteQuery("SELECT * FROM SMS_DistributionPointGroup WHERE Name = \"" + DPGroupName + "\""))
            {
                try
                {
                    Dictionary<string, object> addPkgParameters = new Dictionary<string, object>();
                    addPkgParameters.Add("PackageIDs", new string[] { PackageID });
                    return oDPGroup.ExecuteMethod("AddPackages", addPkgParameters);
                }
                catch (Exception ex)
                {
                    ex.Message.ToString();
                }
            }

            return null;
        }

        //Download Icon from URL
        public System.Drawing.Bitmap IconDL(string URL)
        {
            WebRequest request = WebRequest.Create(URL);
            WebResponse response = request.GetResponse();
            Stream responseStream = response.GetResponseStream();

            return new System.Drawing.Bitmap(responseStream);
        }

        public void RuckZuckSync(AddSoftware oRZ, DLTask downloadTask, bool Bootstrap = false)
        {
            string PkgName = oRZ.ProductName;
            string PkgVersion = oRZ.ProductVersion;
            string Manufacturer = oRZ.Manufacturer.TrimEnd('.'); //Temp Fix
            string sLanguage = Properties.Settings.Default.DefaultAppLanguage;
            if (string.IsNullOrEmpty(sLanguage))
                sLanguage = "EN";

            downloadTask.Installing = true;

            var tRZSW = GetRZSoftware(PkgName, PkgVersion, Manufacturer);
            List<GetSoftware> lSW = (tRZSW.GetAwaiter()).GetResult();

            foreach (var SW in lSW)
            {
                string SWID = "RZ" + SW.SWId.ToString();

                //Get existing App...
                var oWMIApp = getApplicationFromSWID(SWID);

                if (!Properties.Settings.Default.UpdateExistingApplications)
                {
                    //Skip if App already exists
                    if (oWMIApp != null)
                        return;
                }


                downloadTask.Status = "Creating Application...";

                //Listener.WriteLine(SW.Shortname, "Creating Application...");
                Application oApp = createApplication(SW.ProductName, SW.Description, sLanguage, SW.ProductVersion, SW.Manufacturer, SWID, new string[] { "RuckZuck" }, true);

                Boolean bNew = oApp.CustomProperties.Count <= 0;
                Boolean bChanged = true;
                Boolean bDownloadStatus = true; //set to false if content dl failed

                //It's a new App or something has changed -> reapply propperties
                if (bNew || bChanged || Bootstrap)
                {
                    oApp.Title = SW.ProductName + " " + SW.ProductVersion + " " + SWID;
                    oApp.CustomProperties.Clear();
                    oApp.CustomProperties.Add(new CustomProperty() { Name = "Shortname", Value = SW.ShortName });
                    oApp.CustomProperties.Add(new CustomProperty() { Name = "SWID", Value = SWID });
                    oApp.CustomProperties.Add(new CustomProperty() { Name = "LastModified", Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") });
                    if (Bootstrap)
                        oApp.CustomProperties.Add(new CustomProperty() { Name = "Bootstrap", Value = "true" });
                    else
                        oApp.CustomProperties.Add(new CustomProperty() { Name = "Bootstrap", Value = "false" });

                    addApplicationScope(NewAppSecurityScopeName, oApp);

                    oApp.DisplayInfo.First().Title = SW.ShortName + " " + SW.ProductVersion;

                    try
                    {
                        AppDisplayInfo AppInfo = new AppDisplayInfo { Title = SW.ShortName + " " + SW.ProductVersion, Description = SW.Description, Language = sLanguage };
                        try
                        {
                            foreach (string sCat in SW.Categories)
                            {
                                try
                                {
                                    //Add RZCategory as User Category
                                    AppInfo.UserCategories.Add(getCategoryUser(sCat).Properties["CategoryInstance_UniqueID"].StringValue);
                                }
                                catch { }
                            }
                        }
                        catch { }

                        oApp.DisplayInfo.Clear();
                        oApp.DisplayInfo.Add(AppInfo);
                    }
                    catch { }


                    try
                    {
                        //Fix Icon
                        oApp.DisplayInfo.First().Icon = new Icon(IconDL(SW.IconURL.Replace("size=32","size=128")));

                        //Try to Add in Icon
                        //if (oApp.DisplayInfo.First().Icon == null)
                        //{
                        //    //var oImg = new System.Windows.Media.Imaging.BitmapImage(new Uri(SW.IconURL.Replace("size=32", "size=64")));
                        //    oApp.DisplayInfo.First().Icon = new Icon(IconDL(SW.IconURL.Replace("size=32", "size=64")));
                        //}
                    }
                    catch (Exception ex)
                    {
                        File.AppendAllLines(Environment.ExpandEnvironmentVariables("%TEMP%\\RZError.txt"), new string[] { DateTime.Now.ToString() + ";F800E3" + ex.Message });
                    }

                    oApp.ReleaseDate = DateTime.Now.ToString("dd.MM.yyyy");

                    //Deployment Type
                    if (oApp.DeploymentTypes.Count == 0)
                    {
                        bool bPreReq = false;

                        foreach (var oIT in RZRestAPIv2.GetSoftwares(SW.ProductName, SW.ProductVersion, SW.Manufacturer))
                        {
                            try
                            {
                                if (oIT.Architecture.StartsWith("_prereq_"))
                                {
                                    bPreReq = true;
                                    continue;
                                }


                                RZUpdate.SWUpdate oUpd = new RZUpdate.SWUpdate(oIT);

                                downloadTask.Status = "Creating DeploymentType: " + oIT.Architecture;
                                //Listener.WriteLine(SW.Shortname, "Creating DeploymentType: " + oIT.Architecture);
                                DirectoryInfo oDir = new DirectoryInfo(Path.Combine(sSourcePath, oIT.ContentID.ToString()));

                                if (!Directory.Exists(oDir.FullName))
                                {
                                    //oDir = System.IO.Directory.CreateDirectory(Path.Combine(sSourcePath, oIT.ContentID.ToString()));
                                    oDir = Directory.CreateDirectory(Path.Combine(sSourcePath, oIT.ProductName + " " + oIT.ProductVersion + " " + oIT.Architecture + "_" + SWID));
                                }
                                downloadTask.Status = "Downloading File(s)...";
                                //Listener.WriteLine(SW.Shortname, "Downloading File(s)...");
                                if (!Bootstrap)
                                {
                                    bDownloadStatus = oUpd.DownloadAsync(true, oDir.FullName).Result;

                                    //DL Failed!
                                    if (!bDownloadStatus)
                                    {
                                        downloadTask.Error = true;
                                        downloadTask.ErrorMessage = "content download failed.";
                                        Thread.Sleep(3000);
                                    }
                                }
                                else
                                {
                                    try
                                    {
                                        CreateExe oExe = new CreateExe(Path.Combine(oDir.FullName, oIT.ShortName + "_setup.exe"));
                                        oExe.Icon = oApp.DisplayInfo.First().Icon.Data;
                                        oExe.Sources.Add(Properties.Resources.Source.Replace("RZRZRZ", oIT.ShortName));
                                        oExe.Sources.Add(Properties.Resources.RZUpdate);
                                        oExe.Sources.Add(Properties.Resources.RZRestApi);
                                        oExe.Sources.Add(Properties.Resources.Assembly.Replace("RZRZRZ", oIT.ShortName));

                                        if (!oExe.Compile())
                                        {
                                            System.Windows.MessageBox.Show("Failed to create .Exe", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        bDownloadStatus = false;
                                        System.IO.File.AppendAllLines(Environment.ExpandEnvironmentVariables("%TEMP%\\RZError.txt"), new string[] { DateTime.Now.ToString() + ";E1097;" + ex.Message });
                                    }
                                }


                                downloadTask.Status = "Creating DeploymentType: " + oIT.Architecture;
                                //Listener.WriteLine(SW.Shortname, "Creating DeploymentType: " + oIT.Architecture);
                                try
                                {
                                    if (!Bootstrap)
                                    {
                                        using (StreamWriter outfile = new StreamWriter(oDir.FullName + @"\install.ps1"))
                                        {
                                            outfile.WriteLine("Set-Location $PSScriptRoot;");
                                            if (!string.IsNullOrEmpty(oIT.PSPreInstall))
                                            {
                                                outfile.Write(oIT.PSPreInstall);
                                                outfile.WriteLine();
                                            }

                                            outfile.Write(oIT.PSInstall);
                                            outfile.WriteLine();

                                            if (!string.IsNullOrEmpty(oIT.PSPostInstall))
                                            {
                                                outfile.Write(oIT.PSPostInstall);
                                                outfile.WriteLine();
                                            }
                                            outfile.WriteLine("Exit($ExitCode)");
                                            outfile.Close();
                                        }
                                    }


                                    using (StreamWriter outfile = new StreamWriter(oDir.FullName + @"\uninstall.ps1"))
                                    {
                                        outfile.WriteLine("Set-Location $PSScriptRoot;");
                                        outfile.Write(oIT.PSUninstall);
                                        outfile.Close();
                                    }
                                }
                                catch (Exception ex)
                                {
                                    System.IO.File.AppendAllLines(Environment.ExpandEnvironmentVariables("%TEMP%\\RZError.txt"), new string[] { DateTime.Now.ToString() + ";E1135;" + ex.Message });
                                }

                                string sPSDetection = "$bRes = " + oIT.PSDetection + "; if($bRes) { $true } else { $null}";
                                //Create DT
                                try
                                {
                                    DeploymentType DT;
                                    if (!Bootstrap)
                                        DT = createScriptDt("Install_" + oIT.Architecture, "", "powershell.exe -ExecutionPolicy Bypass -File install.ps1", "powershell.exe -ExecutionPolicy Bypass -File UnInstall.ps1", sPSDetection, oDir.FullName);
                                    else
                                        DT = createScriptDt("Install_" + oIT.Architecture, "", "\"" + oIT.ShortName + "_setup.exe" + "\"", "powershell.exe -ExecutionPolicy Bypass -File UnInstall.ps1", sPSDetection, oDir.FullName);

                                    //There is a PreReq !
                                    if (oIT.PreRequisites != null)
                                    {
                                        if (oIT.PreRequisites.Length > 0)
                                        {
                                            DT.Description = "Warning: This Product depends on: " + string.Join(";", oIT.PreRequisites);
                                        }
                                    }


                                    List<string> lOSRules = new List<string>();

                                    //Define OS Rules from web.config
                                    foreach (string sRule in AppOSRequirements)
                                    {
                                        lOSRules.Add(sRule);
                                    }

                                    if (oIT.PSPreReq.StartsWith("![Environment]::Is64BitProcess", StringComparison.InvariantCultureIgnoreCase))
                                    {
                                        foreach (string sOS in RuckZuck_Tool.Properties.Settings.Default.OSRequirementsX86)
                                        {
                                            lOSRules.Add(sOS);
                                        }
                                    }

                                    if (oIT.PSPreReq.StartsWith("[Environment]::Is64BitProcess", StringComparison.InvariantCultureIgnoreCase))
                                    {
                                        foreach (string sOS in RuckZuck_Tool.Properties.Settings.Default.OSRequirementsX64)
                                        {
                                            lOSRules.Add(sOS);
                                        }
                                    }

                                    if (lOSRules.Count > 0)
                                    {
                                        try
                                        {
                                            DT.Requirements.Add(rule_OSExpression(lOSRules));
                                        }
                                        catch (Exception ex)
                                        {
                                            File.AppendAllLines(Environment.ExpandEnvironmentVariables("%TEMP%\\RZError.txt"), new string[] { DateTime.Now.ToString() + ";E1169: " + ex.Message });
                                        }
                                    }
                                    try
                                    {
                                        if (bPrimaryUserRequired)
                                        {
                                            DT.Requirements.Add(rule_PrimaryDevice(true));
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        File.AppendAllLines(Environment.ExpandEnvironmentVariables("%TEMP%\\RZError.txt"), new string[] { DateTime.Now.ToString() + ";E1178: " + ex.Message });
                                    }

                                    //Add a GlobalCondition Rule if defined
                                    string sGC = GlobalConditionName;
                                    if (!string.IsNullOrEmpty(sGC))
                                    {
                                        DT.Requirements.Add(ruleGlobalBoolean(sGC));
                                    }

                                    if (((ScriptInstaller)DT.Installer).ExitCodes.Count(p => p.Code == 999) == 0)
                                    {
                                        ExitCode ec = new ExitCode();
                                        ec.Class = ExitCodeClass.FastRetry;
                                        ec.Code = 999;
                                        ec.Name = "Custom fast retry";
                                        ((ScriptInstaller)DT.Installer).ExitCodes.Add(ec);
                                    }
                                    oApp.DeploymentTypes.Add(DT);
                                }
                                catch (Exception ex)
                                {
                                    File.AppendAllLines(Environment.ExpandEnvironmentVariables("%TEMP%\\RZError.txt"), new string[] { DateTime.Now.ToString() + ";E1200: " + ex.Message });
                                }

                            }
                            catch (Exception ex)
                            {
                                File.AppendAllLines(Environment.ExpandEnvironmentVariables("%TEMP%\\RZError.txt"), new string[] { DateTime.Now.ToString() + ";F800E1" + ex.Message });
                            }
                        }
                    }

                }

                if (oApp.IsChanged)
                {
                    downloadTask.Status = "Updating Application...";
                    //Listener.WriteLine(SW.Shortname, "Updating Application...");
                    ApplicationFactory factory = new ApplicationFactory();
                    IResultObject oRAWApp = getApplicationFromCI_UniqueID(oApp.Id.Scope + "/" + oApp.Id.Name);
                    AppManWrapper wrapper = factory.WrapExisting(oRAWApp);

                    //Create and move Application to the new Folder
                    try
                    {
                        if (!string.IsNullOrEmpty(RuckZuck_Tool.Properties.Settings.Default.AppFolder))
                        {
                            IResultObject oFolder = createConsoleFolder(RuckZuck_Tool.Properties.Settings.Default.AppFolder, 6000);
                            createConsoleFolderItem(oRAWApp["ModelName"].StringValue, 6000, oFolder["ContainerNodeID"].IntegerValue);
                        }
                    }
                    catch { }

                    try
                    {
                        //Fix Icon
                        oApp.DisplayInfo.First().Icon = new Icon(IconDL(SW.IconURL.Replace("size=32", "size=128"))); //testing only !!!
                    }
                    catch (Exception ex)
                    {
                        File.AppendAllLines(Environment.ExpandEnvironmentVariables("%TEMP%\\RZError.txt"), new string[] { DateTime.Now.ToString() + ";E1183;" + ex.Message });
                    }

                    wrapper.InnerAppManObject = oApp;
                    factory.PrepareResultObject(wrapper);
                    wrapper.InnerResultObject.Put();

                    if (bDownloadStatus)
                    {
                        //Add to DP
                        if (!string.IsNullOrEmpty(DPGroup))
                        {
                            downloadTask.Status = "Assigning content to DP-Group: " +DPGroup;
                            //Listener.WriteLine(SW.Shortname, "Assigning content to DP-Group: " + RuckZuck_Tool.Properties.Settings.Default.DPGroup);
                            try
                            {
                                oRAWApp.Get();
                                string sPkgID = oRAWApp["PackageID"].StringValue;
                                if (!string.IsNullOrEmpty(sPkgID))
                                    addDPgroupConentInfo(DPGroup, sPkgID);
                            }
                            catch (Exception ex)
                            {
                                ex.Message.ToString();
                            }
                        }
                    }
                }




                #region Collection
                bool EnabledDeployment = bDownloadStatus; //disable deployment if download is not complete.

                if (!string.IsNullOrEmpty(sADGroup))
                {
                    //Create User Collection
                    if (bNew | bChanged)
                    {
                        downloadTask.Status = "Creating User Collection...";
                        //Listener.WriteLine(SW.Shortname, "Creating User Collection...");
                        //Try to get Colection based on SWID
                        IResultObject oCollection = getCollectionFromSWID(SWID, "");
                        if (oCollection == null)
                        {
                            try
                            {
                                oCollection = createUserCollection(Properties.Settings.Default.CollPrefix_UAI + SW.ShortName + " " + SW.ProductVersion, LimitingUserCollectionID);
                                oCollection["RefreshType"].ObjectValue = CollectionRefreshType.None;
                                oCollection["Comment"].ObjectValue = SWID; ;
                                oCollection.Put();

                                IResultObject oRule = createDirectRuleUserGroup(sADGroup);
                                addMembershipRule(oCollection, oRule);

                                //Create and move Collection to the new Folder
                                try
                                {
                                    if (!string.IsNullOrEmpty(RuckZuck_Tool.Properties.Settings.Default.CollectionFolder))
                                    {
                                        IResultObject oFolder = createConsoleFolder(RuckZuck_Tool.Properties.Settings.Default.CollectionFolder, 5001);
                                        createConsoleFolderItem(oCollection["CollectionID"].StringValue, 5001, oFolder["ContainerNodeID"].IntegerValue);
                                    }
                                }
                                catch { }

                                //Create an available deployment if no deployment exists
                                IResultObject oRawApp = getApplicationFromCI_UniqueID(oApp.Id.Scope + "/" + oApp.Id.Name);
                                //IResultObject oRawApp = getApplicationFromSWID(SWID);

                                List<IResultObject> oDeployments = getDeployments(oRawApp["ModelName"].StringValue);
                                if (oDeployments.Count <= 0)
                                {
                                    if (RuckZuck_Tool.Properties.Settings.Default.CreateDeployments)
                                    {
                                        downloadTask.Status = "Creating Deployment...";
                                        //Listener.WriteLine(SW.Shortname, "Creating Deployment...");
                                        createAppDeployment(oCollection["CollectionID"].StringValue, oRawApp, false, true, EnabledDeployment);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                File.AppendAllLines(Environment.ExpandEnvironmentVariables("%TEMP%\\RZError.txt"), new string[] { DateTime.Now.ToString() + ";F81185" + ex.Message });
                            }
                        }
                        else
                        {
                            if (bChanged)
                            {
                                try
                                {
                                    downloadTask.Status = "Updating existing User Collection...";
                                    //Listener.WriteLine(SW.Shortname, "Updating existing User Collection...");
                                    oCollection["Name"].StringValue = Properties.Settings.Default.CollPrefix_UAI + SW.ShortName + " " + SW.ProductVersion;
                                    oCollection["RefreshType"].ObjectValue = CollectionRefreshType.None;
                                    oCollection.Put();
                                }
                                catch (Exception ex)
                                {
                                    File.AppendAllLines(Environment.ExpandEnvironmentVariables("%TEMP%\\RZError.txt"), new string[] { DateTime.Now.ToString() + ";F81201" + ex.Message });
                                }
                            }
                        }
                    }
                }
                else
                {
                    if (bNew | bChanged)
                    {
                        IResultObject oCollection = getCollectionFromSWID(SWID, "");
                        if (oCollection == null)
                        {
                            try
                            {
                                //Create Device Collection
                                oCollection = createDeviceCollection(Properties.Settings.Default.CollPrefix_DRI + SW.ShortName + " " + SW.ProductVersion, LimitingDeviceCollectionID);
                                oCollection["RefreshType"].ObjectValue = CollectionRefreshType.None;
                                oCollection["Comment"].ObjectValue = SWID;
                                oCollection.Put();

                                //Create and move Collection to the new Folder
                                try
                                {
                                    if (!string.IsNullOrEmpty(CollFolder))
                                    {
                                        IResultObject oFolder = createConsoleFolder(CollFolder, 5000);
                                        createConsoleFolderItem(oCollection["CollectionID"].StringValue, 5000, oFolder["ContainerNodeID"].IntegerValue);
                                    }
                                }
                                catch { }

                                //Create a required deployment if no deployment exists
                                IResultObject oRawApp = getApplicationFromCI_UniqueID(oApp.Id.Scope + "/" + oApp.Id.Name);

                                List<IResultObject> oDeployments = getDeployments(oRawApp["ModelName"].StringValue);
                                if (oDeployments.Count <= 0)
                                {
                                    if (RuckZuck_Tool.Properties.Settings.Default.CreateDeployments)
                                    {
                                        createAppDeployment(oCollection["CollectionID"].StringValue, oRawApp, true, true, EnabledDeployment);
                                    }

                                }
                            }
                            catch (Exception ex)
                            {
                                File.AppendAllLines(Environment.ExpandEnvironmentVariables("%TEMP%\\RZError.txt"), new string[] { DateTime.Now.ToString() + ";F81235;" + ex.Message });
                            }

                        }
                        else
                        {
                            if (bChanged)
                            {
                                try
                                {
                                    oCollection["Name"].StringValue = Properties.Settings.Default.CollPrefix_URI + SW.ShortName + " " + SW.ProductVersion;
                                    oCollection["RefreshType"].ObjectValue = CollectionRefreshType.None;
                                    oCollection.Put();
                                }
                                catch (Exception ex)
                                {
                                    File.AppendAllLines(Environment.ExpandEnvironmentVariables("%TEMP%\\RZError.txt"), new string[] { DateTime.Now.ToString() + ";F81251;" + ex.Message });
                                }

                            }
                        }
                    }
                }
                #endregion

            }

            downloadTask.Status = "";
            downloadTask.Installing = false;
            downloadTask.Installed = true;

            /*if (PDx != null)
            {
                Listener.CloseDialog();
                Listener.Close();
                //PDx.Close();
            }*/
        }

        /// <summary>
        /// Get an SMS_ObjectContainerNode (Folder) based on the Folder Name and ObjectType
        /// </summary>
        /// <param name="FolderName">name of the Folder</param>
        /// <param name="ObjectType">5000 = DeviceCollection, 5001=UserCollection, 6000=Application</param>
        /// <returns>SMS_ObjectContainerNode</returns>
        public IResultObject getFolder(string FolderName, int ObjectType = 5001)
        {
            IResultObject oFolders = connectionManager.QueryProcessor.ExecuteQuery(string.Format("select * from SMS_ObjectContainerNode WHERE Name='{0}' AND ObjectType={1}", FolderName, ObjectType));
            foreach (IResultObject oFolder in oFolders)
            {
                return oFolder;
            }

            return null;
        }

        /// <summary>
        /// Create a FolderItem
        /// http://msdn.microsoft.com/en-us/library/hh949345.aspx
        /// </summary>
        /// <param name="instanceID">ID of the Object (e.g. Application ModelName (ScopeId_6EA78D32-8998-4CB5-B851-9037B197714A/Application_4669da8a-123a-4fb3-9573-ffc504f8f5c0) or PackageID)</param>
        /// <param name="objectType">
        /// 2 = TYPE_PACKAGE
        /// 3 = TYPE_ADVERTISEMENT
        /// 7 = TYPE_QUERY
        /// 8 = TYPE_REPORT
        /// 9 = TYPE_METEREDPRODUCTRULE
        /// 11= TYPE_CONFIGURATIONITEM
        /// 14= TYPE_OSINSTALLPACKAGE
        /// 17= TYPE_STATEMIGRATION
        /// 18= TYPE_IMAGEPACKAGE
        /// 19= TYPE_BOOTIMAGEPACKAGE
        /// 20= TYPE_TASKSEQUENCEPACKAGE
        /// 21= TYPE_DEVICESETTINGPACKAGE
        /// 23= TYPE_DRIVERPACKAGE
        /// 25= TYPE_DRIVER
        /// 1011=TYPE_SOFTWAREUPDATE
        /// 2011=TYPE_CONFIGURATIONBASELINE
        /// 5000=TYPE_DeviceColection
        /// 5001=TYPE_UserCollection
        /// 6000=SMS_ApplicationLatest</param>
        /// <param name="nodeID">FolderID</param>
        /// <returns>SMS_ObjectContainerItem</returns>
        public IResultObject createConsoleFolderItem(string instanceID, Int32 objectType, Int32 nodeID)
        {
            try
            {
                IResultObject folderItem = connectionManager.CreateInstance("SMS_ObjectContainerItem");

                folderItem["InstanceKey"].StringValue = instanceID;
                folderItem["ObjectType"].IntegerValue = objectType;
                folderItem["ContainerNodeID"].IntegerValue = nodeID;

                folderItem.Put();
                folderItem.Get();

                return folderItem;
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Create a new CM Console Folder
        /// </summary>
        /// <param name="name">Folder Name</param>
        /// <param name="objectType">
        /// 2 = TYPE_PACKAGE
        /// 3 = TYPE_ADVERTISEMENT
        /// 7 = TYPE_QUERY
        /// 8 = TYPE_REPORT
        /// 9 = TYPE_METEREDPRODUCTRULE
        /// 11= TYPE_CONFIGURATIONITEM
        /// 14= TYPE_OSINSTALLPACKAGE
        /// 17= TYPE_STATEMIGRATION
        /// 18= TYPE_IMAGEPACKAGE
        /// 19= TYPE_BOOTIMAGEPACKAGE
        /// 20= TYPE_TASKSEQUENCEPACKAGE
        /// 21= TYPE_DEVICESETTINGPACKAGE
        /// 23= TYPE_DRIVERPACKAGE
        /// 25= TYPE_DRIVER
        /// 1011=TYPE_SOFTWAREUPDATE
        /// 2011=TYPE_CONFIGURATIONBASELINE
        /// 5000=TYPE_DeviceColection
        /// 5001=TYPE_UserCollection
        /// 6000=SMS_ApplicationLatest
        /// </param>
        /// <param name="parentNodeID">'0 = Root; otherwise FolderID of the parent Folder</param>
        /// <returns></returns>
        public IResultObject createConsoleFolder(string name, Int32 objectType = 5001, Int32 parentNodeID = 0)
        {
            try
            {
                IResultObject oExisting = getFolder(name, objectType);
                if (oExisting == null)
                {
                    IResultObject folder = connectionManager.CreateInstance("SMS_ObjectContainerNode");

                    folder["Name"].StringValue = name;
                    folder["ObjectType"].IntegerValue = objectType;
                    folder["ParentContainerNodeID"].IntegerValue = parentNodeID;

                    folder.Put();
                    folder.Get();

                    return folder;
                }
                else
                {
                    return oExisting;
                }

            }
            catch { }

            return null;
        }

        public class SQLRZ
        {
            public long RZID { get; set; }
            public string Shortname { get; set; }
            public bool Bootstrap { get; set; }
            public string Version { get; set; }
        }
    }

    [Cmdlet(VerbsCommon.New, "RZApp")]
    public class PSRZ4ConfigMgr : PSCmdlet
    {
        [Parameter(Mandatory = true, HelpMessage = "ProductName of the RuckZuck Package", ValueFromRemainingArguments = true)]
        [ValidateNotNullOrEmpty]
        public string ProductName { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "ProductVersion of the RuckZuck Package", ValueFromRemainingArguments = true)]
        public string ProductVersion { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Manufacturer of the RuckZuck Package", ValueFromRemainingArguments = true)]
        public string Manufacturer { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Create a bootstrap .exe instead of downloading the source files", ValueFromRemainingArguments = true)]
        public bool Bootstrap { get; set; }

        public List<AddSoftware> SWUpdates { get; set; }

        internal CMAPI oCM12 = new CMAPI();
        public void Scan()
        {
            //Disable SSL/TLS Errors
            System.Net.ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
            //Disable CRL Check
            System.Net.ServicePointManager.CheckCertificateRevocationList = false;
            //Get Proxy from IE
            WebRequest.DefaultWebProxy = WebRequest.GetSystemWebProxy();

            XmlDocument xDoc = new XmlDocument();

            string sConfigPath = System.Reflection.Assembly.GetExecutingAssembly().Location + ".config";
            if (!File.Exists(sConfigPath))
            {
                string sFileName = Path.GetFileName(sConfigPath);
                sConfigPath = sConfigPath.Replace(sFileName, sFileName.Replace(".exe.", ".dll."));
            }
            if (File.Exists(sConfigPath))
            {
                try
                {
                    xDoc.Load(System.Reflection.Assembly.GetExecutingAssembly().Location + ".config");


                    string sSQL = xDoc.SelectSingleNode(@"configuration/applicationSettings/RuckZuck_Tool.Properties.Settings/setting[@name='CM_SQLServer']").InnerText ?? "";
                    string sSQLDB = xDoc.SelectSingleNode(@"configuration/applicationSettings/RuckZuck_Tool.Properties.Settings/setting[@name='CM_SQLServerDBName']").InnerText ?? "";
                    oCM12.CM12SQLConnectionString = "Data Source=" + sSQL + ";Initial Catalog=" + sSQLDB + ";Integrated Security=True";
                    oCM12._CMSiteServer = xDoc.SelectSingleNode(@"configuration/applicationSettings/RuckZuck_Tool.Properties.Settings/setting[@name='CM_Server']").InnerText ?? "";
                    oCM12.sSourcePath = xDoc.SelectSingleNode(@"configuration/applicationSettings/RuckZuck_Tool.Properties.Settings/setting[@name='CMContentSourceUNC']").InnerText;
                    oCM12.sADGroup = Environment.ExpandEnvironmentVariables(xDoc.SelectSingleNode(@"configuration/applicationSettings/RuckZuck_Tool.Properties.Settings/setting[@name='DefaultADGroup']").InnerText ?? "");
                    oCM12.LimitingUserCollectionID = xDoc.SelectSingleNode(@"configuration/applicationSettings/RuckZuck_Tool.Properties.Settings/setting[@name='LimitingUserCollectionID']").InnerText ?? "";
                    oCM12.connectionManager.Connect(oCM12._CMSiteServer);
                    oCM12.CM12SiteCode = oCM12.connectionManager.NamedValueDictionary["ConnectedSiteCode"].ToString();
                    oCM12.NewAppSecurityScopeName = xDoc.SelectSingleNode(@"configuration/applicationSettings/RuckZuck_Tool.Properties.Settings/setting[@name='CMSecurityScope']").InnerText ?? "";
                    oCM12.bPrimaryUserRequired = bool.Parse(xDoc.SelectSingleNode(@"configuration/applicationSettings/RuckZuck_Tool.Properties.Settings/setting[@name='PrimaryUserRequired']").InnerText ?? "true");
                    oCM12.DPGroup = xDoc.SelectSingleNode(@"configuration/applicationSettings/RuckZuck_Tool.Properties.Settings/setting[@name='DPGroup']").InnerText;
                    oCM12.CollFolder = xDoc.SelectSingleNode(@"configuration/applicationSettings/RuckZuck_Tool.Properties.Settings/setting[@name='CollectionFolder']").InnerText;
                }
                catch { }
            }
            RZScan oSCAN = new RZScan(false, false);
            Console.WriteLine("Connecting RuckZuck Repository...");
            oSCAN.GetSWRepositoryAsync(new CancellationTokenSource(30000).Token).GetAwaiter().GetResult();
            Console.WriteLine(oSCAN.SoftwareRepository.Count.ToString() + " Items in Repsoitory");
            Console.WriteLine("Checking current Applications...");

            oSCAN.scan(oCM12);

            Console.WriteLine(oSCAN.InstalledSoftware.Count.ToString() + " Applications detected");
            Console.WriteLine("Checking for Updates...");
            oSCAN.CheckUpdatesAsync(null).Wait();
            Console.WriteLine(oSCAN.NewSoftwareVersions.Count.ToString() + " Updates detected.");
            SWUpdates = oSCAN.NewSoftwareVersions;
        }

        public void Update()
        {
            foreach (AddSoftware oItem in SWUpdates)
            {
                try
                {
                    SWUpdate oSW = new SWUpdate(oItem);
                    oSW.GetInstallType();
                    oSW.SW.Author = oItem.Author; //Author is used to store the Bootstrap flag

                    Console.WriteLine("Preparig Application: " + oSW.SW.ShortName + " " + oSW.SW.ProductVersion);
                    oSW.downloadTask.AutoInstall = true;
                    oSW.InstallCM_cmd(oCM12, false, false);

                    foreach (string sPreReq in oSW.SW.PreRequisites)
                    {
                        try
                        {
                            SWUpdate oPreReq = new SWUpdate(sPreReq);
                            oPreReq.GetInstallType();
                            Console.WriteLine("Preparig PreRequisite: " + oSW.SW.ShortName + " " + oSW.SW.ProductVersion);
                            oPreReq.downloadTask.AutoInstall = true;
                            oPreReq.InstallCM_cmd(oCM12, false, false);
                        }
                        catch { }
                    }
                }
                catch { }
            }
        }

        //protected override void BeginProcessing()
        //{
        //    Scan();
        //}
    }
}

namespace RZUpdate
{
    public partial class SWUpdate
    {
        public async Task<bool> InstallCM(bool Force = false, bool Retry = false)
        {
            try
            {
                downloadTask.Downloading = false;
                downloadTask.Installing = true;
                ProgressDetails(downloadTask, EventArgs.Empty);

                //Check if RuckZuckis running...
                try
                {
                    using (var mutex = Mutex.OpenExisting(@"Global\RuckZuckCM"))
                    {
                        if (Retry)
                        {
                            Thread.Sleep(new TimeSpan(0, 0, 2));
                        }
                        else
                            return false;
                    }
                    GC.Collect();
                }
                catch
                {
                }

                bool bMutexCreated = false;
                using (Mutex mutex = new Mutex(false, "Global\\RuckZuckCM", out bMutexCreated))
                {
                    CMAPI oAPI = new CMAPI();
                    ProgressDetails(downloadTask, EventArgs.Empty);
                    bool bBootStrap = false;
                    if (!string.IsNullOrEmpty(SW.Author))
                    {
                        if (SW.Author == "BootstrapTrue")
                            bBootStrap = true;
                    }

                    oAPI.RuckZuckSync(SW, this.downloadTask, bBootStrap);
                    downloadTask.Installed = true;
                    downloadTask.Installing = false;
                    ProgressDetails(downloadTask, EventArgs.Empty);

                    if (bMutexCreated)
                        mutex.Close();
                }
                GC.Collect();

            }
            catch (Exception ex)
            {
                downloadTask.Status = "";
                downloadTask.Installing = false;
                downloadTask.Downloading = false;
                downloadTask.Error = true;
                downloadTask.ErrorMessage = ex.Message;
                ProgressDetails(downloadTask, EventArgs.Empty);
                return false;
            }
            return true;
        }

        internal bool InstallCM_cmd(CMAPI oAPI, bool Force = false, bool Retry = false)
        {
            try
            {
                downloadTask.Downloading = false;
                downloadTask.Installing = true;
                ProgressDetails(downloadTask, EventArgs.Empty);

                //Check if RuckZuckis running...
                try
                {
                    using (var mutex = Mutex.OpenExisting(@"Global\RuckZuckCM"))
                    {
                        if (Retry)
                        {
                            Thread.Sleep(new TimeSpan(0, 0, 2));
                        }
                        else
                            return false;
                    }
                    GC.Collect();
                }
                catch
                {
                }

                bool bMutexCreated = false;
                using (Mutex mutex = new Mutex(false, "Global\\RuckZuckCM", out bMutexCreated))
                {
                    bool bBootStrap = false;
                    if (!string.IsNullOrEmpty(SW.Author))
                    {
                        if (SW.Author == "BootstrapTrue")
                            bBootStrap = true;
                    }

                    oAPI.RuckZuckSync(SW, this.downloadTask, bBootStrap);
                    downloadTask.Installed = true;
                    downloadTask.Installing = false;
                    ProgressDetails(downloadTask, EventArgs.Empty);

                    if (bMutexCreated)
                        mutex.Close();
                }
                GC.Collect();

            }
            catch (Exception ex)
            {
                downloadTask.Status = "";
                downloadTask.Installing = false;
                downloadTask.Downloading = false;
                downloadTask.Error = true;
                downloadTask.ErrorMessage = ex.Message;
                ProgressDetails(downloadTask, EventArgs.Empty);
                return false;
            }
            return true;
        }

        public async Task<bool> Download(bool Enforce)
        {
            downloadTask.AutoInstall = true;
            downloadTask.SWUpd = this;
            downloadTask.PercentDownloaded = 100;
            downloadTask.Downloading = false;
            downloadTask.Installing = false;
            downloadTask.Status = "Connecting ConfigMgr...";
            //Downloaded(downloadTask, EventArgs.Empty);
            ProgressDetails(downloadTask, EventArgs.Empty);
            //OnSWUpdated(this, new EventArgs());
            return true;
        }

        public async Task<string> GetDLPath()
        {
            string sSourcePath = RuckZuck_Tool.Properties.Settings.Default.CMContentSourceUNC;
            var lSW = (await RZRestAPIv2.GetCatalogAsync()).Where(t => t.ProductName == SW.ProductName && t.Manufacturer == SW.Manufacturer && t.ProductVersion == SW.ProductVersion);

            string sDir = Path.Combine(sSourcePath, SW.ProductName + " " + SW.ProductVersion + " " + SW.Architecture + "_" + lSW.First().SWId);
            DirectoryInfo oDir = new DirectoryInfo(Path.Combine(sSourcePath, SW.ProductName + " " + SW.ProductVersion + " " + SW.Architecture + "_" + lSW.First().SWId));
            if (!oDir.Exists)
                return oDir.Parent.FullName;
            return oDir.FullName;
        }
    }


}

namespace RuckZuck.Base
{
    public partial class RZScan
    {
        public Task SWScan()
        {
            CMAPI oAPI = new CMAPI();
            var tSWScan = Task.Run(() =>
            {
                scan(oAPI);
                OnUpdScanCompleted(this, new EventArgs());
                OnSWScanCompleted(this, new EventArgs());
            });

            return tSWScan;
        }

        internal void scan(CMAPI oAPI)
        {

            try
            {
                List<CMAPI.SQLRZ> lIDs = oAPI.getRZIDs();
                //File.AppendAllLines(Environment.ExpandEnvironmentVariables("%TEMP%\\dbg.txt"), new string[] { "Repository-Items:" + SoftwareRepository.Count.ToString() });

#if DEBUG
                System.IO.File.AppendAllLines(Environment.ExpandEnvironmentVariables("%TEMP%\\RZDebug.txt"), new string[] { DateTime.Now.ToString() + ";S0;" + "RZItems detected: ", lIDs.Count.ToString() });
#endif
#if DEBUG
                System.IO.File.AppendAllLines(Environment.ExpandEnvironmentVariables("%TEMP%\\RZDebug.txt"), new string[] { DateTime.Now.ToString() + ";S0;" + "Repository Items: ", SoftwareRepository.Count().ToString() });
#endif

                foreach (CMAPI.SQLRZ SQLRZ in lIDs)
                {
                    try
                    {
                        if (SoftwareRepository.Count(t => t.SWId == SQLRZ.RZID) == 0)
                        {
                            //File.AppendAllLines(Environment.ExpandEnvironmentVariables("%TEMP%\\dbg.txt"), new string[] { "not match, IconId:" + SQLRZ.RZID.ToString() });

                            var oSW = SoftwareRepository.FirstOrDefault(t => t.ShortName == SQLRZ.Shortname);

                            if (oSW != null)
                            {
                                AddSoftware oNew = new AddSoftware()
                                {
                                    ProductName = oSW.ProductName,
                                    ProductVersion = oSW.ProductVersion,
                                    Manufacturer = oSW.Manufacturer,
                                    ShortName = oSW.ShortName,
                                    Description = oSW.Description,
                                    SWId = oSW.SWId,
                                    IconHash = oSW.IconHash,
                                    MSIProductID = SQLRZ.Version
                                };

                                if (SQLRZ.Bootstrap)
                                    oNew.Author = "BootstrapTrue";
                                else
                                    oNew.Author = "BootstrapFalse";

#if DEBUG
                                System.IO.File.AppendAllLines(Environment.ExpandEnvironmentVariables("%TEMP%\\RZDebug.txt"), new string[] { DateTime.Now.ToString() + ";S1;" + "New SWVersion: ", oNew.ProductName, oNew.ProductVersion, oNew.MSIProductID });
#endif
                                NewSoftwareVersions.Add(oNew);
                            }
                        }
                        else
                        {
                            try
                            {
                                var oSW = SoftwareRepository.FirstOrDefault(t => t.SWId == SQLRZ.RZID);
                                if (oSW != null)
                                {
                                    try
                                    {
                                        AddSoftware oExisting = new AddSoftware()
                                        {
                                            ProductName = oSW.ProductName,
                                            ProductVersion = oSW.ProductVersion,
                                            Manufacturer = oSW.Manufacturer,
                                            ShortName = oSW.ShortName,
                                            Description = oSW.Description,
                                            IconHash = oSW.IconHash,
                                            SWId = oSW.SWId
                                        };

                                        if (SQLRZ.Bootstrap)
                                            oExisting.Author = "BootstrapTrue";
                                        else
                                            oExisting.Author = "BootstrapFalse";

#if DEBUG
                                        System.IO.File.AppendAllLines(Environment.ExpandEnvironmentVariables("%TEMP%\\RZDebug.txt"), new string[] { DateTime.Now.ToString() + ";S2;" + "Installed SWVersion: ", oExisting.ProductName, oExisting.ProductVersion });
#endif
                                        InstalledSoftware.Add(oExisting);
                                    }
                                    catch { }
                                }

                            }
                            catch { }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.IO.File.AppendAllLines(Environment.ExpandEnvironmentVariables("%TEMP%\\RZError.txt"), new string[] { DateTime.Now.ToString() + ";F1814E1" + ex.Message });
                    }
                }

                //Cleanup SW where new Version already exists
                foreach (var oSW in InstalledSoftware)
                {
                    NewSoftwareVersions.RemoveAll(t => t.ShortName == oSW.ShortName);
                }
            }
            catch (Exception ex)
            {
                System.IO.File.AppendAllLines(Environment.ExpandEnvironmentVariables("%TEMP%\\RZError.txt"), new string[] { DateTime.Now.ToString() + ";F1845E1" + ex.Message });
            }
            OnUpdScanCompleted(this, new EventArgs());
            OnSWScanCompleted(this, new EventArgs());

        }
    }
}

