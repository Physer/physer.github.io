---
layout: post
title: "Accessing Azure Storage services without storing secrets using Azurite, Docker, HTTPS and Azure - Part 3"
date: 2024-08-07 15:00 +0200
categories: azure
---

## Introduction

Welcome back to the third part in the blog series about using Azurite over HTTPS with the DefaultAzureCredential and Docker!

You can read the previous parts here:

- [Part 1]()
- [Part 2]()

In the previous two parts of this blog series, we've focused on setting up our development environment. We have a working Azure Storage emulator in the form of Azurite and we have a simple .NET API that can interact with a blob in this Azure Storage emulator.

However, we're still very much dependent on the connection string. Using a connection string is only _one_ way of authenticating with an Azure Storage service (whether that's Azurite or the real deal). Not only is only one way, it's also not the _ideal_ way.

In the world of Azure there are things called [managed identities](https://learn.microsoft.com/en-us/entra/identity/managed-identities-azure-resources/overview). These special type of service principals allow you to get access to Azure services from other services (e.g. an App Service running your beautiful .NET application). Managed identities have several advantages over using a connection string or storing identifiers and secrets in your application settings or code. Most notably, you don't have to manage credentials yourself.

That's all nice and well, but we're dealing with Azurite here. That's not a full blown Azure service that lives somewhere in the cloud so those managed identities are of no use to us. However, if we'd were to use managed identities (for instance, or another kind of authentication method) _and_ a connection string in our code for our local development, we'd have to write (at least) two different mechanisms to authenticate to Azure based on which authentication method we are using.

Luckily Microsoft is one step ahead of us (as usual 😉).

This part of this blog series will cover what the `DefaultAzureCredential` is, how to implement it and how to setup Azurite in Docker with HTTPS.

## Setting up DefaultAzureCredential

### Overview

If there ever was a silver bullet for simplifying authentication through code, it's the [DefaultAzureCredential](https://learn.microsoft.com/en-us/dotnet/api/overview/azure/identity-readme?view=azure-dotnet#key-concepts).

The `DefaultAzureCredential` mechanism goes through different types of authentication methods chronologically. Stopping when a certain method is satisfied.

You can see the order in this diagram. More information can be found at the link above.
![authentication flow](https://raw.githubusercontent.com/Azure/azure-sdk-for-net/main/sdk/identity/Azure.Identity/images/mermaidjs/DefaultAzureCredentialAuthFlow.svg)

As you can see, these can be credentials meant for deployed services (e.g. an Azure App Service), as well as for development credentials in Visual Studio or interactive through a user input dialog.

### Connecting your Azure account

Due to the nature of the `DefaultAzureCredential` class, it's imperative that we at least log in to our Azure account. There are multiple ways to do so (as described in the diagram above).

In this blog series, we'll use the [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/get-started-with-azure-cli).

Verify your Azure CLI is available in your terminal. Run `az version`. When your Azure CLI is available, log in to your Azure account by running `az login`. We're not going to use any real Azure services (yet) so don't worry about too much about setting the subscription.

### Wiring it up

In [part 2]() of this blog series we've created a simple .NET application to interact with our Blob service from Azurite. Let's open up our application and navigate to the `Program.cs` file.

Remember that we've installed the `Azure.Identity` package? We haven't used this package yet but we will now.

Looking at [Microsoft's documentation](https://learn.microsoft.com/en-us/dotnet/azure/sdk/dependency-injection?tabs=web-app-builder) on how to configure the Azure SDK for dependency injection, we can see that the `DefaultAzureCredential` class is used with the `UseCredential` extension method.

Let's remove our full connection string and add our `DefaultAzureCredential`. Replace the entire connection string with an URI that points to the Blob service. In the case of Azurite that's: `http://127.0.0.1:10000/devstoreaccount1`.

This will change your argument for the `AddBlobServiceClient` method to:

```csharp
clientBuilder.AddBlobServiceClient(new Uri("http://127.0.0.1:10000/devstoreaccount1"));
```

Next, add a line below where you use the `DefaultAzureCredential`. Now your `AddAzureClients` method should look something like this:

```csharp
builder.Services.AddAzureClients(clientBuilder =>
{
  clientBuilder.AddBlobServiceClient(new Uri("http://127.0.0.1:10000/devstoreaccount1"));
  clientBuilder.UseCredential(new DefaultAzureCredential());
});
```

Resulting in a `Program.cs` file that looks like this:

```csharp
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Azure;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAzureClients(clientBuilder =>
{
  clientBuilder.AddBlobServiceClient(new Uri("http://127.0.0.1:10000/devstoreaccount1"));
  clientBuilder.UseCredential(new DefaultAzureCredential());
});
var app = builder.Build();

app.MapGet("/blob", ([FromServices] BlobServiceClient blobServiceClient) =>
{
  BlobContainerClient blobContainerClient = blobServiceClient.GetBlobContainerClient("demo");
  blobContainerClient.CreateIfNotExists();

  var blob = blobContainerClient.GetBlobs()?.FirstOrDefault();
  return blob is null ? Results.Ok("No blob item available") : Results.Ok(blob);
});
app.MapGet("/", () => "Hello World!");

app.Run();

```

Alright! Let's try to run it.

When we type `dotnet run`, our application should start normally. Navigating to the homepage of the application will still give you `Hello World!` (unless you removed the endpoint, of course). However, when we navigate to our `/blob` endpoint, we are getting an exception:

```sh
fail: Microsoft.AspNetCore.Diagnostics.DeveloperExceptionPageMiddleware[1]
      An unhandled exception has occurred while executing the request.
      System.ArgumentException: Cannot use TokenCredential without HTTPS.
```

The error message is crystal clear, we cannot use the `DefaultAzureCredential` (which is a `TokenCredential`) without using HTTPS!

## Azurite and HTTPS

In order to properly connect Azurite with our Azure SDK and the Azure Storage Explorer, we'll need to switch Azurite to HTTPS. To use HTTPS we are going to need a TLS certificate.

So far we can still follow along with [Microsoft's documentation on how to connect Azurite with the SDK](https://learn.microsoft.com/en-us/azure/storage/common/storage-use-azurite?tabs=visual-studio%2Cblob-storage#connect-to-azurite-with-sdks-and-tools).

There are two steps we need to take in order to let Azurite play nice with the `DefaultAzureCredential`:

- Enable OAuth authentication for Azurite via the --oauth switch. To learn more, see OAuth configuration.
- Enable HTTPS by using a self-signed certificate via the --cert and --key/--pwd options.

[Microsoft's excellent documentation on Github](https://github.com/Azure/Azurite/blob/main/README.md#https-setup) shows how to generate a self-signed certificate for this purpose. Let's follow along.

Since I'm on WSL2 (Ubuntu), I'm going to use [OpenSSL](https://docs.openssl.org/master/man1/openssl-cmds/) for generating my certificate. You can use anything else if you wish. `mkcert` is also covered by Microsoft's instructions.

> In Microsoft's documentation they'll refer to `mkcert`. This is fine as long as you're on a host machine where you can easily trust the root certificate authority. `mkcert` only generates leaf certificates. If we intend to trust our certificate in a containerized application later on, it will be quite cumbersome to do so. Hence my choice of `OpenSSL`.

The next steps assume you've installed and/or can interact with `openssl`.

In our project's root directory (`~/azure-demo`), let's create a folder called `certs` and generate a certificate for our Azurite endpoint. With `openssl` we can use the following command: `openssl req -newkey rsa:2048 -x509 -nodes -keyout key.pem -new -out cert.pem -sha256 -days 365 -addext "subjectAltName=IP:127.0.0.1" -subj "/C=CO/ST=ST/L=LO/O=OR/OU=OU/CN=CN"`.

This will give us two files:

- cert.pem
- key.pem

Now we can use these files in our Docker Compose file for Azurite to use.

Let's update our Compose file with a volume bind to the `certs` folder previously created.
Don't forget to update the parameters for the `azurite` launch command.

Our Compose file now looks like this:

```yml
services:
  azurite:
    container_name: azurite
    image: mcr.microsoft.com/azure-storage/azurite
    ports:
      - 10000:10000
      - 10001:10001
      - 10002:10002
    volumes:
      - ./certs:/certs
      - blobs:/data
    command:
      [
        "azurite",
        "--blobHost",
        "0.0.0.0",
        "--queueHost",
        "0.0.0.0",
        "--tableHost",
        "0.0.0.0",
        "--cert",
        "/certs/cert.pem",
        "--key",
        "/certs/key.pem",
        "--oauth",
        "basic",
        "--location",
        "/data",
      ]

volumes:
  blobs:
```

As you can see we've added quite a few parameters to the Azurite launch command. First of all, we have bound the certificate and key for the TLS certificate to Azurite through the `--cert` and `--key` parameters. Additionally, if we want to make use of the `DefaultAzureCredential` mechanism, we also need to enable [OAuth](https://learn.microsoft.com/en-us/azure/storage/common/storage-use-azurite?tabs=visual-studio%2Cblob-storage#oauth-configuration).

Secondly, since we're using a Docker container we will need the application te able to expose the certificate. If we simply let Azurite bind itself to `127.0.0.1`, we will not be able to access the certificate details from outside the container (for a handshake, for instance). To support this, we let Azurite listen on every IP address by specifying the `--blobHost` parameter (and queue and table, but those are not relevant for this blog post). For more information about the listening host configuration, check out [the documentation](https://github.com/Azure/Azurite/blob/main/README.md#listening-host-configuration).

And lastly, once we override the default Azurite launch command we need to make sure to point to the proper persistence location for data storage. In our case that's the `/data` folder since that's what we bind the `blobs` volume to.

Run `docker compose up -d` to recreate Azurite with these new parameters.

Verify Azurite now runs on HTTPS in your container logs:

```
Azurite Blob service is starting at https://0.0.0.0:10000
Azurite Blob service is successfully listening at https://0.0.0.0:10000
Azurite Queue service is starting at https://0.0.0.0:10001
Azurite Queue service is successfully listening at https://0.0.0.0:10001
Azurite Table service is starting at https://0.0.0.0:10002
Azurite Table service is successfully listening at https://0.0.0.0:10002
```

## Verifying the connection

Now that we've set up Azurite to accept HTTPS connections using our self-signed certificate, let's update our demo application to reflect this in the storage URL.

Open the `Program.cs` file and change the protocol of the storage URL to `https`:

```csharp
clientBuilder.AddBlobServiceClient(new Uri("https://127.0.0.1:10000/devstoreaccount1"));
```

Keep in mind that if you're trying to interact with the TLS certificate on _your_ machine you'll need to trust the certificate. You can trust the self-signed newly created Azurite certificate on WSL2/Linux by following these steps:

```sh
sudo cp ~/azurite-demo/certs/cert.pem /usr/local/share/ca-certificates/cert.crt
sudo update-ca-certificates
```

> On Linux, the file extension of the certificate located in the `ca-certificates` directory has to be `.crt`.

Please refer to [Microsoft's documentation](https://github.com/Azure/Azurite/blob/main/README.md#https-setup) for more details.

Personally I'm using the Azure Storage Explorer on Windows, whilst all the other things live in WSL2. In this case I copy over self-signed `cert.pem` file. I then trust it on my Windows machine by running `certutil –addstore -enterprise –f "Root" cert.pem`.

> You need an elevated terminal to execute the `certutil` command.

Before we test our .NET application, let's go to the Azure Storage Explorer.

If you try to open the Azurite emulator node now, you'll receive an error. We're now using Azurite over HTTPS so we'll need to tell the Azure Storage Explorer to accept the TLS certificate and use that for the handshake.

Here's an excerpt on how to configure the Azure Storage Explorer from Microsoft's documentation:

```
1. Connect to Azurite using HTTPS
By default, Storage Explorer doesn't open an HTTPS endpoint that uses a self-signed certificate. If you're running Azurite with HTTPS, you're likely using a self-signed certificate. In Storage Explorer, import SSL certificates via the Edit -> SSL Certificates -> Import Certificates dialog.

2. Import Certificate to Storage Explorer
  a. Find the certificate on your local machine.
  b. In Storage Explorer, go to Edit -> SSL Certificates -> Import Certificates and import your certificate.

If you don't import a certificate, you get an error: unable to verify the first certificate or self signed certificate in chain

3. Add Azurite via HTTPS connection string
Follow these steps to add Azurite HTTPS to Storage Explorer:

  a. Select Toggle Explorer
  b. Select Local & Attached
  c. Right-click on Storage Accounts and select Connect to Azure Storage.
  d. Select Use a connection string
  e. Select Next.
  f. Enter a value in the Display name field.
  g. Enter the HTTPS connection string from the previous section of this document
  h. Select Next
  i. Select Connect
```

In-depth information on how to do this can be found on [Microsoft's documentation on connecting Azurite with SDKs and tools](https://learn.microsoft.com/en-us/azure/storage/common/storage-use-azurite?tabs=visual-studio%2Cblob-storage#connect-to-azurite-with-sdks-and-tools).

The connection string for the HTTPS Blob Storage is:

```
DefaultEndpointsProtocol=https;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=https://127.0.0.1:10000/devstoreaccount1;
```

> If you receive a certificate error, ensure both the Certificate Authority (CA) as well as the self-signed certificate are trusted on your machine.

Once you're connected, ensure there's an image in the `demo` container.

The Azure Storage Explorer will look something like this. Notice the `Properties` in the lower left corner:
![Azure Storage Explorer with HTTPS](/assets/images/2024-08-07-azurite-with-https-in-docker/azure-storage-explorer-https.png)

Now that we know our Azurite is working properly with HTTPS, let's fire up our .NET application (`dotnet run` in the `~/azurite-demo/demo-app` directory) and navigate to the `/blob` endpoint.

You should see valid output returned by an HTTP 200 OK status code, similar to:
![.NET application calls Azurite through HTTPS](/assets/images/2024-08-07-azurite-with-https-in-docker/dotnet-succesfully-https-azurite.png)

## Almost there

Very nice! You've made it this far.

We can now let our .NET application connect to Azurite running in Docker using HTTPS with a self-signed certificated created by us. Additionally, we're capable of interacting with the Azurite container from our host machine through the sharing of the self-signed certificate.

In the next part we are going to containerize our .NET application. Since we're no longer running the .NET application from a host, we will also make sure we can trust our self-signed certificate in our Docker container. If you have no intentions of containerizing your application for development purposes and do not want to follow along with our deployment to Azure, you can stop reading here.

Though of course the fun _really_ only starts from part 4 onwards! 😉

Continue to [part 4 here]().
