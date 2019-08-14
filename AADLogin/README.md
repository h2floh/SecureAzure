# AAD Login ASP.NET & Azure Key Vault Config & Azure App Configuration & Kestrel SSL

TODO: Clean up and describe steps in detail.


## Create Azure App Configuration
```
az appconfig create -g securewebapp --name  <YourAppConfigName> --location southeastasia
```
```
az appconfig kv import -n <YourAppConfigName> --source file --path .\initial_app_settings.json --format json --separator '-'
```

## Create a KeyVault
```
az keyvault create --resource-group securewebapp --name  <YourKeyVaultName> --location koreacentral
```

## Set Azure App Configuration Secret
```
az keyvault secret set --vault-name <YourKeyVaultName> --name "ConnectionStrings--AppConfig" --value $(az appconfig credential list -n <YourAppConfigName>  --query '[0].connectionString' -o tsv)
```

Generate SSL certificate for webserver. For details on Kestrel SSL see [here](../SSL/README.md).
```
az keyvault certificate create --vault-name <YourKeyVaultName> --name LocalhostK8s --policy "@..\SSL\k8s_localhost_policy.json"
```

## App ID from seperate new app with app roles etc...
We will now create an Azure Active Directory application and directly adding roles which are used for authorization within the webapp code.
```
az ad app create --display-name AADLoginSamplePersonalAndAllOrg --app-roles "@app_roles_manifest.json" --available-to-other-tenants true  
$appId = $(az ad app list --display-name AADLoginSamplePersonalAndAllOrg --query '[0].appId' -o tsv)
```

When to set password???
```
az ad sp credential reset --name $appId
```

We will also need to add a service principal (otherwise app is not available under Enterprise Applications to assign users and groups to roles)
```
az ad sp create --id $appId
```

Now we are changing some properties of the App
```
az ad app update --id $appId --set logoutUrl=https://localhost:44363/signout-oidc
```

### Change Properties of Application in Azure Portal
There are several properties which currently can not be defined via Azure CLI. You can use Powershell or Azure Portal to set following values (in the Application manifest file).<br/>

```
"replyUrlsWithType": [
        {
                "url": "https://localhost:44363/signin-oidc",
                "type": "Web"
        },
        {
                "url": "https://localhost:44363/",
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

## Add rights to new app on keyvault

```
az keyvault set-policy --name <YourKeyVaultName> --spn $appId --certificate-permissions get list --secret-permissions get list
```


## Set AzureAD Client Secret
```
az keyvault secret set --vault-name <YourKeyVaultName> --name "AzureAd--ClientSecret" --value "<AppClientSecret>"
```

## Docker build
```
docker build . -t <yourrepo>/aadloginsample:latest
```

## Test Application in Container

```
docker run -it -e CertificateName=LocalhostK8s -e KeyVaultName=<YourKeyVaultName> -e AzureConnectionString="RunAs=App;AppId=<YourAppId>;TenantId=<YourTenantId>;AppKey=<YourAppKey>" -p 44363:5001 <yourrepo>/aadloginsample:latest
```