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
                if (oRes.Count > 0)
                {
                    Console.Write("Latest Version of 'sccmclictr': ");
                    string sVersion = (((System.Management.Automation.PSProperty)oRes[0].Properties["Version"]).Value as string) ?? "";
                    Console.WriteLine(sVersion);
                    Assert.AreNotEqual(sVersion, "");
                }
            }
        }

        [TestMethod]
        public void InstallPackage()
        {
            var pkg = new PackageProvider();
            {
                var oRes = pkg.RunPS("Install-Package -Provider RuckZuck -Name vcredist2019x64");
                Assert.AreEqual(pkg.PackageProviderName, "RuckZuck");
                Assert.IsNotNull(oRes);
                if (oRes != null)
                {
                    if (oRes.Count > 0)
                    {
                        Console.Write("Latest Version of 'vcredist2019x86': ");
                        string sVersion = (((System.Management.Automation.PSProperty)oRes[0].Properties["Version"]).Value as string) ?? "";
                        Console.WriteLine(sVersion);
                        Assert.AreNotEqual(sVersion, "");
                    }
                }
            }
            {
                var oRes = pkg.RunPS("Install-Package -Provider RuckZuck -Name vcredist2019x64");
                Assert.AreEqual(pkg.PackageProviderName, "RuckZuck");
                Assert.IsNotNull(oRes);
                if (oRes != null)
                {
                    if (oRes.Count > 0)
                    {
                        Console.Write("Latest Version of 'vcredist2019x64': ");
                        string sVersion = (((System.Management.Automation.PSProperty)oRes[0].Properties["Version"]).Value as string) ?? "";
                        Console.WriteLine(sVersion);
                        Assert.AreNotEqual(sVersion, "");
                    }
                }
            }
        }
    }
}
