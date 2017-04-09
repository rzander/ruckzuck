using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Data.SqlClient;

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
                int id = Convert.ToInt32(context.Request.QueryString["Id"]);
                
                string cmdText = "SELECT Icon FROM [ProductVersion] WHERE Id = " + id;
                string myConnection = "Data Source=server.database.windows.net;Initial Catalog=RZDB;User ID=uuu;Password=xxx";
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
            catch { }
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