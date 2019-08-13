# AAD Login ASP.NET & Azure Key Vault Config & Azure App Configuration & Kestrel SSL

TODO: Clean up and describe steps in detail.


## Create Azure App Configuration
```
az appconfig create -g securewebapp --name  <YourAppConfigName> --location southeastasia
```
```
az appconfig kv import -n <YourAppConfigName> -s file --path .\initial_app_settings.json --format json
```
Manual Import because of preview limitation

## Like SSL create Keyvault
https://github.com/h2floh/AzureSecure/tree/master/SSL
For details on Kestrel SSL see [here](../SSL/README.md)

## And SSL Certificate
```
az keyvault create --resource-group securewebapp --name  <YourKeyVaultName> --location koreacentral
```
```
az keyvault certificate create --vault-name  <YourKeyVaultName> --name LocalhostK8s --policy "@..\SSL\k8s_localhost_policy.json"
```

## App ID from seperate new app with app roles etc...

```
az ad app create --display-name AADLoginSamplePersonalAndAllOrg --identifier-uris https://localhost --app-roles "@app_roles_manifest.json" --available-to-other-tenants true  
```

### Change Properties of app
signInAudience=AzureADandPersonalMicrosoftAccountProperty 'signInAudience' is read-only and cannot be set.

Add in Portal

Redirect URLs 
LogoutURL
Change "signInAudience": "AzureADandPersonalMicrosoftAccount",

• AzureADMyOrg - Users with a Microsoft work or school account in my organization’s Azure AD tenant (i.e. single tenant)
• AzureADMultipleOrgs - Users with a Microsoft work or school account in any organization’s Azure AD tenant (i.e. multi-tenant)
AzureADandPersonalMicrosoftAccount - Users with a personal Microsoft account, or a work or school account in any organization’s Azure AD tenant

Service Principal missing
```
az ad add service pricinpal command 
```

### Add Graph Permissions (not needed for this one, check again)
ADD  Delegation Directory.Read.All, User.Read, User.ReadBasic.All

```
az ad app permission add --id <YourAppId> --api
        00000003-0000-0000-c000-000000000000 --api-permissions
       06da0dbc-49e2-44d2-8312-53f166ab848a=Scope e1fe6dd8-ba31-4d61-89e7-88639da4683d=Scope b340eb25-3456-403f-be2f-af7a0d370277=Scope
```

## Add rights to new app on keyvault

```
az keyvault set-policy --name  <YourKeyVaultName> --spn 1db0da9a-80cf-448b-9894-1ec86cb8f331 --certificate-permissions get list --secret-permissions get list
```

## Set Azure App Configuration Secret

```
az keyvault secret set --vault-name  <YourKeyVaultName> --name "ConnectionStrings--AppConfig" --value $(az 
appconfig credential list -n <YourAppConfigName>  --query '[0].connectionString' -o tsv)
```

## Set AzureAD Client Secret
```
az keyvault secret set --vault-name  <YourKeyVaultName> --name "AzureAd--ClientSecret" --value "<AppClientSecret>"
```

## Docker build
```
docker build . -t <yourrepo>/aadloginsample:latest
```

## Test Application in Container

```
docker run -it -e CertificateName=LocalhostK8s -e KeyVaultName=<YourKeyVaultName> -e AzureConnectionString="RunAs=App;AppId=<YourAppId>;TenantId=<YourTenantId>;AppKey=<YourAppKey>" -p 44363:5001 <yourrepo>/aadloginsample:latest
```