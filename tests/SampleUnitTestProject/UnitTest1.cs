using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SampleUnitTestProject
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestMethod()
        {
        }

        [DataTestMethod]
        [DataRow(1)]
        [DataRow(2)]
        public void DataTestMethod(int parameter)
        {
        }

        [TestMethod]
        public void TestMethodThatFails()
        {
            TestContext.WriteLine("Faily test fails");
            TestContext.AddResultFile("C:\\TeamCity\\Repos\\AzurePipelines.TestLogger\\LICENSE");
            Assert.Fail("This test is expected to fail");
        }

        [TestMethod]
        public void TestMethodThatIsDeliberatelyFlakey()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("Flakey")))
            {
                TestContext.WriteLine("Flakey test is flakey");
                TestContext.AddResultFile("C:\\TeamCity\\Repos\\AzurePipelines.TestLogger\\README.md");
                Assert.Fail("Flakey");
            }
        }

        [TestCleanup]
        public void TestCleanup()
        {
            if (TestContext.CurrentTestOutcome != UnitTestOutcome.Passed && File.Exists("flakey.txt"))
            {
                TestContext.AddResultFile("flakey.txt");
            }
        }

        [AssemblyCleanup]
        public static void AssemblyCleanup()
        {
            if (File.Exists("flakey.txt"))
            {
                File.Delete("flakey.txt");
            }
        }


        private TestContext m_testContext;
        public TestContext TestContext
        {
            get { return m_testContext; }

            set
            {
                m_testContext = value;
            }
        }

    }
}
