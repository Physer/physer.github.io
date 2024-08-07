---
layout: post
title: "Let's build an Azure Storage solution using Azurite, self-signed certificates, Docker, .NET and Azure - Part 6"
date: 2024-08-07 15:00 +0200
categories: azure
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

We'll also set up an [Azure Container Registry](https://learn.microsoft.com/en-us/azure/container-registry/) and push our image through the registry. If you prefer to use the Docker Hub or any other kind of registry, that's also fine.

Create the registry by running: `az acr create --resource-group rg-azurite --name crazuritedemo --sku Basic`. After the registry's been created, we can use the `az acr` CLI tool to build and push our image directly to the registry. Ensure you're (still) in the root of your project, where the Dockerfile is located (`~/azurite-demo`) and run `az acr build --image azurite-demo/demo-app:v1 --registry crazuritedemo --file Dockerfile .`. This command will allow you to build and push the image using Azure directly. Note that we're not specifying a target now, as we don't want the certificate mumbo-jumbo to come along to Azure.

> You might see some certificate related log messages when applying the `az acr` command. Do not worry though, this intermediate container will not be part of Azure's final image.

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

Now we are going to create a user-assigned managed identity. Contrary to system-assigned managed identities, user-assigned managed identities persist throughout the deletion of resources. System-assigned identities are managed by Azure and bound to a specific resource. For more information about the different types of managed identities, take a look at [Microsoft's documentation](https://learn.microsoft.com/en-us/entra/identity/managed-identities-azure-resources/overview#managed-identity-types).

Let's add a user-assigned managed identity declaration to our Bicep file:

```bicep
resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-07-31-preview' = {
  name: 'id-demo-app'
  location: resourceGroup().location
}
```

Now that we have our user-assigned managed identity in place, we can assign a contributor role on the Blob storage for our identity as well as a role for pulling images from an Azure Container Registry. You can find a list of built-in role definitions over at [Microsoft's documentation](https://learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles).

```Bicep
resource storageAccountDataReaderDefinition 'Microsoft.Authorization/roleDefinitions@2022-05-01-preview' existing = {
  scope: subscription()
  name: '2a2b9908-6ea1-4ae2-8e65-a410df84e7d1'
}

resource containerToStorageAccountRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, managedIdentity.id, storageAccountDataReaderDefinition.id)
  properties: {
    principalId: managedIdentity.properties.principalId
    roleDefinitionId: storageAccountDataReaderDefinition.id
    principalType: 'ServicePrincipal'
  }
}

resource registryPullDefinition 'Microsoft.Authorization/roleDefinitions@2022-05-01-preview' existing = {
  scope: subscription()
  name: '7f951dda-4ed3-4680-a7ca-43fe172d538d'
}

resource containerToRegistryRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, managedIdentity.id, registryPullDefinition.id)
  properties: {
    principalId: managedIdentity.properties.principalId
    roleDefinitionId: registryPullDefinition.id
    principalType: 'ServicePrincipal'
  }
}
```

Next up, we'll create the resource definitions for our .NET application. We're going to use an [Azure Container Instance](https://learn.microsoft.com/en-us/azure/container-instances/) in this tutorial. You could also use an Azure App Service, or an Azure Container App if you so desire. First we'll retrieve the previously created Azure Container Registry so we know where to pull our image from, then we'll set up the Container Instance proper.

```Bicep
resource containerRegistry 'Microsoft.ContainerRegistry/registries@2023-11-01-preview' existing = {
  name: 'crazuritedemo'
}

resource demoAppContainer 'Microsoft.ContainerInstance/containerGroups@2024-05-01-preview' = {
  name: 'ci-demo-app-${uniqueString(resourceGroup().id)}'
  location: resourceGroup().location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentity.id}': {}
    }
  }
  properties: {
    imageRegistryCredentials: [
      {
        server: '${containerRegistry.name}.azurecr.io'
        identity: managedIdentity.id
      }
    ]
    containers: [
      {
        name: 'ci-demo-app-${uniqueString(resourceGroup().id)}'
        properties: {
          image: 'crazuritedemo.azurecr.io/azurite-demo/demo-app:v1'
          environmentVariables: [
            {
              name: 'Storage__ServiceUri'
              value: storageAccount.properties.primaryEndpoints.blob
            }
          ]
          ports: [
            {
              port: 80
              protocol: 'TCP'
            }
          ]
          resources: {
            requests: {
              cpu: 1
              memoryInGB: 1
            }
          }
        }
      }
    ]
    osType: 'Linux'
    restartPolicy: 'OnFailure'
    ipAddress: {
      dnsNameLabel: 'ci-demo-app-${uniqueString(resourceGroup().id)}'
      type: 'Public'
      ports: [
        {
          port: 80
          protocol: 'TCP'
        }
      ]
    }
  }
  dependsOn: [
    containerToRegistryRoleAssignment
    containerToStorageAccountRoleAssignment
  ]
}
```

After creating our Bicep file with our resource declarations, it's time to deploy our resources to Azure. Run the following command: `az deployment group create --resource-group rg-azurite --template-file ./main.bicep`.

For more information about these Bicep declarations, see [Microsoft's Bicep reference](https://learn.microsoft.com/en-us/azure/templates/).

> Of course you don't have to use Bicep. You can use the AZ CLI, as well as the Portal or any other method you prefer!

## Testing our .NET application

Now that we have all our resources in place, let's test our .NET application. If you want to retrieve the name of your container instance, you can run the following command: `az resource list --resource-group rg-azurite --resource-type Microsoft.ContainerInstance/containerGroups --query [0].name`. Once you have the name of your container instance, you can get its URL by running `az container show --resource-group rg-azurite --name ci-demo-app-mf53zb5hnqgto --query ipAddress.fqdn`.

> Replace the name of App Service with your app's name.

!!!! WIP !!!!

You should see the `Hello World!` output from the default endpoint. Navigate to the `/blob` endpoint. Since this is the first time we're looking at this endpoint, it will create the Blob container for us and show `No blob item available`:
![successful call to Azure Blob Storage with managed identity](/assets/images/2024-08-07-azurite-with-https-in-docker/azure-managed-identity-connection.png)

If you want you can upload an item to the Blob container using any of the preferred methods. I'll be using the Azure Storage Explorer built-in to the Azure Portal.
![Azure Portal's Storage Browser](/assets/images/2024-08-07-azurite-with-https-in-docker/azure-portal-storage-browser.png)

After uploading an item and upon refreshing the `/blob` endpoint, you'll see data regarding the uploaded blob!
![Successful Azure managed identity call with Blob data](/assets/images/2024-08-07-azurite-with-https-in-docker/azure-successful-blob-data.png)

## Result and recap

And there you have it!

We have a working version of a .NET application interacting with Azure Storage services. Whether that's emulated through Azurite or on a real Azure Storage Account.

We have accomplished this by running Azurite in Docker, generating a self-signed certificate, trusting said certificate and leveraging Azure's `DefaultAzureCredential` mechanism. After the application was working in our containerized Azurite environment, we've set up the infrastructure required for an Azure Blob Storage service in the cloud as well as a place to host our .NET demo application. By using managed identities, we are able to communicate with our Azure Storage Account without storing any kind of credentials in our code. We no longer have to think about key rotation, security comprises or any other kind of password security issue.

We have followed these steps:

1. Set up Azurite in Docker
2. Set up a small .NET application capable of interacting with the blobs using the Azure SDKs
3. Using the `DefaultAzureCredential` mechanism to authenticate to Azure services
4. Changing Azurite to support HTTPS
5. Containerizing our .NET application
6. Optimizing our containerization process and make use of environment variables
7. Setting up, deploying to and interacting with an actual Azure Storage Account in the cloud

## Finishing up

I hope you have enjoyed our journey through Azure's wondrous worlds of SDKs, authentication and managed identities. Thank you for taking the time to read my blog posts and I hope it will help you out on your cloud endeavors!

As with all my blog posts, the full code is available in the repository of this site: [physer.github.io]().

Thank you and I'll see you in the next one!

## References
