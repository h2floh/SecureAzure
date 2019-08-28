using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using System;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.Extensions.Configuration.AzureKeyVault;
using System.Security.Cryptography.X509Certificates;
using System.Net;

namespace Microsoft.CSE.SecureWebServerHelper
{
    public static class SecureConfigurationHelper
    {
        private static KeyVaultClient keyVaultClient;
        private static string envCertificateName = Environment.GetEnvironmentVariable("CertificateName");
        private static string envKeyVaultName = Environment.GetEnvironmentVariable("KeyVaultName");
        private static string envAzureConnectionString = Environment.GetEnvironmentVariable("AzureConnectionString");

        /// <summary>
        /// Adding KeyVault to App Configuration, all secrets in keyvault will be available as App configurations
        /// </summary>
        /// <param name="webHostBuilder">IWebHostBuilder</param>
        /// <param name="keyVaultName">Optional keyVaultName, otherwise will be retrieved by ENV 'KeyVaultName'</param>
        /// <returns>IWebHostBuilder instance</returns>
        public static IWebHostBuilder ConfigurationFromKeyVault(this IWebHostBuilder webHostBuilder, string keyVaultName = null)
        {
            PrepareKeyVaultClient();
            LoadValues(ref keyVaultName);

            webHostBuilder.
                ConfigureAppConfiguration((context, config) =>
                {

                    if (context.HostingEnvironment.IsDevelopment() && envAzureConnectionString == null)
                    {
                        Console.WriteLine("please specify env variable 'AzureConnectionString' with value 'RunAs=App;AppId={AppId};TenantId={TenantId};AppKey={ClientSecret}' or configure managed identity (if development infra is wihtin Azure).");
                    }

                    config.AddAzureKeyVault(
                        $"https://{keyVaultName}.vault.azure.net/",
                        keyVaultClient,
                        new DefaultKeyVaultSecretManager());
                });

            return webHostBuilder;
        }

        /// <summary>
        /// Adding Azure App Configuration service to App Configuration, all configurations stored in Azure App Configuration will be available as App configurations
        /// </summary>
        /// <param name="webHostBuilder">IWebHostBuilder</param>
        /// <param name="azureAppConfigConnectionStringKey">The app configuration parameter to Azure App Configuration connection String (should be stored in KeyVault)</param>
        /// <returns>IWebHostBuilder instance</returns>
        public static IWebHostBuilder ConfigurationFromAzureAppConfig(this IWebHostBuilder webHostBuilder, string azureAppConfigConnectionStringKey = "ConnectionStrings:AppConfig")
        {
            PrepareKeyVaultClient();

            webHostBuilder.
                ConfigureAppConfiguration((context, config) =>
                {
                    // To grep Azure App Config ConnectionString from config
                    var settings = config.Build();

                    if (settings[azureAppConfigConnectionStringKey] == null)
                    {
                        Console.WriteLine($"Please specify the configuration '{azureAppConfigConnectionStringKey}' for your Azure App Configuration Service, you can save that as a secret in KeyVault and first call extension method ConfigrationFromKeyVault().");
                    }

                    config.AddAzureAppConfiguration(settings[azureAppConfigConnectionStringKey]);
                });

            return webHostBuilder;
        }

        /// <summary>
        /// Configures Kestrel Webserver with SSL certificate stored in KeyVault. 
        /// The usage is intended for containerized apps in public container registries where the certificates should not be a priori installed
        /// </summary>
        /// <param name="webHostBuilder">IWebHostBuilder</param>
        /// <param name="keyVaultName">Optional keyVaultName, otherwise will be retrieved by ENV 'KeyVaultName'</param>
        /// <param name="certificateName">Optional certificateName, otherwise will be retrieved by ENV 'CertificateName'</param>
        /// <returns>IWebHostBuilder instance</returns>
        public static IWebHostBuilder ConfigureKestrelSSLFromKeyVault(this IWebHostBuilder webHostBuilder, string keyVaultName = null, string certificateName = null)
        {
            PrepareKeyVaultClient();
            LoadValues(ref keyVaultName, ref certificateName);

            webHostBuilder
                .ConfigureKestrel((context, options) =>
                {
                    const int PortNumber = 5001;

                    options.Listen(
                        new IPEndPoint(IPAddress.Any, PortNumber),
                        listenOptions =>
                        {
                            listenOptions.KestrelServerOptions.AddServerHeader = false;
                            listenOptions.UseHttps(DownloadCertificateFromKeyVault(keyVaultName, certificateName));
                        });
                });

            return webHostBuilder;
        }

