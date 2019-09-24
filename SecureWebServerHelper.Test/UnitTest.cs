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
            // Local Test
            //Environment.SetEnvironmentVariable("AzureConnectionString", "");
            //Environment.SetEnvironmentVariable("KeyVaultName", "");
            //Environment.SetEnvironmentVariable("CertificateName", "");
            //Environment.SetEnvironmentVariable("CertificateThumbprint", "");
            //
        }

        [Test]
        public void InstallTrustedRootCertificate()
        {
            // Will use values provided in ENV Variable

            SecureConfigurationHelper.ConfigureRootCAFromKeyVault();

            using (X509Store certStore = new X509Store(StoreName.Root, StoreLocation.CurrentUser))
            {
                certStore.Open(OpenFlags.ReadOnly);
                var certList = certStore.Certificates.Find(X509FindType.FindByThumbprint, Environment.GetEnvironmentVariable("CertificateThumbprint"), false);
                certStore.Close();
                Assert.IsTrue(certList.Count == 1);
            }
        }
    }
}