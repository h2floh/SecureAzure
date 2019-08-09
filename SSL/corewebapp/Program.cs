using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.Services.AppAuthentication;

namespace corewebapp
{
    public class Program
    {
        private static KeyVaultClient keyVaultClient;
        private static string certificateName = Environment.GetEnvironmentVariable("CertificateName");
        private static string keyVaultName = Environment.GetEnvironmentVariable("KeyVaultName");
        private static string azureConnectionString = Environment.GetEnvironmentVariable("AzureConnectionString");

        public static void Main(string[] args)
        {
            AzureServiceTokenProvider azureServiceTokenProvider;

            if (azureConnectionString != null)
            {
                azureServiceTokenProvider = new AzureServiceTokenProvider(azureConnectionString);
            }
            else 
            {
                azureServiceTokenProvider = new AzureServiceTokenProvider();
            }
            
            keyVaultClient = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(azureServiceTokenProvider.KeyVaultTokenCallback));
            StoreRootCAFromKeyVault();
            CreateHostBuilder(args)
                .Build()
                .Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder
                    .UseStartup<Startup>()
                    .ConfigureKestrel((context, options) =>
                    {
                        const int PortNumber = 5001;
                        
                        options.Listen(
                            new IPEndPoint(IPAddress.Any, PortNumber),
                            listenOptions =>
                            {
                                listenOptions.KestrelServerOptions.AddServerHeader = false;
                                listenOptions.UseHttps(DownloadCertificateFromKeyVault());
                            });
                    });
                });

        private static X509Certificate2 DownloadCertificateFromKeyVault()
        {
            var certificateSecret = keyVaultClient.GetSecretAsync($"https://{keyVaultName}.vault.azure.net", certificateName).GetAwaiter().GetResult();
            var certificate = System.Convert.FromBase64String(certificateSecret.Value);
            return new X509Certificate2(certificate);
        }

        private static void StoreRootCAFromKeyVault()
        {
            var certificate = keyVaultClient.GetCertificateAsync($"https://{keyVaultName}.vault.azure.net", certificateName).GetAwaiter().GetResult();
            var x509Cert = new X509Certificate2(certificate.Cer);

            using (X509Store certStore = new X509Store(StoreName.Root, StoreLocation.CurrentUser))
            {
                certStore.Open(OpenFlags.ReadWrite);
                certStore.Add(x509Cert);
                certStore.Close();
            }
        }
    }
}
