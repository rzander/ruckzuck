using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RZUpdate;

namespace RZUpdateTest
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestMethodDownloadUpdate()
        {
            RZUpdater oRZSW = new RZUpdater();
            var oUpdate = oRZSW.CheckForUpdate("Client Center for Configuration Manager", "1.0.3.9", "Zander Tools");
            
            Assert.IsNotNull(oUpdate, "Update detected successfully.");
            Assert.IsNotNull(oUpdate.SW.ContentID, "Update conatins ContentID");
            Assert.IsTrue(oUpdate.Download().Result, "Files downloaded successfully.");
        }
    }
}
