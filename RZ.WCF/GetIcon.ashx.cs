using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Data.SqlClient;
using System.IO;

namespace RuckZuck_WCF
{
    /// <summary>
    /// Summary description for Handler1
    /// </summary>
    public class GetIcon : IHttpHandler
    {

        public void ProcessRequest(HttpContext context)
        {
            try
            {
                context.Response.ContentType = "image/jpeg";
                int iconid = Convert.ToInt32(context.Request.QueryString["Id"]);

                if (File.Exists(@".\Data\Icons\" + iconid.ToString() + ".jpg"))
                {
                    using (MemoryStream ms = new MemoryStream())
                    {
                        File.Open(HttpContext.Current.Server.MapPath("~") + @"\Data\Icons\" + iconid.ToString() + ".jpg", FileMode.Open).CopyTo(ms);
                        context.Response.BinaryWrite(ms.ToArray());
                    }
                }
                else
                {
                    int id = Convert.ToInt32(iconid);

                    RZService oSVC = new RZService();
                    
                    var oSW = oSVC.GetSWDetail(id);
                    if (oSW.Image != null)
                    {
                        byte[] image = oSW.Image;

                        MemoryStream ms = new MemoryStream(image);
                        try
                        {
                            var sIcon = new System.IO.FileStream(HttpContext.Current.Server.MapPath("~") + @"\Data\Icons\" + iconid.ToString() + ".jpg", FileMode.Create);
                            ms.CopyTo(sIcon);
                            sIcon.FlushAsync();
                        }
                        catch { }

                        context.Response.BinaryWrite(image);
                    }

                }
            }
            catch { }
            /*try
            {
                int id = Convert.ToInt32(context.Request.QueryString["Id"]);
                
                string cmdText = "SELECT Icon FROM [ProductVersion] WHERE Id = " + id;
                string myConnection = "Data Source=server.database.windows.net;Initial Catalog=xxxx;User ID=xxxx;Password=xxxxx";
                SqlConnection connection = new SqlConnection(myConnection);
                SqlCommand command = new SqlCommand(cmdText, connection);
                try
                {
                    context.Response.ContentType = "image/jpeg";


                    connection.Open();

                    SqlDataReader reader = command.ExecuteReader();
                    reader.Read();
                    byte[] image = (byte[])reader.GetValue(0);
                    context.Response.BinaryWrite(image);
                    reader.Close();
                }
                catch { }
                finally
                {
                    connection.Close();
                }
            }
            catch { }*/
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