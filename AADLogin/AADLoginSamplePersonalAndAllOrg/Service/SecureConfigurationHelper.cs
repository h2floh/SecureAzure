using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.Extensions.Configuration.AzureKeyVault;
using System.Security.Cryptography.X509Certificates;
using System.Net;

namespace AADLoginSamplePersonalAndAllOrg.Service
{
    public static class SecureConfigurationHelper
    {
        private static KeyVaultClient keyVaultClient;
        private static string certificateName = Environment.GetEnvironmentVariable("CertificateName");
        private static string keyVaultName = Environment.GetEnvironmentVariable("KeyVaultName");
        private static string azureConnectionString = Environment.GetEnvironmentVariable("AzureConnectionString");

        public static IWebHostBuilder ConfigurationFromKeyVaultAndAzureAppConfig(this IWebHostBuilder webHostBuilder, Action<WebHostBuilderContext, IConfigurationBuilder> configureDelegate)
        {
            PrepareKeyVault();
            

            webHostBuilder.
                ConfigureAppConfiguration((context, config) =>
                {

                    if (context.HostingEnvironment.IsDevelopment() && azureConnectionString == null)
                    {
                        Console.WriteLine("please specify env variable 'AzureConnectionString' with value 'RunAs=App;AppId={AppId};TenantId={TenantId};AppKey={ClientSecret}' or configure managed identity (if development infra is wihtin Azure).");
                    }

                    config.AddAzureKeyVault(
                        $"https://{keyVaultName}.vault.azure.net/",
                        keyVaultClient,
                        new DefaultKeyVaultSecretManager());

                    // To grep ConnectionString from KeyVault
                    var settings = config.Build();

                    config.AddAzureAppConfiguration(settings["ConnectionStrings:AppConfig"]);
                });

            return webHostBuilder;
        }
       
        public static IWebHostBuilder ConfigureKestrelSSLFromKeyVault(this IWebHostBuilder webHostBuilder, Action<WebHostBuilderContext, Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions> configureDelegate)
        {
            StoreRootCAFromKeyVault();

            webHostBuilder
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

            return webHostBuilder;
        }

        private static void PrepareKeyVault()
        {
            if (keyVaultName == null)
            {
                Console.WriteLine("Please specify env variable 'KeyVaultName' with the name of your keyvault.");
            }

            var azureServiceTokenProvider = new AzureServiceTokenProvider(azureConnectionString);
            keyVaultClient = new KeyVaultClient(
                new KeyVaultClient.AuthenticationCallback(
                    azureServiceTokenProvider.KeyVaultTokenCallback));
        }

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
