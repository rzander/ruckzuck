using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PackageManagement;

namespace RZOneGetTest
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void FindPackage()
        {
            var pkg = new PackageProvider();
            var oRes = pkg.RunPS("Find-Package -Provider RuckZuck -Name sccmclictr");
            Assert.AreEqual(pkg.PackageProviderName, "RuckZuck");
            Assert.IsNotNull(oRes);
            if(oRes != null)
            {
                Console.Write("Latest Version of 'sccmclictr': ");
                string sVersion = (((System.Management.Automation.PSProperty)oRes[0].Properties["Version"]).Value as string) ?? "";
                Console.WriteLine(sVersion);
                Assert.AreNotEqual(sVersion, "");
            }
        }
    }
}
