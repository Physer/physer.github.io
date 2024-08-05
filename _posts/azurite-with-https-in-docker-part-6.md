---
# layout: post
# title: "Azurite, HTTPS, Azure Storage SDKs, Azure Storage Explorer and Docker - Part 6"
# date: 2023-07-23 11:40 +0200
# categories: azure
---

## Introduction

Part 6! The final part of this _slightly_ overblown series on how to use the DefaultAzureCredential with Azurite and Azure over HTTPS!

Welcome to this post and thank you for taking the time to read this series.

In this final part we're going to set up the Azure resources for our blob storage, our .NET application and deploy our code. We will use managed identities to connect from our .NET application to Azure. All the while without storing any kind of access tokens or credentials in our code or environment variables (such as a connection string).

> This part of the series requires you to have an Azure account with a valid subscription.

## Setting up Azure resources

Make sure you're installed the [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli).

Log in to your Azure account by running `az login`. Don't forget to select the right subscription after logging in (if applicable).

Next up we'll create a resource group by running `az group create --location westeurope --name rg-azurite`.

> If you want to create a resource group in a different location or with a different name, you're of course free to do so.

The rest of the resources will be created through [Bicep](https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/overview?tabs=bicep). I highly recommend using Visual Studio Code with the [Bicep extension](https://marketplace.visualstudio.com/items?itemName=ms-azuretools.visualstudiobicep) from Microsoft, this makes it a breeze to author Bicep files.

Create a new Bicep file in the root folder of your project called `main.bicep` (`~/azurite-demo/main.bicep`). Once created, we'll add our Storage Account resource definition to it.

```Bicep
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: 'stazurite${uniqueString(resourceGroup().id)}'
  location: resourceGroup().location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
}
```

Due to a storage account requiring a globally unique name, I prefer to append the hashed version of the resource group ID to it. If you prefer to use a different name, or different values for the SKU - that's completely fine.

Next up, we'll create the resource definitions for our .NET application. We're going to use an [Azure App Service](https://learn.microsoft.com/en-us/azure/app-service/) in this tutorial. You could also use an Azure Container Instance, or an Azure Container App if you so desire.

```Bicep
resource demoAppPlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: 'asp-demo-app-${uniqueString(resourceGroup().id)}'
  location: resourceGroup().location
  sku: {
    name: 'B1'
  }
  kind: 'linux'
  properties: {
    reserved: true
  }
}

resource demoApp 'Microsoft.Web/sites@2023-12-01' = {
  name: 'app-demo-app-${uniqueString(resourceGroup().id)}'
  location: resourceGroup().location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: demoAppPlan.id
    httpsOnly: true
    siteConfig: {
      appSettings: [
        {
          name: 'Storage__ServiceUri'
          value: storageAccount.properties.primaryEndpoints.blob
        }
      ]
    }
  }
}
```

I'm choosing a Linux app service plan on the B1 SKU. The web app itself will have a system-assigned identity enabled and our Blob endpoint set to the value of the `Storage__ServiceUri` environment variable. Note that if you wish to create a Linux app service plan, it's important that you set both the `kind` value to `linux` as well as the `reserved` property to `true`.

Once we have our Storage Account and our App Service with a system-assigned managed identity, we can assign a reader role on the Blob storage for our identity:

```Bicep
resource storageAccountDataReaderDefinition 'Microsoft.Authorization/roleDefinitions@2022-05-01-preview' existing = {
  scope: subscription()
  name: '2a2b9908-6ea1-4ae2-8e65-a410df84e7d1'
}

resource appServiceRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: storageAccount
  name: guid(storageAccount.id, demoApp.id, storageAccountDataReaderDefinition.id)
  properties: {
    principalId: demoApp.identity.principalId
    roleDefinitionId: storageAccountDataReaderDefinition.id
    principalType: 'ServicePrincipal'
  }
}
```

## Deploying the .NET application

Now that we have all our resources in place, let's deploy our application to Azure. In this scenario I'll be using the `dotnet` CLI to publish the application to my local file system after which I'll be using the [ZIP deploy](https://learn.microsoft.com/en-us/azure/app-service/deploy-zip?tabs=cli#create-a-project-zip-package) functionality to upload it to the Azure App Service. If you wish to use a different way of deploying your application, e.g. through Visual Studio, that will work just as well.

Let's navigate to our `demo-app` folder: `~/azurite-demo/demo-app`. Publish the application by running the `publish` command with the `dotnet` CLI: `dotnet publish --configuration Release --output ./publish`

Navigate to the newly created `publish` folder (`~/azurite-demo/demo-app/publish`) and ZIP all the files in this directory: `zip -r demo-app.zip .`.

> You might need to install `zip` on your machine (`sudo apt install zip -y`).

Once all the published files are zipped, we can use the Azure CLI to deploy our application to our previously created Azure App Service: `az webapp deploy --resource-group rg-azurite --name app-demo-app-mf53zb5hnqgto --src-path ./demo-app.zip`. Be sure to replace the name of the web app with the name of your web app.

> If you want to retrieve the name of your web app, you can run the following command: `az resource list --resource-group rg-azurite --resource-type Microsoft.Web/sites --query [0].name`