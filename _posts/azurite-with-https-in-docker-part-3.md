---
# layout: post
# title: "Azurite, HTTPS, Azure Storage SDKs, Azure Storage Explorer and Docker - Part 3"
# date: 2023-07-23 11:40 +0200
# categories: azure
---

## Introduction

Welcome back to the third part in the blog series about using Azurite over HTTPS with the DefaultAzureCredential and Docker!

In case you missed the previous part: [read part 2]().

If you want to read part 1, you can do so [here]().

In the previous two parts of this blog series, we've focused on setting up our development environment. We have a working Azure Storage emulator in the form of Azurite and we have a simple .NET API that can interact with a blob in this Azure Storage emulator.

However, we're still very much dependent on the connection string. Using a connection string is only _one_ way of authenticating with an Azure Storage service (whether that's Azurite or the real deal). Not only is only one way, it's also not the _ideal_ way.

In the world of Azure there are things called [managed identities](https://learn.microsoft.com/en-us/entra/identity/managed-identities-azure-resources/overview). These special type of service principals allow you to get access to Azure services from other services (e.g. an App Service running your beautiful .NET application). Managed identities have several advantages over using a connection string or storing identifiers and secrets in your application settings or code. Most notably, you don't have to manage credentials yourself.

That's all nice and well, but we're dealing with Azurite here. That's not a full blown Azure service that lives somewhere in the cloud so those managed identities are of no use to us. Whilst that is of course true, if we'd were to use managed identities (for instance, or another kind of authentication method) _and_ a connection string in our code for our local development, we'd have to write (at least) two different mechanisms to authenticate to Azure based on which authentication method we are using.

Luckily Microsoft is one step ahead of us (as usual 😉).

This part of this blog series will cover what the `DefaultAzureCredential` is, how to implement it and how to setup Azurite in Docker with HTTPS.

## Setting up DefaultAzureCredential

### Overview

If there ever is a silver bullet for simplifying authentication through code, it's the [DefaultAzureCredential](https://learn.microsoft.com/en-us/dotnet/api/overview/azure/identity-readme?view=azure-dotnet#key-concepts).

The `DefaultAzureCredential` mechanism goes through different types of authentication methods chronologically. Stopping when a certain method is satisfied.

You can see the order in this diagram. More information can be found at the link above.
![authentication flow](https://raw.githubusercontent.com/Azure/azure-sdk-for-net/main/sdk/identity/Azure.Identity/images/mermaidjs/DefaultAzureCredentialAuthFlow.svg)

As you can see, these can be credentials meant for deployed services (e.g. an Azure App Service), as well as for development credentials in Visual Studio or interactive through a user input dialog.

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

Since I'm on WSL2 (Ubuntu), I'm going to use [mkcert](https://github.com/FiloSottile/mkcert) for generating my certificate. You can use anything else if you wish. OpenSSL is also covered by Microsoft's instructions.

The next steps assume you've installed `mkcert` and trusted the local Certificate Authority (by running `mkcert -install`).

In our project's root directory (`~/azure-demo`), let's create a folder called `certs` and generate a certificate for our Azurite endpoint. With `mkcert` we can use the following command: `mkcert 127.0.0.1`.

This will give us two files:

- 127.0.0.1.pem
- 127.0.0.1-key.pem

Now we can use these files in our Docker Compose file for Azurite to use.

Let's update our Compose file with a volume bind to the `certs` folder previously created and updated our parameters.

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
    command:
      [
        "azurite",
        "--cert",
        "/certs/127.0.0.1.pem",
        "--key",
        "/certs/127.0.0.1-key.pem",
      ]
```

Run `docker compose up -d` to recreate Azurite with these new parameters.

Verify Azurite now runs on HTTPS in your container logs:

```
Azurite Blob service is starting at https://127.0.0.1:10000
Azurite Blob service is successfully listening at https://127.0.0.1:10000
Azurite Queue service is starting at https://127.0.0.1:10001
Azurite Queue service is successfully listening at https://127.0.0.1:10001
Azurite Table service is starting at https://127.0.0.1:10002
Azurite Table service is successfully listening at https://127.0.0.1:10002
```

## Verifying the connection

Now that we've set up Azurite to accept HTTPS connections using our self-signed certificate, let's update our demo application to reflect this in the storage URL.

Open the `Program.cs` file and change the protocol of the storage URL to `https`:

```csharp
clientBuilder.AddBlobServiceClient(new Uri("https://127.0.0.1:10000/devstoreaccount1"));
```

Execute `dotnet run` and navigate to the `/blob` endpoint... Now.. Wait, something's wrong! Our endpoint's not working anymore and we're receiving exceptions in our terminal.

Right, now we've hit some Docker networking snag. Since we're running Azurite in Docker we can't simply connect to `127.0.0.1`. Instead we could connect to `localhost`. However, that's not what we generated our certificate for. Let's go back and update our certificate so we can make this work with Docker.

Let's remove the existing certificates we generated: `rm ~/azurite-demo/certs/*.pem`.

Next we'll create a certificate that's valid for multiple domains and IP addresses: `mkcert localhost azurite 127.0.0.1`.

> Notice how I squeaked `azurite` as hostname in there? We'll cover that in the next part!

`mkcert` will output:

```sh
Created a new certificate valid for the following names 📜
 - "localhost"
 - "azurite"
 - "127.0.0.1"

The certificate is at "./localhost+2.pem" and the key at "./localhost+2-key.pem" ✅

It will expire on 23 October 2026
```

Let's rename our certificates for easy access:

- `mv localhost+2.pem azurite.pem`
- `mv localhost+2-key.pem azurite-key.pem`

### Updating Compose

Go back to our Compose file and update the certificate names as well as the binding. Since we now use HTTPS, Azurite only binds to its own address by default (`127.0.0.1`). However, we've just established that we can't use this address for communicating with our Azurite container since that's the internal loopback IP address. Additionally, as mentioned in the [Azurite and HTTPS](#azurite-and-https) chapter, we also need the `OAuth` flag.

We will update the certificate names and the command to reflect this like so:

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
        "/certs/azurite.pem",
        "--key",
        "/certs/azurite-key.pem",
        "--oauth",
        "basic",
      ]
```

Notice the updated `.pem` file names as well as the `*Host` arguments to specify the listen addresses for our hosts. Next to that, we've added the `--oauth` flag with the value `basic`.

Let's recreate our Azurite container: `docker compose up -d`.

All Azurite services should now be listening on all addresses:

```
Azurite Blob service is starting at https://0.0.0.0:10000
Azurite Blob service is successfully listening at https://0.0.0.0:10000
Azurite Queue service is starting at https://0.0.0.0:10001
Azurite Queue service is successfully listening at https://0.0.0.0:10001
Azurite Table service is starting at https://0.0.0.0:10002
Azurite Table service is successfully listening at https://0.0.0.0:10002
```

## Testing our connection

Remember, if you're trying to interact with the TLS certificate on _your_ machine you'll need to trust the certificate. Please refer to [Microsoft's documentation](https://github.com/Azure/Azurite/blob/main/README.md#https-setup) for more details. If you're executing the Azure Storage Explorer on a different machine than you've trusted the `mkcert` Certificate Authority, you'll need to trust the _same_ Certificate Authority on both machines. For more information on how to transfer Certificate Authorities, refer to the [mkcert documentation](https://github.com/FiloSottile/mkcert).

You can trust the self-signed newly created Azurite certificate on WSL2/Linux by following these steps:

```sh
sudo cp ~/azurite-demo/certs/azurite.pem /usr/local/share/ca-certificates/azurite.crt
sudo update-ca-certificates
```

> On Linux, the file extension of the certificate located in the `ca-certificates` directory has to be `.crt`.

Before we test our .NET application, let's go to the Azure Storage Explorer.

If you try to open the Azurite emulator node now, you'll receive an error. We're now using Azurite over HTTPS so we'll need to tell the Azure Storage Explorer to accept the TLS certificate and use that for the handshake.

In-depth information on how to do this can be found on [Microsoft's documentation on connecting Azurite with SDKs and tools](https://learn.microsoft.com/en-us/azure/storage/common/storage-use-azurite?tabs=visual-studio%2Cblob-storage#connect-to-azurite-with-sdks-and-tools).

Locate the `Microsoft Azure Storage Explorer` section and follow the instructions for `Connect to Azurite using HTTPS`.

> If you receive a certificate error, ensure both the Certificate Authority (CA) as well as the self-signed certificate are trusted on your machine.

Once you're connected, verify there's an image in the `demo` container.

The Azure Storage Explorer will look something like this. Notice the `Properties` in the lower left corner:
![Azure Storage Explorer with HTTPS](/assets/images/2024-07-23-azurite-with-https-in-docker/azure-storage-explorer-https.png)

Now that we know our Azurite is working properly with HTTPS, let's fire up our .NET application (`dotnet run` in the `~/azurite-demo/demo-app` directory) and navigate to the `/blob` endpoint.

You should see valid output returned by an HTTP 200 OK status code, similar to:
![.NET application calls Azurite through HTTPS](/assets/images/2024-07-23-azurite-with-https-in-docker/dotnet-succesfully-https-azurite.png)

## Almost there

Very nice! You've made it this far.

We can now let our .NET application connect to Azurite running in Docker using HTTPS with a self-signed certificated created by us. Additionally, we're capable of interacting with the Azurite container from our host machine through the sharing of the Certificate Authority and the self-signed certificate.

In the next part we are going to containerize our .NET application. Since we're no longer running the .NET application from a host, we will also make sure we can trust our self-signed certificate in our Docker container.

Continue to part 4 here: []().
