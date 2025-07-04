---
layout: post
title: "Using Auth0 with role-based authorization in ASP.NET Core"
date: 2025-07-04 09:00 +0200
categories: authentication
---

## Introduction

Authentication and authorization are always an interesting topic in the world of web development.
When developing web applications, at some point you're bound to have a situation where users need to be able to log in to your app.
Subsequently, when users log in to your app, at some point you're bound to have a situation where only specific users are allowed to access specific parts of your app.

In ASP.NET Core, there are several [identity management solutions](https://learn.microsoft.com/en-us/aspnet/core/security/identity-management-solutions?view=aspnetcore-9.0) available to choose from.
One of these solutions is [Auth0](https://auth0.com/).

Auth0 is a comprehensive cloud-based authentication, authorization and user management solution. It has both B2C and B2B options, as well as a generous free tier.

When using Auth0 as your identity management solution in ASP.NET Core applications, you might want to assign roles to users in Auth0 and propagate those to your ASP.NET Core application.
This blog post explains how to set-up your Auth0 tenant so that ASP.NET Core will be able to automatically capture the roles onto the [.NET claims-based identity](https://learn.microsoft.com/en-us/dotnet/api/system.security.claims?view=net-9.0) so you can create role-based policies.

> Disclaimer: This blog post is _not_ sponsored or supported by Auth0 in any way. Everything I mention about Auth0 and ASP.NET Core is my own opinion and experience and does not reflect the Auth0 community.

## Setting up our ASP.NET Core project

For simplicity this blog post will focus on setting up identity management with an [ASP.NET Core Razor Pages]() project. However, everything mentioned applies to [ASP.NET Core MVC]() and [ASP.NET Core Blazor]() applications as well.

> All development in this blog post by me is done using Visual Studio Code and the .NET CLI on WSL2 with the .NET 9 SDK.

Let's go ahead and set up our project!

First we're going to create a new ASP.NET Core Razor Pages project by running: `dotnet new webapp --name auth0-aspnet-demo`. After this has been created, go ahead and open the newly created folder in Visual Studio Code (`code ~/auth0-aspnet-demo`).

Verify everything works as expected and you can run the project by running: `dotnet run` in your project folder. Your console should output some logging and a URL should be presented like so:

```bash
alex@PC-ALEX:~/auth0-aspnet-demo$ dotnet run
Using launch settings from /home/alex/auth0-aspnet-demo/Properties/launchSettings.json...
Building...
warn: Microsoft.AspNetCore.DataProtection.KeyManagement.XmlKeyManager[35]
      No XML encryptor configured. Key {91ddaa99-57ac-44a4-9656-7bf833735c45} may be persisted to storage in unencrypted form.
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5203
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
info: Microsoft.Hosting.Lifetime[0]
      Hosting environment: Development
info: Microsoft.Hosting.Lifetime[0]
      Content root path: /home/alex/auth0-aspnet-demo
```

Open your browser and navigate to the presented URL (or CTRL+Click) to verify you can see the new Razor Pages project looking something like this:
![Newly created ASP.NET Core Razor Pages project](/assets/images/2025-07-04-auth0-role-based-auth-aspnet/new-razor-pages-project.png)

Perfect! For simplicity's sake, we'll keep everything as-is. If you'd like to customize your application and its styling, feel free to do so however!

Now that we've set up our Razor Pages project, let's head over to Auth0 to set up our tenant!

## Setting up our Auth0 tenant and application

Head over to https://auth0.com and log in or sign up. Select the tenant you want to apply this to or create a new one.

I'm not going into details on how to set up a new tenant in Auth0 or what a tenant is exactly. If you'd like more information about this, please view Auth0's [Get Started](https://auth0.com/docs/get-started/auth0-overview) documentation.

For demo purposes, I have created a new tenant in the EU with the Development environment tag:
![New Auth0 tenant](/assets/images/2025-07-04-auth0-role-based-auth-aspnet/auth0-new-tenant.png)

Once your tenant has been created, head over to the Applications tab in the sidebar and select the Applications sub navigation item. Once you're on the applications page, create a new application by pressing the button: `+ Create Application`:
![New Auth0 application](/assets/images/2025-07-04-auth0-role-based-auth-aspnet/auth0-new-application.png)

Select the desired type of application, in case of this example: Regular Web Applications and click on create:
![Create Auth0 Application modal](/assets/images/2025-07-04-auth0-role-based-auth-aspnet/auth0-create-application-modal.png)

Feel free to open the Quickstart for ASP.NET Core MVC or follow along with this post to fully set up your tenant for local use.

Switch to the Settings tab at the top of the page and scroll down until you find the `Application URIs` section.

Copy the URL from your running local .NET application that you created in the previous step and add `/callback` to it. Add this URL to the `Allowed Callback URLs`. If your application is running on multiple URLs (e.g. HTTP and HTTPS), you can add multiple URLs in Auth0 by comma-separating them.

In my example, the Allowed Callback URL will be: `http://localhost:5203/callback`.

Add the URL(s) (without a path) in the `Allowed Logout URLs` field. In my example, that would be: `http://localhost:5203`.

This will result in your settings looking something like this:
![Application URI settings](/assets/images/2025-07-04-auth0-role-based-auth-aspnet/auth0-application-uris.png)

Don't forget to smash the save button in the bottom!

That concludes the necessary set up for our Auth0 tenant and application. Let's switch back to our .NET project to set up the integration with the Auth0 SDK!

## Updating our .NET project with the Auth0 SDK

Now that we've got our Auth0 tenant set up and our Auth0 application configured, let's get back to our .NET project and set up things from that side.

Install the Auth0 SDK for .NET by running `dotnet add Auth0.AspNetCore.Authentication` in your previously created folder (e.g. `~/auth0-aspnet-demo`).

Configure the .NET project to support Auth0's authentication provider by updating your `Program.cs` with the following code:

```csharp
builder.Services.AddAuth0WebAppAuthentication(options =>
{
    options.Domain = builder.Configuration["Auth0:Domain"] ?? throw new InvalidOperationException("Auth0:Domain configuration is missing.");
    options.ClientId = builder.Configuration["Auth0:ClientId"] ?? throw new InvalidOperationException("Auth0:ClientId configuration is missing.");
});
```

As you can see from the preceding code, the necessary Auth0 tenant settings are retrieved from the `appsettings.json`. Open the `appsettings.json` file and add the Domain and ClientId. You can find these values on the Settings page of your application in Auth0:

![Application's client ID and domain](/assets/images/2025-07-04-auth0-role-based-auth-aspnet/auth0-application-basic-info.png)

Whilst these values aren't secret, they are sensitive information. Please be sure to use care when checking these details into a version control system. Where necessary, use [User Secrets]() for local development and a proper secret management tool for Cloud development such as [Azure Key Vault]().

For demo purposes, I'm simply going to add them to our `appsettings.json` but be sure to not publish your settings online like that.

Your `appsettings.json` file should now look like this:

```json
"Auth0": {
    "Domain": "aspnetcore-physer-blog.eu.auth0.com",
    "ClientId": "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx"
}
```

> My ClientId has been redacted in the examples, make sure to fill in your details properly when following along with this blogpost.

## Creating the pages in .NET

## Creating our users and roles in Auth0

## Writing an Auth0 post-login Action

## Setting the policies in .NET

## Wrapping up

## References
