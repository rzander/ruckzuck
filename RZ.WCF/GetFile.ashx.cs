using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;



namespace RuckZuck_API
{
    /// <summary>
    /// Summary description for GetFile
    /// </summary>
    public class GetFile : IHttpHandler
    {

        public void ProcessRequest(HttpContext context)
        {
            try
            {

                int id = Convert.ToInt32(context.Request.QueryString["Id"]);
                if (id == 7654)
                {
                    try
                    {
                        //SWEntitiesApi oApi = new SWEntitiesApi();
                        //oApi.ProductVersionFeedback.Add(new ProductVersionFeedback() { CreationDateTime = DateTime.Now, ProductVersionId = 155425, Working = true, UserKey = "RZ4ConfigMgrSetup", Feedback = context.Request.UserHostName });
                        //oApi.SaveChanges();
                    }
                    catch { }

                    
                    context.Response.Redirect("https://ruckzuck.azurewebsites.net/DL/RZ4ConfigMgrSetup.exe", false);
                    
                }

                if (id == 42135)
                {
                    try
                    {
                        //SWEntitiesApi oApi = new SWEntitiesApi();
                        //oApi.ProductVersionFeedback.Add(new ProductVersionFeedback() { CreationDateTime = DateTime.Now, ProductVersionId = 166221, Working = true, UserKey = "RuckZuck_x64", Feedback = context.Request.UserHostName });
                        //oApi.SaveChanges();
                    }
                    catch { }


                    context.Response.Redirect("https://ruckzuck.azurewebsites.net/DL/RuckZuck.exe", false);

                }

                if (id == 42136)
                {
                    try
                    {
                        //SWEntitiesApi oApi = new SWEntitiesApi();
                        //oApi.ProductVersionFeedback.Add(new ProductVersionFeedback() { CreationDateTime = DateTime.Now, ProductVersionId = 166221, Working = true, UserKey = "RuckZuck_x86", Feedback = context.Request.UserHostName });
                        //oApi.SaveChanges();
                    }
                    catch { }


                    context.Response.Redirect("https://ruckzuck.azurewebsites.net/DL/RuckZuck.exe", false);

                }

                if (id == 134869)
                {
                    try
                    {
                        //SWEntitiesApi oApi = new SWEntitiesApi();
                        //oApi.ProductVersionFeedback.Add(new ProductVersionFeedback() { CreationDateTime = DateTime.Now, ProductVersionId = 218756, Working = true, UserKey = "RuckZuck provider for OneGet_x64", Feedback = context.Request.UserHostName });
                        //oApi.SaveChanges();
                    }
                    catch { }


                    context.Response.Redirect("https://ruckzuck.azurewebsites.net/DL/RuckZuck provider for OneGet_x64.msi", false);

                }
            }
            catch { }
            finally
            {

            }
        } 

        public bool IsReusable
        {
            get
            {
                return false;
            }
        }
    }
}