        /// <summary>
        /// Intended for containerized microservices when you need SSL/HTTPS communication within the cluster. You can install a trusted root certificate
        /// to avoid untrusted cert errors using WebClient connections.
        /// </summary>
        /// <param name="webHostBuilder">IWebHostBuilder</param>
        /// <param name="keyVaultName">Optional keyVaultName, otherwise will be retrieved by ENV 'KeyVaultName'</param>
        /// <param name="certificateName">Optional certificateName, otherwise will be retrieved by ENV 'CertificateName'</param>
        /// <returns>IWebHostBuilder instance</returns>
        public static IWebHostBuilder ConfigureRootCAFromKeyVault(this IWebHostBuilder webHostBuilder, string keyVaultName = null, string certificateName = null)
        {
            PrepareKeyVaultClient();
            LoadValues(ref keyVaultName, ref certificateName);

            var certificate = keyVaultClient.GetCertificateAsync($"https://{keyVaultName}.vault.azure.net", certificateName).GetAwaiter().GetResult();
            var x509Cert = new X509Certificate2(certificate.Cer);

            using (X509Store certStore = new X509Store(StoreName.Root, StoreLocation.CurrentUser))
            {
                certStore.Open(OpenFlags.ReadWrite);
                certStore.Add(x509Cert);
                certStore.Close();
            }

            return webHostBuilder;
        }

        /// <summary>
        /// We do not know which extension functions will be used so we have to be prepared for every step. Just creates the keyVaultClient with an Azure Bearer Token
        /// Token is subject to Managed Identity or a Connection String which can be supplied for Dev/Test under ENV 'AzureConnectionString'
        /// </summary>
        private static void PrepareKeyVaultClient()
        {
            // Create client only if not yet created by other previous call
            if (keyVaultClient == null)
            {
                var azureServiceTokenProvider = new AzureServiceTokenProvider(envAzureConnectionString);
                keyVaultClient = new KeyVaultClient(
                    new KeyVaultClient.AuthenticationCallback(
                        azureServiceTokenProvider.KeyVaultTokenCallback));
            }
        }

        /// <summary>
        /// Loads a X509 Certificate out of KeyVault including the private Key
        /// </summary>
        /// <param name="keyVaultName">Optional keyVaultName, otherwise will be retrieved by ENV 'KeyVaultName'</param>
        /// <param name="certificateName">Optional certificateName, otherwise will be retrieved by ENV 'CertificateName'</param>
        /// <returns>X509Certificate2</returns>
        private static X509Certificate2 DownloadCertificateFromKeyVault(string keyVaultName, string certificateName)
        {
            LoadValues(ref keyVaultName, ref certificateName);

            var certificateSecret = keyVaultClient.GetSecretAsync($"https://{keyVaultName}.vault.azure.net", certificateName).GetAwaiter().GetResult();
            var certificate = System.Convert.FromBase64String(certificateSecret.Value);
            return new X509Certificate2(certificate);
        }

        /// <summary>
        /// Helper function to either use the parameter or the environment variable value
        /// </summary>
        /// <param name="keyVaultName">after this operation keyVaultName will have it's value or if not set will get the ENV 'KeyVaultName' value</param>
        /// <param name="certificateName">after this operation certificateName will have it's value or if not set will get the ENV 'CertificateName'</param>
        private static void LoadValues(ref string keyVaultName, ref string certificateName)
        {
            if (keyVaultName == null)
            {
                keyVaultName = envKeyVaultName;
            }
            if (certificateName == null)
            {
                certificateName = envCertificateName;
            }

            // Check for values
            if (keyVaultName == null)
            {
                Console.WriteLine("No value for KeyVaultName detected, please either set the ENV 'KeyVaultName' or pass the value as parameter to your KeyVault");
            }
            if (certificateName == null)
            {
                Console.WriteLine("No value for CertificateName detected, please either set the ENV 'CertificateName' or pass the value as parameter for requested certificate name within KeyVault");
            }
        }

        /// <summary>
        /// Helper function to either use the parameter or the environment variable value
        /// </summary>
        /// <param name="keyVaultName">after this operation keyVaultName will have it's value or if not set will get the ENV 'KeyVaultName' value</param>
        private static void LoadValues(ref string keyVaultName)
        {
            if (keyVaultName == null)
            {
                keyVaultName = envKeyVaultName;
            }
            // Check for values
            if (keyVaultName == null)
            {
                Console.WriteLine("No value for KeyVaultName detected, please either set the ENV 'KeyVaultName' or pass the value as parameter to your KeyVault");
            }
        }

    }
}
