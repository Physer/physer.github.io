---
layout: post
title: "Accessing Azure Storage services without storing secrets using Azurite, Docker, HTTPS and Azure - Part 2"
date: 2024-08-07 15:00 +0200
categories: azure
---

## Introduction

Welcome back to the blog series about setting up Azurite using HTTPS in Docker!

If you haven't read part 1, you can do so [here]().

In this part of the blog series, we'll focus on setting up an example application using the Azure Storage SDKs to communicate with Azure (or Azurite in this case). Our example application will be a very simple application, returning the first available file in a Blob Container.

In this post I'll be using .NET 8 and C# to communicate with Azure. The principles are the same when using Python, JavaScript or any other language, as long as you're using the [Azure Storage Client Libraries](https://learn.microsoft.com/en-us/azure/storage/common/storage-introduction#storage-apis-libraries-and-tools).

## Setting up an example project

Let's start by creating a new empty .NET project in our project folder (`~/azurite-demo`): `dotnet new web --name demo-app`.

Next we'll verify if everything has been set-up correctly. Navigate to your newly created application: `cd demo-app`.

Execute the `dotnet run` command.

The output should be something similar to:

```
Building...
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5004
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
info: Microsoft.Hosting.Lifetime[0]
      Hosting environment: Development
info: Microsoft.Hosting.Lifetime[0]
      Content root path: /home/alex/azurite-demo/demo-app
```

Verify the application is up and running on the specified URL. In the case of the example, if we navigate to http://localhost:5004, we'll see `Hello World!`:
![empty dotnet application](/assets/images/2024-08-07-azurite-with-https-in-docker/empty-dotnet-application.png)

> Note that our application currently doesn't run using HTTPS. If you wish to do so, feel free but the focus of this blog series is communicating with Azurite through HTTPS, regardless of what the application itself is exposed through.

## Adding the Azure SDK

Now that we've got our project set-up, let's add the Azure SDK libraries required for communicating with our Azure environment (and by extension, Azurite), as well as the necessary Identity library.

Update your project with the following packages:

- [Microsoft.Extensions.Azure](https://github.com/Azure/azure-sdk-for-net/blob/Microsoft.Extensions.Azure_1.7.4/sdk/extensions/Microsoft.Extensions.Azure/README.md)
- [Azure.Identity](https://github.com/Azure/azure-sdk-for-net/blob/Azure.Identity_1.12.0/sdk/identity/Azure.Identity/README.md)
- [Azure.Storage.Blobs](https://github.com/Azure/azure-sdk-for-net/blob/Azure.Identity_1.12.0/sdk/storage/Azure.Storage.Blobs/README.md)

> Note that we're focusing on the Storage library here but all the code surrounding dependency injection and identity applies (to a certain extent) to other Azure services as well such as Service Bus and Key Vault.

You can run the following commands if you want to use the dotnet CLI to add the packages:

```sh
dotnet add package Microsoft.Extensions.Azure
dotnet add package Azure.Identity
dotnet add package Azure.Storage.Blobs
```

Next up is wiring up the Azure SDK using dependency injection. We can follow along with [Microsoft's documentation](https://learn.microsoft.com/en-us/dotnet/azure/sdk/dependency-injection?tabs=web-app-builder) for this part as well.

Microsoft shows the following code to be added to your `Program.cs`.

```csharp
builder.Services.AddAzureClients(clientBuilder =>
{
  clientBuilder.AddBlobServiceClient(new Uri("<storage_url>"));
  clientBuilder.UseCredential(new DefaultAzureCredential());
});
```

We'll change this a little bit so we have a working version first. We'll move on to HTTPS in a later part of this blog series.
Let's stick to the `AddAzureClients` and `AddBlobServiceClient` extension methods, but we'll no longer use the `DefaultAzureCredential`. Instead, we will directly connect to the Azurite Blob service by using the connection string.

The connection data for Azurite can be found in [Microsoft's documentation](https://learn.microsoft.com/en-us/azure/storage/common/storage-use-azurite?tabs=visual-studio%2Cblob-storage#connect-to-azurite-with-sdks-and-tools).The account name and account key are the so called 'Well-known storage account and key'.

Your entire `Program.cs` class should now look something along these lines:

```csharp
using Microsoft.Extensions.Azure;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAzureClients(clientBuilder =>
{
  clientBuilder.AddBlobServiceClient("DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;");
});
var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.Run();
```

Let's quickly add some code so we can interact with a blob. We'll create an endpoint that returns the first Blob it can find.

We'll create a separate endpoint named `/blob` that simply returns the item if it can be found in a HTTP 200 OK result, or an HTTP 200 OK status with a simple message.

> Please let's not have a discussion about the proper use of 200 OKs here 😉

Since we've wired up the `BlobServiceClient` through dependency injection, we can inject it into our endpoint. Our endpoint will create a container called `demo` if it does not exist and grab the first item in that container. It will then be returned as a JSON object to the client.

I've uploaded a PNG image called `azure.png` to my container through the Azure Storage Explorer.

My endpoint looks like this:

```csharp
app.MapGet("/blob", ([FromServices] BlobServiceClient blobServiceClient) =>
{
  BlobContainerClient blobContainerClient = blobServiceClient.GetBlobContainerClient("demo");
  blobContainerClient.CreateIfNotExists();

  var blob = blobContainerClient.GetBlobs()?.FirstOrDefault();
  return blob is null ? Results.Ok("No blob item available") : Results.Ok(blob);
});
```

> Note that if you do not see your `demo` container, you'll have to call the `/blob` endpoint first, in order for the container to be created.

If I now call this newly created endpoint, I get my blob data served back:

```sh
curl http://localhost:5004/blob
{"name":"azure.png","deleted":false,"snapshot":null,"versionId":null,"isLatestVersion":null,"properties":{"lastModified":"2024-08-07T11:55:54+00:00","contentLength":170479,"contentType":"image/png","contentEncoding":null,"contentLanguage":null,"contentHash":"x+qc8arsrSVHp7muQG64tA==","contentDisposition":null,"cacheControl":null,"blobSequenceNumber":null,"blobType":0,"leaseStatus":1,"leaseState":0,"leaseDuration":null,"copyId":null,"copyStatus":null,"copySource":null,"copyProgress":null,"copyStatusDescription":null,"serverEncrypted":true,"incrementalCopy":null,"destinationSnapshot":null,"remainingRetentionDays":null,"accessTier":{},"accessTierInferred":true,"archiveStatus":null,"customerProvidedKeySha256":null,"encryptionScope":null,"tagCount":null,"expiresOn":null,"isSealed":null,"rehydratePriority":null,"lastAccessedOn":null,"eTag":"\"0x1DADFD25E7ADE40\"","createdOn":"2024-08-07T11:55:54+00:00","copyCompletedOn":null,"deletedOn":null,"accessTierChangedOn":"2024-08-07T11:55:54+00:00","immutabilityPolicy":{"expiresOn":null,"policyMode":null},"hasLegalHold":false},"metadata":{},"tags":null,"objectReplicationSourceProperties":null,"hasVersionsOnly":null}
```

Or in a more readable format:

```json
{
  "name": "azure.png",
  "deleted": false,
  "snapshot": null,
  "versionId": null,
  "isLatestVersion": null,
  "properties": {
    "lastModified": "2024-08-07T11:55:54+00:00",
    "contentLength": 170479,
    "contentType": "image/png",
    "contentEncoding": null,
    "contentLanguage": null,
    "contentHash": "x+qc8arsrSVHp7muQG64tA==",
    "contentDisposition": null,
    "cacheControl": null,
    "blobSequenceNumber": null,
    "blobType": 0,
    "leaseStatus": 1,
    "leaseState": 0,
    "leaseDuration": null,
    "copyId": null,
    "copyStatus": null,
    "copySource": null,
    "copyProgress": null,
    "copyStatusDescription": null,
    "serverEncrypted": true,
    "incrementalCopy": null,
    "destinationSnapshot": null,
    "remainingRetentionDays": null,
    "accessTier": {},
    "accessTierInferred": true,
    "archiveStatus": null,
    "customerProvidedKeySha256": null,
    "encryptionScope": null,
    "tagCount": null,
    "expiresOn": null,
    "isSealed": null,
    "rehydratePriority": null,
    "lastAccessedOn": null,
    "eTag": "\"0x1DADFD25E7ADE40\"",
    "createdOn": "2024-08-07T11:55:54+00:00",
    "copyCompletedOn": null,
    "deletedOn": null,
    "accessTierChangedOn": "2024-08-07T11:55:54+00:00",
    "immutabilityPolicy": {
      "expiresOn": null,
      "policyMode": null
    },
    "hasLegalHold": false
  },
  "metadata": {},
  "tags": null,
  "objectReplicationSourceProperties": null,
  "hasVersionsOnly": null
}
```

## Next up

Alright then! Now we've got our Azurite emulating an Azure Blob Storage as well as a small .NET application capable of interacting with these Blobs.

We've also seen how to wire up the Azure SDK with its `BlobServiceClient` through dependency injection.

However, there's a major flaw with this approach at the moment. We've directly entered the Azurite connection string into our `Program.cs` class. This connection string contains an account key. This account key should not be exposed.

Sure, we could simply move this to a configuration file such as the `appsettings.json` files and let the application configuration take care of choosing a different connection string based on its environment, but then your appsettings or your environment where you'd deploy to later on would still need the connection string _somewhere_. Not to mention you'd have to be in charge of rotation keys, which is always a pain in the, well, you know where.

The next step will be to leverage the `DefaultAzureCredential` class from the `Azure.Identity` package to make our code agnostic of both the specific credentials required as well as the _type_ of credential being used. This means that we can use environment variables, interactive credentials or managed identities all with the same bit of code!

Continue to [part 3 here]().
