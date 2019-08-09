# Create KeyVault with self signed localhost and wildcard *.cluster.local certificate
This is a preparation step for HTTPS SSL communication between (Micro)services in a Kubernetes Cluster from a one-click deployment (can not know the service names or IPs beforehand).
<br/><br/>
Main idea:
1. Generate a self-signed wildcard X.509 Certificate with KeyVault (and store and manage it there)
2. Use Azure's managed identity to authenticate to the KeyVault (for test in local container I will add guidance on using a service principal)
3. Retrieve the Certificate as RootCA and store it in the containers cert store for trusted SSL/HTTPS communication within the cluster (it is even safer than change the settings to accept untrusted SSL connections)
4. Start the own webserver instance with the self-signed certificate
<br/>
Prerequisites
2
1. Azure Subscription
2. Azure CLI (latest version - I tested below commands with 2.0.69)

## Create KeyVault (and resource group)
`REMARK`: _\<YourKeyVaultName>_ has to be unique within Azure.
```
az login
az group create --name securewebapp --location koreacentral
az keyvault create --resource-group securewebapp --name <YourKeyVaultName> --location koreacentral
```

## Create Certificate
From this github's base path:
```
cd SSL
az keyvault certificate create --vault-name <YourKeyVaultName> --name LocalhostK8s --policy "@k8s_localhost_policy.json"
```

## Create Service Principal (for test in local container)
You should use managed identity within Azure
You can login into Azure CLI with your user and execute the apps in that context without the principal
```
az ad sp create-for-rbac --name securewebapp
```
result should look like this
```
{
  "appId": "7b9a4d8a-0000-4ef2-bbb3-189a745cf434",
  "displayName": "securewebapp",
  "name": "http://securewebapp",
  "password": "2ca84dde-0000-4ddd-ad24-4ba8cc3e9672",
  "tenant": "001ceeb6-0000-42e0-88fb-0e388f8c6675"
}
```
You will need appId _\<YourAppId>_, password _\<YourAppKey>_ and tenant _\<YourTenantId>_ later.

## Add certificate access rights to Service Principal (for test in local container)
```
az keyvault set-policy --name <YourKeyVaultName> --spn <YourAppId> --certificate-permissions get list --secret-permissions get list
```

## Build Container
From this github's base path:
```
cd SSL/corewebapp
docker build . -t <yourrepo>/securewebapp:latest
```

## Run Container
```
docker run -it -e CertificateName=LocalhostK8s -e KeyVaultName=<YourKeyVaultName> -e AzureConnectionString="RunAs=App;AppId=<YourAppId>;TenantId=<YourTenantId>;AppKey=<YourAppKey>" -p 5001:5001 <yourrepo>/securewebapp:latest
```

## Install the CA on your dev machine
If you want that the RootCA is installed on your dev machine you just need to build and run the application once on your dev machine. 
You need to have .NET Core 3.0 (SDK & ASP Runtime) installed. 
<br/>
Prepare your ENV variables and login to Azure CLI:
```
setx KeyVaultName <YourKeyVaultName>
setx CertificateName LocalhostK8s
az login
```
As an alternative you could export the connection string instead of login. But not good security practice. <br/>The main goal will be to skip this also in the container when we run it in ACI or AKS and using Managed Identity instead
```
setx AzureConnectionString "RunAs=App;AppId=<YourAppId>;TenantId=<YourTenantId>;AppKey=<YourAppKey>" 
```
<br/>From this github's base path:
```
cd SSL/corewebapp
dotnet restore
dotnet build
dotnet run
```
If you are using Windows you will see a request if you accept to store the certificate.