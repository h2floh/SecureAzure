/************************************************************************************************
The MIT License (MIT)

Copyright (c) Microsoft Corporation

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
***********************************************************************************************/
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.Extensions.Configuration.AzureKeyVault;
using System.Security.Cryptography.X509Certificates;
using System.Net;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace CSE.SecureWebServerHelper
{

    public static class SecureConfigurationHelper
    {
        private static Dictionary<string, KeyVaultClient> keyVaultClients = new Dictionary<string, KeyVaultClient>();
        private static string envCertificateName = Environment.GetEnvironmentVariable("CertificateName");
        private static string envKeyVaultName = Environment.GetEnvironmentVariable("KeyVaultName");
        private static string envAzureConnectionString = Environment.GetEnvironmentVariable("AzureConnectionString");


        /// <summary>
        /// Adding KeyVault to App Configuration, all secrets in keyvault will be available as App configurations
        /// </summary>
        /// <param name="webHostBuilder">IWebHostBuilder</param>
        /// <param name="keyVaultName">Optional keyVaultName, otherwise will be retrieved by ENV 'KeyVaultName'</param>
        /// <param name="azureConnectionString">Optional azureConnectionString, otherwise will be retrieved by ENV 'AzureConnectionString'</param>
        /// <returns>IWebHostBuilder instance</returns>
        public static IWebHostBuilder ConfigurationFromKeyVault(this IWebHostBuilder webHostBuilder, string keyVaultName = null, string azureConnectionString = null)
        {
            var keyVaultClient = PrepareKeyVaultClient(azureConnectionString);
            LoadValues(ref keyVaultName);

            webHostBuilder.
                ConfigureAppConfiguration((context, config) =>
                {
                    // Warning message that if it is not working on development machine.
                    if (envAzureConnectionString == null && azureConnectionString == null)
                    {
                        Console.WriteLine("WARNING: In case of unexpected behavior/authentication errors please specify env variable 'AzureConnectionString' with value 'RunAs=App;AppId={AppId};TenantId={TenantId};AppKey={ClientSecret}' or configure managed identity (if development infra is wihtin Azure).");
                    }

                    config.AddAzureKeyVault(
                        $"https://{keyVaultName}.vault.azure.net/",
                        keyVaultClient,
                        new DefaultKeyVaultSecretManager());
                });

            return webHostBuilder;
        }

        /// <summary>
        /// Adding Azure App Configuration service to App Configuration, all configurations stored in Azure App Configuration will be available as App configurations.
        /// The ideal case is that this connection string is stored in a Key Vault. Please use first .ConfigurationFromKeyVault() method to retrieve the connection string from there.
        /// </summary>
        /// <param name="webHostBuilder">IWebHostBuilder</param>
        /// <param name="azureAppConfigConnectionStringKey">The app configuration parameter to Azure App Configuration connection String (should be stored in KeyVault)</param>
        /// <returns>IWebHostBuilder instance</returns>
        public static IWebHostBuilder ConfigurationFromAzureAppConfig(this IWebHostBuilder webHostBuilder, string azureAppConfigConnectionStringKey = "ConnectionStrings:AppConfig", string refreshSettingsKey = "RefreshSettingsTrigger")
        {

            webHostBuilder.
                ConfigureAppConfiguration((context, config) =>
                {
                    // To grep Azure App Config ConnectionString from config
                    var settings = config.Build();

                    if (settings[azureAppConfigConnectionStringKey] == null)
                    {
                        Console.WriteLine($"Please specify the configuration '{azureAppConfigConnectionStringKey}' for your Azure App Configuration Service, you can save that as a secret in KeyVault and first call extension method ConfigrationFromKeyVault().");
                    }
                    
                    config.AddAzureAppConfiguration(options =>
                    {
                        options.Connect(settings[azureAppConfigConnectionStringKey])
                               .ConfigureRefresh(refresh =>
                               {
                                   refresh.Register(refreshSettingsKey, true);
                               });
                    });
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
        /// <param name="portNumber">Optional portNumber, otherwise Standard HTTPS 443 will be used</param>
        /// <param name="azureConnectionString">Optional azureConnectionString, otherwise will be retrieved by ENV 'AzureConnectionString'</param>
        /// <returns>IWebHostBuilder instance</returns>
        public static IWebHostBuilder ConfigureKestrelSSLFromKeyVault(this IWebHostBuilder webHostBuilder, string keyVaultName = null, string certificateName = null, int portNumber = 443, string azureConnectionString = null)
        {
            var keyVaultClient = PrepareKeyVaultClient(azureConnectionString);
            LoadValues(ref keyVaultName, ref certificateName);

            webHostBuilder
                .ConfigureKestrel((context, options) =>
                {
                    options.Listen(
                        new IPEndPoint(IPAddress.Any, portNumber),
                        listenOptions =>
                        {
                            listenOptions.KestrelServerOptions.AddServerHeader = false;
                            listenOptions.UseHttps(DownloadCertificateFromKeyVault(keyVaultClient, keyVaultName, certificateName));
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
        /// <param name="azureConnectionString">Optional azureConnectionString, otherwise will be retrieved by ENV 'AzureConnectionString'</param>
        /// <returns>IWebHostBuilder instance</returns>
        public static IWebHostBuilder ConfigureRootCAFromKeyVault(this IWebHostBuilder webHostBuilder, string keyVaultName = null, string certificateName = null, string azureConnectionString = null)
        {
            ConfigureRootCAFromKeyVault(keyVaultName, certificateName, azureConnectionString);

            return webHostBuilder;
        }

        /// <summary>
        /// Intended for containerized microservices when you need SSL/HTTPS communication within the cluster. You can install a trusted root certificate
        /// to avoid untrusted cert errors using WebClient connections.
        /// </summary>
        /// <param name="keyVaultName">Optional keyVaultName, otherwise will be retrieved by ENV 'KeyVaultName'</param>
        /// <param name="certificateName">Optional certificateName, otherwise will be retrieved by ENV 'CertificateName'</param>
        /// <param name="azureConnectionString">Optional azureConnectionString, otherwise will be retrieved by ENV 'AzureConnectionString'</param>
        /// <param name="storeName">Optional StoreName for certificate</param>
        /// <param name="storeLocation">Optional StoreLocation for certificate, on Linux only StoreLocation.CurrentUser is allowed see https://github.com/dotnet/corefx/blob/master/Documentation/architecture/cross-platform-cryptography.md</param>
        public static void ConfigureRootCAFromKeyVault(string keyVaultName = null, string certificateName = null, string azureConnectionString = null, StoreName storeName = StoreName.Root, StoreLocation storeLocation = StoreLocation.CurrentUser)
        {
            var keyVaultClient = PrepareKeyVaultClient(azureConnectionString);
            LoadValues(ref keyVaultName, ref certificateName);

            //var certificate = keyVaultClient.GetCertificateAsync($"https://{keyVaultName}.vault.azure.net", certificateName).GetAwaiter().GetResult();
            //var x509Cert = new X509Certificate2(certificate.Cer);

            var certBundle = keyVaultClient.GetSecretAsync($"https://{keyVaultName}.vault.azure.net", certificateName).GetAwaiter().GetResult();
            byte[] certBytes = null;
            if (certBundle.ContentType == "application/x-pkcs12")
            {
                certBytes = Convert.FromBase64String(certBundle.Value);
            }
            else if (certBundle.ContentType == "application/pkix-cert")
            {
                certBytes = JsonConvert.DeserializeObject<PublicKeyCertificate>(certBundle?.Value).Data;
            }

            if (certBytes != null && certBytes.Length > 0)
            {
                var x509Cert = new X509Certificate2(certBytes);

                using (X509Store certStore = new X509Store(storeName, storeLocation))
                {
                    certStore.Open(OpenFlags.ReadWrite);
                    certStore.Add(x509Cert);
                    certStore.Close();
                }
            }
        }

        /// <summary>
        /// We do not know which extension functions will be used so we have to be prepared for every step. Just creates the keyVaultClient with an Azure Bearer Token
        /// Token is subject to Managed Identity or a Connection String which can be supplied for Dev/Test under ENV 'AzureConnectionString'
        /// </summary>
        /// <param name="azureConnectionString">Optional azureConnectionString, otherwise will be retrieved by ENV 'AzureConnectionString'</param>
        /// <returns>KeyVaultClient</returns>
        private static KeyVaultClient PrepareKeyVaultClient(string azureConnectionString = null)
        {
            
            // If a AzureConnectionString is given as parameter use it, otherwise set it to ENV variable value
            // REMARK: If ENV is also null, AzureServiceTokenProvider can still get a valid token via Managed Identity, which is the recommended usage pattern
            if (azureConnectionString == null)
            {
                azureConnectionString = envAzureConnectionString;
            }

            // Setting Dictonary Key value
            var key = "ManagedIdentity";
            if (azureConnectionString != null)
            {
                key = azureConnectionString;
            }

            // Create client only if not yet created by other previous call or connectionString has changed
            if (!keyVaultClients.ContainsKey(key))
            {
                var azureServiceTokenProvider = new AzureServiceTokenProvider(azureConnectionString);
                keyVaultClients.Add(key, new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(
                                                                azureServiceTokenProvider.KeyVaultTokenCallback)));
            }

            return keyVaultClients[key];
        }

        /// <summary>
        /// Loads a X509 Certificate out of KeyVault including the private Key
        /// </summary>
        /// <param name="keyVaultClient">The Key Vault Client to be used</param>
        /// <param name="keyVaultName">Optional keyVaultName, otherwise will be retrieved by ENV 'KeyVaultName'</param>
        /// <param name="certificateName">Optional certificateName, otherwise will be retrieved by ENV 'CertificateName'</param>
        /// <returns>X509Certificate2</returns>
        private static X509Certificate2 DownloadCertificateFromKeyVault(KeyVaultClient keyVaultClient, string keyVaultName, string certificateName)
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

        // class used to access public key certificate stored in Key Vault
        public class PublicKeyCertificate
        {
            public byte[] Data;
        }

    }
}
