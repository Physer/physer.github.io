---
# layout: post
# title: "Azurite, HTTPS, Azure Storage SDKs, Azure Storage Explorer and Docker - Part 5"
# date: 2023-07-23 11:40 +0200
# categories: azure
---

## Introduction

Welcome to the 5th part of the blog series about setting up Azurite with a self-signed certificate in Docker.

In the previous parts we've assembled all the puzzle pieces necessary for communicating with Azurite over HTTPS through our containerized application.

In this part we will optimize our Dockerfile and code so this only becomes relevant for our development cycle, not our other environments.

We will also take a look at not making our applications dependent on the generating of the certificate on the host machine but rather from the application itself.

## Using environment variables

Let's start with moving our hard-coded service URL in our `~/azurite-demo/demo-app/Program.cs` file to the `appsettings.json` file.

I personally prefer to enter an empty string in my production appsettings and the actual value in my development settings where applicable so I get a reminder in case I forget a setting. Obviously this is completely up to you if you want to follow along.

Let's open up our `appsettings.json` file and add the following JSON object:

```json
"Storage": {
  "ServiceUri": ""
}
```

Then we'll head over to our `appsettings.Development.json` file and add the same JSON but with the actual value for the Azurite URL now:

```json
"Storage": {
  "ServiceUri": "https://127.0.0.1:10000/devstoreaccount1"
}
```

> Yes, we're referring to `127.0.0.1` here, we'll cover the Docker container name in a different environment variable later on.

We're not picking the `Storage` object with the `ServiceUri` randomly. These settings correspond with the `BlobServiceClient` constructor values as can be read in [Microsoft's documentation](https://learn.microsoft.com/en-us/dotnet/azure/sdk/dependency-injection?tabs=web-app-builder#store-configuration-separately-from-code).

> Note that if you _do_ pick different values for your appsettings, the way we set up our `BlobServiceClient` next won't work for you.

Go to the `Program.cs` file and update the `AddBlobServiceClient` extension method:

```csharp
clientBuilder.AddBlobServiceClient(builder.Configuration.GetSection("Storage"));
```

Since we're using the JSON object of `Storage` with the `ServiceUri` property, we can simply tell the `AddBlobServiceClient` method to take this Configuration section. The Azure SDK will wire it up for us.

Run the application (`dotnet run`) and verify you still get a response from your `/blob` endpoint.

> Run this on your machine, not through a Docker container.

## Setting up environment variables for our Docker container

Now that we've got our application ready to support different URLs based on appsettings, we can also add this to our containerized version of the application.

We have already done a similar thing for our Azure credentials using the `azure.env` file in [part 4]() of this series.

Now let's create a `app.env` file in the root directory of your project (`~/azurite-demo`) and add the storage URL appsetting.

> Remember that if you're on WSL2/Linux you should use `__` (double underscore) as a separater for nested settings as opposed to `:` on Windows machines.

```
Storage__ServiceUri=https://azurite:10000/devstoreaccount1
```

After this we will update our Compose file (`~/azurite-demo/compose.yaml`) to read from this new environment file:

```yml
demo_app:
  container_name: demo-app
  build:
    context: .
    dockerfile: Dockerfile
  env_file:
    - azure.env
    - app.env
  ports:
    - 8080
```

Let's run our applications: `docker compose up -d --build` and let's navigate to the `/blob` endpoint of container's URL. You should see your blob item.

> Keep in mind that we have not set up a persisted location for our Azurite blobs, so if your Azurite container has been deleted your blob container will be empty, required you to re-upload an item to the container before you see results.

## Generating our certificate from the other side

As mentioned in the end of [part 4](), one of the problems we have now is that our host machine is responsible for generating the certificates.

Let's fix that by moving our certificate generation to our Dockerfile (`~/azurite-demo/Dockerfile`).

Instead of generating our certificates on our host machine and sharing them with our Docker containers, our .NET application will take care of the generation of the certificates.

Our entire Dockerfile now looks like this:

```Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /app

COPY ./demo-app ./
RUN dotnet restore
RUN dotnet publish --no-restore -c Release -o /publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /certs
RUN openssl req -newkey rsa:2048 -x509 -nodes -keyout ./key.pem -new -out ./cert.pem -sha256 -days 365 -addext "subjectAltName=IP:127.0.0.1,DNS:azurite" -subj "/C=CO/ST=ST/L=LO/O=OR/OU=OU/CN=CN"
RUN cp cert.pem /usr/local/share/ca-certificates/cert.crt
RUN update-ca-certificates

WORKDIR /app
COPY --from=build-env /publish .

ENTRYPOINT ["dotnet", "demo-app.dll"]
```

The line that previously copied over our certificate from our `~/azurite-demo/certs` folder is gone and instead is replaced by an `openssl` command. The very same command we used in [part 4]() to generate our certificate on our host machine.

After our change to the Dockerfile, we will update our Compose file (`~/azurite-demo/compose.yaml`).

Instead of a volume bind to our host, we will use a named volume to share data between the two containers.

At the bottom of the Compose file we'll create a volume named `certificates`. Additionally we will mount the volume on both containers. To finish it off, we will make our Azurite container dependent on our .NET application since that's responsible for generating the certificates.

All in all, our Compose file now looks like this:

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
      - certificates:/certs
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
      ]
    depends_on:
      - demo_app

  demo_app:
    container_name: demo-app
    build:
      context: .
      dockerfile: Dockerfile
    env_file:
      - azure.env
      - app.env
    ports:
      - 8080
    volumes:
      - certificates:/certs

volumes:
  certificates:
```

We can clean up our previously made `certs` folder by running `rm -r ~/azurite-demo/certs`.

A small side note here about the Docker volume we've just created. This is a named volume that only exists in the context of Docker. You have no direct access to this volume by file system. In our case, we'd still want to access our `cert.pem` file on our host machine so we can interact with Azurite through the Azure Storage Container. In order to do so, we can inspect the volume using Docker Desktop and storing the `cert.pem` file from there. More information about this feature of Docker Desktop [can be found here](https://docs.docker.com/desktop/use-desktop/volumes/#inspect-a-volume). There are other ways to accomplish this as well. For instance, you might use a host bind or a volume with certain driver options for data persistence (e.g. NFS). You can see an example of the volume data through Docker Desktop here:
![stored volume data in Docker Desktop](/assets/images/2024-07-23-azurite-with-https-in-docker/docker-desktop-volume-inspect.png)

> Don't forget to import the newly created certificate in Azure Storage Explorer and trust it on the host machine with the `certutil` command! The certificate is now a different one than before, requiring us to re-import this.

Let's run the applications by running `docker compose up -d --build`. Navigate to the `/blob` endpoint on your container's URL and verify you get a response.

Awesome! We now have a working set-up of two containers talking to each other with a self-signed TLS certificate from the .NET application's Dockerfile.

Although this works like a charm, our Dockerfile currently does this at all times. Even if we would deploy to a Cloud environment such as Production or a Staging environment. In that case we don't want to use Azurite and we certainly don't want to generate and trust a self-signed certificate. Of course it won't do any harm, but it's good practice to not do it when it's not required.

## Optimizing our Dockerfile

In order to only generate and trust a self-signed certificate when we're developing we're going to make more use of [multi-stage magics](https://docs.docker.com/build/building/multi-stage/). More specifically, targeting certain stages.

Let's open up our Dockerfile (`~/azurite-demo/Dockerfile`).

