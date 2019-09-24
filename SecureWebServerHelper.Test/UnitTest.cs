using CSE.SecureWebServerHelper;
using NUnit.Framework;
using System;
using System.Security.Cryptography.X509Certificates;

namespace Tests
{
    public class Tests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void InstallTrustedRootCertificate()
        {
            SecureConfigurationHelper.ConfigureRootCAFromKeyVault(TestContext.Parameters["KeyVaultName"], TestContext.Parameters["CertificateName"], TestContext.Parameters["AzureConnectionString"]);

            using (X509Store certStore = new X509Store(StoreName.Root, StoreLocation.CurrentUser))
            {
                certStore.Open(OpenFlags.ReadOnly);
                var certList = certStore.Certificates.Find(X509FindType.FindByThumbprint, TestContext.Parameters["CertificateThumbprint"], false);
                certStore.Close();
                Assert.IsTrue(certList.Count == 1);
            }
        }
    }
}