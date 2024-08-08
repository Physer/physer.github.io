---
layout: post
title: "Accessing Azure Storage services without storing secrets using Azurite, Docker, HTTPS and Azure - Part 5"
date: 2024-08-07 15:00 +0200
categories: azure
---

## Introduction

Welcome to the 5th part of the blog series about setting up Azurite with a self-signed certificate in Docker.

In the previous parts we've assembled all the puzzle pieces necessary for communicating with Azurite over HTTPS through our containerized application.

In this part we will optimize our Dockerfile and code so this only becomes relevant for our development cycle, not our other environments.

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

We have already done a similar thing for our Azure credentials using the `azure.env` file in [part 4](https://blog.alexschouls.com/azure/2024/08/07/azurite-with-https-in-docker-part-4.html) of this series.

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

## Optimizing our Dockerfile

Currently when we build our .NET application with our Dockerfile, it will always trust the certificate we've generated and self-signed for development purposes. Whilst this won't hurt, it's not optimal to execute this in any other environment than development. _Especially_ when you're using the same Dockerfile for automated builds to other environments, for instance using automated deployment pipelines (e.g. GitHub Actions).

We can use more [multi-stage magic](https://docs.docker.com/build/building/multi-stage/) to make this happen only in situations where we want it. More specifically, by using targets.

Let's open up our Dockerfile (`~/azurite-demo/Dockerfile`).

We're going to change the stages a bit. First of all we're going to add an alias to our second stage called `runtime`. To do so, add `AS runtime` after the second `FROM` statement:

```Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
```

Next, we're going to make the `runtime` stage copy the files from the `build-env` stage to the `/app` directory. In other words, we'll grab the two lines we have below the `RUN update-ca-certificates` statement and paste them below the `runtime` stage:

```Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

WORKDIR /app
COPY --from=build-env /publish .
```

Now we have a `runtime` stage available which has the slimmed down version of the .NET runtime, rather than the entire SDK and our .NET application's published files available for reference.

Below these lines, we'll create a new stage called `development`, based on the `runtime` stage like so:

```Dockerfile
FROM runtime AS development
```

This stage will do the certificate work we've set-up previously, as well as switching to the `/app` directory later on and setting the `ENTRYPOINT` statement. It no longer needs to copy the files from the `/publish` directory from the other stage as we're basing it off our `runtime` stage. Remove that code

Your `development` stage should then look like this:

```Dockerfile
FROM runtime AS development

WORKDIR /certs
COPY ./certs/cert.pem .
RUN cp cert.pem /usr/local/share/ca-certificates/cert.crt
RUN update-ca-certificates

WORKDIR /app
ENTRYPOINT ["dotnet", "demo-app.dll"]
```

Finally, below the `ENTRYPOINT` of the `development` stage, we'll create a new unnamed stage also based off the `runtime` stage. This simply points to the `/app` directory and executes (the same) `ENTRYPOINT` statement as our `development` stage.

```Dockerfile
FROM runtime

WORKDIR /app
ENTRYPOINT ["dotnet", "demo-app.dll"]
```

Your entire Dockerfile now looks like:

```Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /app

COPY ./demo-app ./
RUN dotnet restore
RUN dotnet publish --no-restore -c Release -o /publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

WORKDIR /app
COPY --from=build-env /publish .

FROM runtime AS development

WORKDIR /certs
COPY ./certs/cert.pem .
RUN cp cert.pem /usr/local/share/ca-certificates/cert.crt
RUN update-ca-certificates

WORKDIR /app
ENTRYPOINT ["dotnet", "demo-app.dll"]

FROM runtime

WORKDIR /app
ENTRYPOINT ["dotnet", "demo-app.dll"]
```

If we would now run our Compose services (`docker compose up -d --build`), you'll see it will no longer import and trust the self-signed certificate (causing an HTTP 500 error when navigating to the `/blob` endpoint, by the way).

We can remedy this by updating our Compose file (`~/azurite-demo/compose.yaml`).

At our `demo_app` service, in the `build` property we can add the `target` property and set this to `development` like so:

```yml
build:
  context: .
  dockerfile: Dockerfile
  target: development
```

> If you intend to use the same Compose file for deployments or for other environments, it might be a good idea to move this target to an environment variable.

If we run our Compose services now (`docker compose up -d --build`), everything works like it used to do.

## Next

Great! We now have an optimized Dockerfile with support from Compose to have the option to import and trust the self-signed certificate or not.

We also have updated our code to let the Blob Service Client be registered based on an app setting rather than a hardcoded URL.

In the next part we'll deploy a real storage account in Azure, upload a blob to it and deploy our .NET application to Azure and allow it to read the file using managed identities and the _same_ code as we've written all the way back in [part 3](https://blog.alexschouls.com/azure/2024/08/07/azurite-with-https-in-docker-part-3.html) - excluding the environment variables, but that was a minor change 😉!

If you want to clean up your local Docker files you can run `docker compose down --rmi all`. If you want to clean your entire Docker environment afterwards, you can run: `docker system prune -af && docker volume prune -af && docker builder prune -af`.

Continue to [part 6 here](https://blog.alexschouls.com/azure/2024/08/07/azurite-with-https-in-docker-part-6.html).
