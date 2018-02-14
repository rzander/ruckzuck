using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RZUpdate;

namespace RZUpdateTest
{
    [TestClass]
    public class UnitTest1
    {
        private TestContext tr;

        [TestMethod]
        public void TestMethodDownloadUpdate()
        {
            tr.WriteLine("Check Update for SCCMCliCtr 1.0.3.9...");
            RZUpdater oRZSW = new RZUpdater();
            var oUpdate = oRZSW.CheckForUpdate("Client Center for Configuration Manager", "1.0.3.9", "Zander Tools");
            tr.WriteLine("found Version:" + oRZSW.SoftwareUpdate.SW.ProductVersion);
            Assert.IsNotNull(oUpdate, "Update detected successfully.");
            Assert.IsNotNull(oUpdate.SW.ContentID, "Update conatins ContentID");
            tr.WriteLine("Downloading files..");
            bool bDLResult = oUpdate.Download().Result;
            Assert.IsTrue(bDLResult, "Files downloaded successfully.");
            if (bDLResult)
                tr.WriteLine("..done.");
            else
                tr.WriteLine("download failed !!");
        }
    }
}
