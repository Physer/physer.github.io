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

## Integrating our .NET project with Auth0

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

Verify everything is still working correctly by running your program and navigating to your application (`dotnet run`).

Cool! Our ASP.NET Core application is now integrated with the Auth0 SDK. In the next stage we will create a way to log in and out of the ASP.NET Core application.

## Creating UI support for logging in and out

So we've set up our Auth0 integration, great! It won't do us much good though until we've set up a way to authenticate. We need to be able to log in (and out, preferably) through our ASP.NET Core application.

Let's switch back to Visual Studio Code and head over to the `Program.cs` file, the entry point of our application.

Near the bottom of the file you can spot a line containing: `app.UseAuthorization();`. Add the following line above it: `app.UseAuthentication();`. This sets our application up to not only use authorization policies but also support authentication.

> Reminder: Authentication is the process of determining the validity of a user's identity. Is the user who it says it is? Whereas authorization is the process of verifying the access of a user. Once a user is identified, is it allowed to do what it's trying to do?

For simplicity, I'm going to create a new Razor Pages page that takes care of logging the user in and another one that takes care of logging the user out. This is not the only way to this though, you could also set up minimal endpoints, for instance.

Let's add a new empty Razor page and its code-behind file. Run `touch Pages/Login.cshtml & touch Pages/Login.cshtml.cs`

Open up your `Login.cshtml` file and add the following lines:

```
@page
@model LoginModel
```

Next, add the following code to your `Login.cshtml.cs` file:

```csharp
using Auth0.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace auth0_aspnet_demo.Pages;

public class LoginModel : PageModel
{
    public async Task OnGetAsync()
    {
        var authenticationProperties = new LoginAuthenticationPropertiesBuilder().WithRedirectUri("/").Build();
        await HttpContext.ChallengeAsync(Auth0Constants.AuthenticationScheme, authenticationProperties);
    }
}
```

If you're following along with the tutorial, verify your namespace and usings are correctly set.

Let's see if it worked! Run your application (`dotnet run`) and navigate to your URL. Go to `/login` and you should be redirected to Auth0. Register as a new user or log in as an existing one if you already have an account in Auth0. Afterwards, you should be redirected back to your application!

> If you run into an error in the Auth0 redirecting process, ensure your callback URL matches the URL you're visiting, for instance if you've set up Auth0 to accept `http://localhost:5203/callback`, and you're on `http:127.0.0.1:5203/callback`, Auth0 will not accept the URL.

To verify we're actually logged in as a user we can quickly update our homepage to show some data of the logged in user.

Open up your `Pages/Index.cshtml` file and below the existing code add the following:

```html
@if (User.Identity?.IsAuthenticated == true) {
<p>Hello, @User.Identity.Name</p>
}
```

Run your application and if you're still logged in (you might have to login again by navigating to the `/login` page), you should see your e-mail address pop up like so:

![User details from .NET Identity](/assets/images/2025-07-04-auth0-role-based-auth-aspnet/aspnet-user-identity-details.png)

Let's also quickly add a page to log out from the application. We will create a page similar to the login page, except now it will log you out. Run `touch Pages/Logout.cshtml & touch Pages/Logout.cshtml.cs` (or create them in any way you're comfortable).

Open `Pages/Logout.cshtml` and add the following lines:

```
@page
@model LogoutModel
```

Next, open up the code-behind: `Pages/Logout.cshtml.cs` and add these lines:

```csharp
using Auth0.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace auth0_aspnet_demo.Pages;

public class LogoutModel : PageModel
{
    public async Task OnGetAsync()
    {
        var authenticationProperties = new LogoutAuthenticationPropertiesBuilder().WithRedirectUri("/").Build();
        await HttpContext.SignOutAsync(Auth0Constants.AuthenticationScheme, authenticationProperties);
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
```

Ensure you're signing out of both the Auth0 authentication scheme and the Cookie authentication scheme and spin up your application to test it out!

First `/login` as a user, head over the homepage to verify you're logged in and then navigate to `/logout`. If you head over to the homepage after logging out (you should already be there since you get redirect to it) you won't see your user information anymore.

> The logging out process can happen very quickly, don't be surprised if you don't notice a lot happening. At the very least the user information should be gone from the homepage though.

Awesome! We can log in using Auth0 as an identity management platform and we can see that it's tied into the .NET ecosystem by simply reading from the User object available in the .NET SDK. Now let's create some pages that only logged in (and privileged) users can access.

## Creating pages in .NET only authenticated and authorized users can visit

Okay, now that we're able to log in (and out) as a user, let's set up some pages that only (privileged) users can access.

Let's create a page only an authenticated user can access, regardless of his roles and rights. We'll create a page and its code-behind like so: `touch Pages/Authenticated.cshtml & touch Pages/Authenticated.cshtml.cs`.

Open up the `Pages/Authenticated.cshtml` file and add the following lines:

```
@page
@model AuthenticatedModel
@{
    ViewData["Title"] = "A very secure page";
}

<div>Congratulations! You're logged in, otherwise you wouldn't be able to see this.</div>
```

Next, open its code-behind (`Pages/Authenticated.cshtml.cs`) and add these lines:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace auth0_aspnet_demo.Pages;

[Authorize]
public class AuthenticatedModel : PageModel;
```

> There are different ways of setting authentication and authorization conventions in ASP.NET Core Razor Pages but for simplicity, we are creating an empty page model here to decorate it with the proper attribute.

Now to verify it works, run your application. Make sure you're logged out by navigating to the `/logout` URL and try to navigate to the `/authenticated` URL. You will probably end up on a 404 with a weird URL saying something like `/Account/Login?ReturnUrl=%2Fauthenticated`. Don't worry about this for now, this is simply because we haven't configured the URL unauthorized users will land on. Log in by navigating to the `/login` URL and try to go to the `/authenticated` URL again. You should now be able to access the page and see something like this:

![The /authenticated page when you're logged in](/assets/images/2025-07-04-auth0-role-based-auth-aspnet/aspnet-authenticated-page.png)

Now that we have a page that every authenticated user can access, let's add a page that only a user in a specific _role_ can access. This is called [role-based authorization](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/roles?view=aspnetcore-9.0) in ASP.NET Core.

We'll create a page that only users that belong to the `Admin` role can access. Remember the name of this role, we'll need this later in Auth0. Let's create an admin page: `touch Pages/Admin.cshtml & touch Pages/Admin.cshtml.cs`.

Open the `Pages/Admin.cshtml` file and add:

```
@page
@model AdminModel
@{
    ViewData["Title"] = "Area 51";
}

<div>Wow, you're an admin! So cool!</div>
```

Then open up the code-behind (`Pages/Admin.cshtml.cs`) and add:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace auth0_aspnet_demo.Pages;

[Authorize(Roles = "Admin")]
public class AdminModel : PageModel;
```

Notice how we have specified a specific role in our `[Authorize]` attribute. Now only users that will have the `Admin` role as a specific claim will be able to access this page.

At this point in time we are not yet able to assign an Admin role to our user. We will take care of that in the next chapter. We can, however, verify a 'regular' logged-in user does not have access to this page.

Run your application, verify you're logged in by navigating to the `/login` endpoint and try to navigate to the `/admin` page. You should (again) end up on a non-existing URL like `/Account/AccessDenied?ReturnUrl=%2Fadmin` which is (again) because we haven't configured the redirects.

As you can see, we can't access our page even though we're logged in. We don't have the proper role assigned to our logged-in user! Let's fix that in Auth0.

## Assigning roles to our users in Auth0

## Writing an Auth0 post-login Action

## Setting the policies in .NET

## Wrapping up

## References
