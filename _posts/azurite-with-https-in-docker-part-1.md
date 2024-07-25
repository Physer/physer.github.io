---
# layout: post
# title: "Azurite, HTTPS, Azure Storage SDKs, Azure Storage Explorer and Docker - Part 1"
# date: 2023-07-23 09:35 +0200
# categories: azure
---

## Introduction

Hey there!

Lots of buzzwords, this title! Right? Well, let's break it down a little.

[Azurite](https://learn.microsoft.com/en-us/azure/storage/common/storage-use-azurite?tabs=visual-studio%2Cblob-storage) is the official open-source Azure Storage emulator. If you're familiar with the old-school Azure Storage Emulator, Azurite is its successor. You can use Azurite to emulate the Azure Blob, Queue and Table Storage cloud services on your local machine.

If you're using these Azure services, there's a high probability you're using the Azure Storage client libraries. Whether you're using these for .NET, Python, JavaScript or any other language is irrelevant to this blog post.

When using the Azure Storage client libraries (or any other Azure libraries), you're going to want to authenticate to Azure at some point. Whether that's on your local development environment already or in a cloud environment at a later stage, doesn't matter.

Handling authentication to Azure or its emulators on your local machine and in Azure without too much hassle can be daunting. For this purpose, the [DefaultAzureCredential](https://learn.microsoft.com/en-us/dotnet/api/overview/azure/identity-readme?view=azure-dotnet#defaultazurecredential) has been created by Microsoft. This mechanism allows you to write your code agnostic to your credentials. You no longer need to store identifiers and secrets in your application.

Using the Azure client libraries and the DefaultAzureCredential in conjunction with Azurite requires communicating over [HTTPS](https://www.cloudflare.com/learning/ssl/what-is-https/).

In this blog series, we'll cover how to set up Azurite in Docker, using HTTPS and a self-signed certificate. We'll connect our Azurite emulator to an example application using the Azure client libraries without storing any secrets in our application. Our example application will be capable of connecting to both our local emulator and an Azure cloud environment. Additionally, we'll cover how to use Azurite over HTTPS with the Azure Storage Explorer to view and manage the storage in your emulator.

The first part of this blog series will focus on setting up our environment. We'll set up Azurite in Docker and set up the Azure Storage Explorer.

## Prerequisites

If you want to follow along with this blog series, you will need the following software:

- [Docker](https://www.docker.com/products/docker-desktop/)
- [Docker Compose](https://docs.docker.com/compose/)
- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- [mkcert](https://github.com/FiloSottile/mkcert)
- [Azure Storage Explorer](https://azure.microsoft.com/en-us/products/storage/storage-explorer)

If you also wish to follow along with the part where we connect our application an actual Azure cloud environment, you'll also need an Azure account.

Note that I'm doing this on a Windows machine using WSL2 and Visual Studio Code. All applications and tools are available cross-platform.

## Setting up Azurite in Docker using Compose

Let's get started!

We will begin by setting up Azurite in Docker. Let's create a Compose file in a new directory.

- `mkdir ~/azurite-demo`
- `cd ~/azurite-demo`
- `touch compose.yaml`

Let's open up our Compose file and add Azurite as a service. I'm using [Visual Studio Code](https://code.visualstudio.com/) but you can use any editor you prefer of course.

Taking a look at [Microsoft's documentation on Azurite](https://learn.microsoft.com/en-us/azure/storage/common/storage-use-azurite?tabs=docker-hub%2Cblob-storage#install-azurite), we can see how to run Azurite in Docker. We'll use this information to create our Compose file.

Our Compose file should look something like this:

```yml
services:
  azurite:
    container_name: azurite
    image: mcr.microsoft.com/azure-storage/azurite
    ports:
      - 10000:10000
      - 10001:10001
      - 10002:10002
```

Let's run our Compose file: `docker compose up -d`.

Azurite is now running as a Docker container on ports 10000, 10001 and 10002. If we inspect the container's logs we'll see this confirmed (`docker logs azurite`):

```
Azurite Blob service is starting at http://0.0.0.0:10000
Azurite Blob service is successfully listening at http://0.0.0.0:10000
Azurite Queue service is starting at http://0.0.0.0:10001
Azurite Queue service is successfully listening at http://0.0.0.0:10001
Azurite Table service is starting at http://0.0.0.0:10002
Azurite Table service is successfully listening at http://0.0.0.0:10002
```

We now have a very simple Azurite docker container running with [ephemeral storage](https://docs.docker.com/storage/) (meaning all data is removed once the container is removed).

## Setting up the Azure Storage Explorer

Great! We've got Azurite up and running. However, currently we don't have an easy way to manage and view its data. Let's fix that!

Grab the Azure Storage Explorer tool from [Microsoft's website](https://azure.microsoft.com/en-us/products/storage/storage-explorer). Download the version for your operating system and run it after installing.

You should be greeted by a screen similar to this:
![Azure Storage Explorer starting screen](/assets/images/2024-07-23-azurite-with-https-in-docker/azure-storage-explorer-welcome.png)

Azure Storage Explorer has attached the Storage Emulator by default (whether that's the legacy Azure Storage Emulator, or Azurite).

- Expand the `(Emulator - Default Ports) (Key)` node in the Explorer on the left hand side.
- Open the `Blob Containers` node

You'll see that there aren't any containers at the moment. You'll just see a `View all` button that won't show you anything no matter how often you mash it.

Let's create a container to verify we can access Azurite properly. Right-click the `Blob Containers` node and select `Create Blob Container`. Enter a name for your container (e.g. `demo`) and confirm. The container has been created. If you wish, you can even upload a file but creating a container tells us enough already. Azurite and the Azure Storage Explorer are connected.

Finally your Azure Storage Explorer could look something like this:

![Azure Storage Explorer demo upload](/assets/images/2024-07-23-azurite-with-https-in-docker/azure-storage-explorer-demo-upload.png)

## Next up

This concludes the first part of this blog series! Setting up Azurite and the Storage Explorer will give us a solid foundation for setting up our example application where we'll connect our Docker container to so we can actually interact with Azure Storage.

Continue to part 2 here: []()
