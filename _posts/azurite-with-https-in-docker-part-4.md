---
# layout: post
# title: "Azurite, HTTPS, Azure Storage SDKs, Azure Storage Explorer and Docker - Part 4"
# date: 2023-07-23 11:40 +0200
# categories: azure
---

## Introduction

Welcome to part 4 of this blog series where we uncover how Azurite can emulate Azure Storage services using Docker, HTTPS and the DefaultAzureCredential!

In the previous parts we've covered setting up Azurite as a Docker container, setting up a sample .NET application to interact with the Azure Storage using the Azure SDKs and setting up the DefaultAzureCredential to simplify Azure access in code.

You can read the previous parts here:

- [Part 1]()
- [Part 2]()
- [Part 3]()

## Containerizing the application

We will start by containerizing our sample .NET application. We're going to create a multi-stage Dockerfile to optimize the image the application will run on.

If you have been following along with another stack or programming language, you can just focus on the changes we do to the Dockerfile later on, don't worry about specific .NET things here.

Let's head over to our project root folder (`~/azurite-demo`) and create a Dockerfile by running `touch Dockerfile`.

> If you prefer to use Visual Studio/dotnet's generation of a Dockerfile, that's perfectly fine as well

First up is creating a build stage where we can use the .NET SDK to build and publish our application. We'll stick to a rather standard approach for this:

```Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /app

COPY ./demo-app ./
RUN dotnet restore
RUN dotnet publish --no-restore -c Release -o /publish
```

A stage called `build-env` will build and publish our application after copying the necessary source files into the Docker container.

The next stage will be the runtime stage, which will be responsible for running our application by using a slimmed-down image without the SDK.

> Interested in more information about multi-stage builds? Check out [Docker's documentation](https://docs.docker.com/build/building/multi-stage/).

```Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app
COPY --from=build-env /publish .

ENTRYPOINT ["dotnet", "demo-app.dll"]
```

Your entire Dockerfile should now look like this:

```Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /app

COPY ./demo-app ./
RUN dotnet restore
RUN dotnet publish --no-restore -c Release -o /publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app
COPY --from=build-env /publish .

ENTRYPOINT ["dotnet", "demo-app.dll"]
```

Now that we have our image ready for use, let's add it to our Compose file. Navigate to `~/azure-demo/compose.yaml` and add the image to the file like so:

```yml
demo_app:
  container_name: demo-app
  build:
    context: .
    dockerfile: Dockerfile
  ports:
    - 8080
```

This Compose service will start a container using our Dockerfile on a random available host port binding to the container's port of `8080`.

Run `docker compose up -d` to run the container.

Navigate to the exposed port (e.g. http://localhost:56659/) and verify you see the `Hello World!` output from the root endpoint:
![containerized .NET application](/assets/images/2024-07-23-azurite-with-https-in-docker/containerized-hello-world.png)

Now let's try the `/blob` endpoint. When we open that endpoint, we can see (a lot) of errors in our Docker logs. If we scroll through those errors we see something like this:

<!-- TODO ERROR -->

Our certificate is getting rejected by the container. That's because the container does not trust the previously generated `azurite.pem` certificate.

## Updating the Dockerfile to support our certificate

In order to get the container to trust our certificate, we can update the Dockerfile.

Let's head over to our previously created Dockerfile (`~/azurite-demo/Dockerfile`).

Since we're interested in our container trusting the certificate when running, we'll need to update the second stage (the runtime stage) of the Dockerfile.

We can copy our generated certificate into our image and tell Linux to trust it. Let's add the code necessary to do so:

```Dockerfile
WORKDIR /certs
COPY ./certs/azurite.pem .
RUN cp azurite.pem /usr/local/share/ca-certificates/azurite.crt
RUN update-ca-certificates
```

This should seem familiar to you, we've used the same approach on our own WSL2 system in [Part 3]() of this series.


