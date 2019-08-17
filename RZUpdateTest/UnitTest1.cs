using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RZUpdate;
using System.Management.Automation;

namespace RZUpdateTest
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestMethodDownloadUpdate()
        {
            Console.WriteLine("Check Update for SCCMCliCtr 1.0.3.9...");
            RZUpdater oRZSW = new RZUpdater();
            var oUpdate = oRZSW.CheckForUpdate("Client Center for Configuration Manager", "1.0.3.9", "Zander Tools");
            Console.WriteLine("found Version:" + oRZSW.SoftwareUpdate.SW.ProductVersion);
            Assert.IsNotNull(oUpdate, "Update detected successfully.");
            Assert.IsNotNull(oUpdate.SW.ContentID, "Update conatins ContentID");
            Console.Write("Downloading files..");
            bool bDLResult = oUpdate.Download().Result;
            Assert.IsTrue(bDLResult, "Files downloaded successfully.");
            if (bDLResult)
                Console.WriteLine("..done.");
            else
                Console.WriteLine("download failed !!");
        }

        [TestMethod]
        public void RUNPS()
        {
            Console.WriteLine("Check RunPS...");

            var oRes = SWUpdate._RunPS("sleep 5", "", new TimeSpan(0, 0, 40));

            oRes.ToString();
            Assert.IsNotNull(oRes);
        }
    }
}
