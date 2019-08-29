# AAD Login ASP.NET & Azure Key Vault Config & Azure App Configuration & Kestrel SSL

We will do 4 things in this tutorial/sample:
1. We will connect the App to a KeyVault where we can store all relevant application secrets and certificates
2. We will connect Azure App Configuration Service where we can store all other application settings (yes, appsettings.json will be empty)
3. We will generate and retrieve a SSL self-signed certificate in Key Vault and use it that we can communicate with the APP via HTTPS
4. We will use Azure Active Directory Login and Role mechanism to log in to the App and assign users in AAD roles to the application.


## Create Azure App Configuration

As long as Azure App Configuration Service is in preview you have to register the provider first.
```bash
az provider register --namespace Microsoft.AppConfiguration
```
That will take around 5min and then you should be able to create your Azure App Configuration Service:
```bash
az appconfig create -g <YourResourceGroupName> --name  <YourAppConfigName> --location southeastasia
```
For this example you have to import the settings from __initial_app_settings.json__ file:
```bash
az appconfig kv import -n <YourAppConfigName> --source file --path .\initial_app_settings.json --format json --separator '-'
```

## Create a KeyVault and Set Azure App Configuration Secret
Now it is time to create a KeyVault to store the Connection String of the App Configuration Service there:
```bash
az keyvault create --resource-group <YourResourceGroupName> --name  <YourKeyVaultName> --location koreacentral
```
With the following command we can import the connectionString to KeyVault, we will name the secret `ConnectionStrings--AppConfig`:
```bash
az keyvault secret set --vault-name <YourKeyVaultName> --name "ConnectionStrings--AppConfig" --value $(az appconfig credential list -n <YourAppConfigName>  --query '[0].connectionString' -o tsv)
```

Since we also want to run the ASP.NET app with a self signed HTTPS certificate we will create one too. To reduce complexity of the tutorial we will use the same KeyVault. <br/>
For details on Kestrel SSL please refer [here](../SSL/README.md).
```bash
az keyvault certificate create --vault-name <YourKeyVaultName> --name LocalhostK8s --policy "@..\SSL\k8s_localhost_policy.json"
```

## Create Azure Active Directory Application, Service Principal and additional settings
We will now create an Azure Active Directory application and directly adding roles which are used for authorization within the webapp code. This will be needed in order to distinguish between a normal and an admin user within the ASP.NET app.
```bash
az ad app create --display-name AADLoginSamplePersonalAndAllOrg --app-roles "@app_roles_manifest.json" --available-to-other-tenants true  
$appId=$(az ad app list --display-name AADLoginSamplePersonalAndAllOrg --query '[0].appId' -o tsv)
```
Now we create and save the application password (often referred as client secret). We will also save it in KeyVault, so you can look it up there. Or echoing for your reference.
```bash
$password=$(az ad app credential reset --id $appId --query 'password' -o tsv)
echo $password
az keyvault secret set --vault-name <YourKeyVaultName> --name "AzureAd--ClientSecret" --value $password
```

We will also need to add a service principal (otherwise app is not available under Enterprise Applications to assign users and groups to roles).
```bash
az ad sp create --id $appId
```

Now we will give the app/service principal also read rights to our Key Vault (we need rights for certificates and secrets):
```bash
az keyvault set-policy --name <YourKeyVaultName> --spn $appId --certificate-permissions get list --secret-permissions get list
```

Last but not least we have to change several application properties
```bash
az ad app update --id $appId --set logoutUrl=https://localhost:5001/signout-oidc
```

### Change Properties of Application in Azure Portal
There are several properties which currently can not be defined via Azure CLI. You can use Powershell or Azure Portal to set following values (in the Application manifest file).<br/>

```json
"replyUrlsWithType": [
        {
                "url": "https://localhost:5001/signin-oidc",
                "type": "Web"
        },
        {
                "url": "https://localhost:5001/",
                "type": "Web"
        }
],
...
"signInAudience": "AzureADandPersonalMicrosoftAccount",
```

For your reference the possible values for _signInAudience_ are:
• AzureADMyOrg - Users with a Microsoft work or school account in my organization’s Azure AD tenant (i.e. single tenant)
• AzureADMultipleOrgs - Users with a Microsoft work or school account in any organization’s Azure AD tenant (i.e. multi-tenant)
AzureADandPersonalMicrosoftAccount - Users with a personal Microsoft account, or a work or school account in any organization’s Azure AD tenant

Now we finished the preperation and can execute/start the app.

## Docker build

Navigate to to folder called `AADLogin` and execute a docker build. 
```bash
cd AADLogin
docker build . -t <yourrepo>/aadloginsample:latest
```

## Test Application in Container

If you did not recorded the Client Secret aka AppKey aka Application Password please look it up with this command:
```bash
az keyvault secret show --vault-name <YourKeyVaultName> --name "AzureAd--ClientSecret"
```
Now replace all values and run the container and access it over `https://localhost:5001` if you do not import the certificate to your CA root you will get an untrusted error/warning.
```bash
docker run -it -e CertificateName=LocalhostK8s -e KeyVaultName=<YourKeyVaultName> -e AzureConnectionString="RunAs=App;AppId=<YourAppId>;TenantId=<YourTenantId>;AppKey=<YourAppKey>" -p 5001:443 <yourrepo>/aadloginsample:latest
```