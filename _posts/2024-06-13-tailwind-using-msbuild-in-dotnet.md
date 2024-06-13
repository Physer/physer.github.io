---
layout: post
title: "Using Tailwind in .NET projects with MSBuild and the Tailwind CLI"
date: 2024-06-13 12:00 +0200
categories: tailwind
---

## Introduction

Hi!

Welcome to this blogpost about Tailwind CSS and .NET projects.

[Tailwind CSS](https://tailwindcss.com/) is a utility-first CSS framework that allows developers to style their websites without writing any (or at the very least, barely any) custom CSS. Tailwind CSS has a ton of classes that developers can add to their HTML elements in order to style their application.

Usually Tailwind CSS is used in combination with front-end libraries and frameworks such as React, Vue or Angular.
In a case like that, you'll most likely have an NPM project that you can install Tailwind into.

However, perhaps there's a case where you're using a more back-end oriented language like C# for your web development. In these cases, usage of an NPM project is rarer and usually only done to support one or two libraries or packages.

In this blogpost we're going to find out how we can set up Tailwind CSS with a .NET project such as ASP.NET MVC, ASP.NET Razor Pages or a Blazor project using the standalone Tailwind CLI and MSBuild. **This way we don't need an NPM project and no `package.json` file is needed, and no NodeJS is required to be installed.**

### Table of contents

- [Introduction](#introduction)
- [Setting up a .NET project](#setting-up-a-net-project)
- [Getting the Tailwind CLI](#getting-the-tailwind-cli)
- [Preparing the project for Tailwind](#preparing-the-project-for-tailwind)
- [Setting up an MSBuild action](#setting-up-an-msbuild-action)
- [Supporting multiple operating systems and architectures](#supporting-multiple-operating-systems-and-architectures)
- [Conclusion](#conclusion)
- [References](#references)

## Setting up a .NET project

In this blogpost I'm going to use ASP.NET Core MVC as an example but what you'll see applies to Razor Pages and Blazor or any other MSBuild supported project as well.

Let's create [a new ASP.NET Core MVC project](https://learn.microsoft.com/en-us/aspnet/core/tutorials/first-mvc-app/start-mvc?view=aspnetcore-8.0):

`dotnet new mvc -o TailwindDotnet`

You can open the project in your favorite editor, e.g. Visual Studio Code:

`code TailwindDotnet`

Since the scaffolded MVC project contains Bootstrap and several other files that are not interesting for us at the moment, let's remove those references from our CSHTML files.

After the clean up, the `_Layout.cshtml` file in `~/Views/Shared` now looks like this:

```html
<!DOCTYPE html>
<html lang="en">
  <head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>@ViewData["Title"] - TailwindDotnet</title>
    <link rel="stylesheet" href="~/css/site.css" asp-append-version="true" />
    <link
      rel="stylesheet"
      href="~/TailwindDotnet.styles.css"
      asp-append-version="true"
    />
  </head>
  <body>
    <div>
      <main>@RenderBody()</main>
    </div>
    <script src="~/js/site.js" asp-append-version="true"></script>
  </body>
</html>
```

My `Index.cshtml` file in `~/Views/Home` now looks like:

```html
@{ ViewData["Title"] = "Home Page"; }

<div>
  <h1>Welcome</h1>
  <p>
    Learn about
    <a href="https://learn.microsoft.com/aspnet/core"
      >building Web apps with ASP.NET Core</a
    >.
  </p>
</div>
```

This should give a rather empty index page to look at. When you run the application, it will look something like this:
![empty-mvc](/assets/images/2024-06-13-tailwind-using-msbuild-in-dotnet/empty-mvc.png)

Alright, now that we have our empty MVC project set up, let's get Tailwind!

## Getting the Tailwind CLI

As mentioned in the [Introduction](#introduction), Tailwind CSS is usually installed as an NPM package. However, for projects where Node will otherwise not be required, such as our case here, there's a standalone CLI available that does not require Node JS.

You can find more information in this blogpost, including a download link to the CLI: https://tailwindcss.com/blog/standalone-cli.

Grab the CLI for your operating system and architecture from [their Github release page](https://github.com/tailwindlabs/tailwindcss/releases).

For now I'm going to grab the 64-bit Windows executable but we'll take a look on how to support multiple operating systems and architectures in [Supporting multiple operating systems and architectures](#supporting-multiple-operating-systems-and-architectures) later.

After downloading `tailwindcss-windows-x64.exe`, let's rename it to `tailwindcss.exe` for simplicity.
Let's add the `tailwindcss.exe` file to the root of our project (`~`).

## Preparing the project for Tailwind

Now that we have the Tailwind CLI available for us, let's navigate to the root of our project and initiate the Tailwind CSS configuration, by running: `.\tailwindcss init`.

This will create an empty `tailwind.config.js` file at the root of our project (or wherever you ran the previous command), looking like this:

```js
/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [],
  theme: {
    extend: {},
  },
  plugins: [],
};
```

For a more in-depth look into the configuration on Tailwind, please take a look at [their documentation](https://tailwindcss.com/docs/configuration).

At the core of this configuration file is the `content` property. This is an array of glob-supported paths where Tailwind should scan files for utility classes.

This means that we need to update this property with our Razor files. Let's add the following path to this property:
`"./Views/**/*.cshtml"`. This tells the Tailwind CLI to scan all CSHTML files in all subdirectories of the `~/Views` directory.

Your Tailwind config should now look like:

```js
/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ["./Views/**/*.cshtml"],
  theme: {
    extend: {},
  },
  plugins: [],
};
```

Next we have to update our CSS file to include the Tailwind directives so Tailwind can compile the CSS.
Let's head over to our `site.css` file in `~/wwwroot/css`.

We'll have to add the following directives:

```css
@tailwind base;
@tailwind components;
@tailwind utilities;
```

Let's add those to the top, my `site.css` file now looks like this:

```css
@tailwind base;
@tailwind components;
@tailwind utilities;

html {
  font-size: 14px;
}

@media (min-width: 768px) {
  html {
    font-size: 16px;
  }
}

.btn:focus,
.btn:active:focus,
.btn-link.nav-link:focus,
.form-control:focus,
.form-check-input:focus {
  box-shadow: 0 0 0 0.1rem white, 0 0 0 0.25rem #258cfb;
}

html {
  position: relative;
  min-height: 100%;
}

body {
  margin-bottom: 60px;
}
```

We're almost there! Now let's add some utility classes so Tailwind has something to do.

Head over to our `_Layout.cshtml` file in `~/Views/Shared` and add some Tailwind classes.
I have added some background color and a container to our body and div elements.

My file now looks like:

```html
<!DOCTYPE html>
<html lang="en">
  <head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>@ViewData["Title"] - TailwindDotnet</title>
    <link rel="stylesheet" href="~/css/site.css" asp-append-version="true" />
    <link
      rel="stylesheet"
      href="~/TailwindDotnet.styles.css"
      asp-append-version="true"
    />
  </head>
  <body class="bg-slate-900">
    <div class="container mx-auto text-white">
      <main>@RenderBody()</main>
    </div>
    <script src="~/js/site.js" asp-append-version="true"></script>
  </body>
</html>
```

Okay, so now we have some classes for Tailwind to transform. Let's run the CLI to generate our output CSS:

`.\tailwindcss -i .\wwwroot\css\site.css -o .\wwwroot\css\output.css --minify`

If everything went well, you should see a new file popup in your `~/wwwroot/css` folder: `output.css`.

Let's link our new generated file in our `_Layout.cshtml` file by adding the following line as the first stylesheet:

```html
<link rel="stylesheet" href="~/css/output.css" asp-append-version="true" />
```

Making your `_Layout.cshtml` file now look like:

```html
<!DOCTYPE html>
<html lang="en">
  <head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>@ViewData["Title"] - TailwindDotnet</title>
    <link rel="stylesheet" href="~/css/output.css" asp-append-version="true" />
    <link rel="stylesheet" href="~/css/site.css" asp-append-version="true" />
    <link
      rel="stylesheet"
      href="~/TailwindDotnet.styles.css"
      asp-append-version="true"
    />
  </head>
  <body class="bg-slate-900">
    <div class="container mx-auto text-white">
      <main>@RenderBody()</main>
    </div>
    <script src="~/js/site.js" asp-append-version="true"></script>
  </body>
</html>
```

Let's run the project and you should see something like:

![tailwind-mvc](/assets/images/2024-06-13-tailwind-using-msbuild-in-dotnet/tailwind-mvc.png)

Congratulations! You now have Tailwind running without Node JS in an ASP.NET Core MVC application.

However, it's rather annoying to generate the Tailwind CSS output manually every time you make a change. No way we're going to do that!

Let's take a look how to [automate it with MSBuild](#setting-up-an-msbuild-action) in the next step.

## Setting up an MSBuild action

Open up the project file or your web project (e.g. `~/TailwindDotnet.csproj`).

It looks something like this:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

</Project>
```

We're going to add a [Target](https://learn.microsoft.com/en-us/visualstudio/msbuild/target-element-msbuild?view=vs-2022) that executes before the building of the project.

Add the following Target to the CSPROJ file:

```xml
<Target Name="Tailwind" BeforeTargets="Build">
  <Exec Command="tailwindcss.exe -i ./wwwroot/css/site.css -o ./wwwroot/css/output.css --minify" />
</Target>
```

This target will run before the build of the project and executes the command we were running manually in the previous step.

To make things complete, we can tell MSBuild to always execute the targets when doing a fast build (e.g. when there are not a lot of changes). So when our `output.css` or our `tailwind.config.js` is changed, we'd like to make sure that our Tailwind Target gets executed.

We can do so by adding an [ItemGroup](https://learn.microsoft.com/en-us/visualstudio/msbuild/itemgroup-element-msbuild?view=vs-2022) with two [UpToDateCheckBuilt](https://github.com/dotnet/project-system/blob/main/docs/up-to-date-check.md) elements:

```xml
<ItemGroup>
  <UpToDateCheckBuilt Include="wwwroot/css/site.css" Set="Css" />
  <UpToDateCheckBuilt Include="wwwroot/css/output.css" Set="Css" />
  <UpToDateCheckBuilt Include="Tailwind/tailwind.config.js" Set="Css" />
</ItemGroup>
```

To verify things work, let's change our background color in our `_Layout.cshtml` file to `bg-zinc-600`. After that, build and run the project and verify you see the new background color:

![zinc-mvc](/assets/images/2024-06-13-tailwind-using-msbuild-in-dotnet/zinc-mvc.png)

Nice!

We now have a working Tailwind CSS framework using ASP.NET Core MVC without using NodeJS or NPM. Every time the `output.css` or `site.css` is changed due to a change in utility classes or the `tailwind.config.js` is changed, MSBuild will automatically recompile the Tailwind output CSS.

> "But Alex, do I look like Bill Gates? I'd like to do this on Linux or my Mac!"
>
> > "No worries, I got you covered, in the next step we'll support different operating systems and architectures through MSBuild!"

## Supporting multiple operating systems and architectures

Now that we know how to set everything up for Windows (❤️ Microsoft), let's also take a look on how to make this generic in such a way that we can support Windows, Linux and OSX through MSBuild and the different CLI executables from Tailwind.

Let's grab all the executables we'd like to support from [their Github release page](https://github.com/tailwindlabs/tailwindcss/releases).

In case of this demo, I'm going to support the following:

| Operating system | Architecture | Tailwind executable           |
| ---------------- | ------------ | ----------------------------- |
| Linux            | x64          | tailwindcss-linux-x64         |
| Linux            | Arm64        | tailwindcss-linux-arm64       |
| OSX              | x64          | tailwindcss-macos-x64         |
| OSX              | Arm64        | tailwindcss-macos-arm64       |
| Windows          | x64          | tailwindcss-windows-x64.exe   |
| Windows          | Arm64        | tailwindcss-windows-arm64.exe |

We'll create a new folder in the root of our ASP.NET Core MVC project called: `Tailwind`.

If you've followed along with all the other steps, let's remove our existing `tailwindcss.exe` file in our project root (`~`). Copy the `tailwind.config.js` file to this new folder and update the `content` property so that the path is now properly pointing to the Views:

```js
/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ["./../Views/**/*.cshtml"],
  theme: {
    extend: {},
  },
  plugins: [],
};
```

If you haven't followed along, generate a Tailwind config. Take a look at [Preparing the project for Tailwind](#preparing-the-project-for-tailwind) if you want to know how.

Place all the downloaded executables from the Tailwind Github page in the `~/Tailwind` folder. Note that this time we won't rename the executables.

Open up the project file or your web project (e.g. `~/TailwindDotnet.csproj`) again. If you've followed along previously, remove the existing Tailwind target from the file.

Before we create any targets, we're first going to make some variables to determine the operating system and architecture of our system.

You can do so by creating a [PropertyGroup](https://learn.microsoft.com/en-us/visualstudio/msbuild/propertygroup-element-msbuild?view=vs-2022) with custom properties.

For each property, we'll return `true` if a certain `Condition` is met. We can add properties for every operating system and architecture combination:

```xml
<PropertyGroup>
  <IsLinuxX64 Condition="$([MSBuild]::IsOsPlatform('Linux')) And $([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture) == X64">true</IsLinuxX64>
  <IsLinuxArm64 Condition="$([MSBuild]::IsOsPlatform('Linux')) And $([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture) == Arm64">true</IsLinuxArm64>
  <IsOsxX64 Condition="$([MSBuild]::IsOsPlatform('OSX')) And $([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture) == X64">true</IsOsxX64>
  <IsOsxArm64 Condition="$([MSBuild]::IsOsPlatform('OSX')) And $([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture) == Arm64">true</IsOsxArm64>
  <IsWindowsX64 Condition="$([MSBuild]::IsOsPlatform('Windows')) And $([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture) == X64">true</IsWindowsX64>
  <IsWindowsArm64 Condition="$([MSBuild]::IsOsPlatform('Windows')) And $([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture) == Arm64">true</IsWindowsArm64>
</PropertyGroup>
```

<sub>_You don't need these property groups, you can also set these conditions inline on the targets, but this way it's a bit easier to maintain in my opinion._</sub>

Once we have the property groups, we can create the targets like we did before. However, for Linux and OSX we'll also need to give the executable the proper permissions, so we'll need an additional [Exec](https://learn.microsoft.com/en-us/visualstudio/msbuild/exec-task?view=vs-2022) property for these targets.

The targets can be defined like this:

```xml
<Target Name="TailwindLinuxX64" BeforeTargets="Build" Condition="$(IsLinuxX64) == true">
  <Exec WorkingDirectory="./Tailwind" Command="chmod +x ./tailwindcss-linux-x64" />
  <Exec WorkingDirectory="./Tailwind" Command="./tailwindcss-linux-x64 -i ./../wwwroot/css/site.css -o ./../wwwroot/css/output.css --minify" />
</Target>
<Target Name="TailwindLinuxArm64" BeforeTargets="Build" Condition="$(IsLinuxArm64) == true">
  <Exec WorkingDirectory="./Tailwind" Command="chmod +x ./tailwindcss-linux-arm64" />
  <Exec WorkingDirectory="./Tailwind" Command="./tailwindcss-linux-arm64 -i ./../wwwroot/css/site.css -o ./../wwwroot/css/output.css --minify" />
</Target>
<Target Name="TailwindOsxX64" BeforeTargets="Build" Condition="$(IsOsxX64) == true">
  <Exec WorkingDirectory="./Tailwind" Command="chmod +x ./tailwindcss-osx-x64" />
  <Exec WorkingDirectory="./Tailwind" Command="./tailwindcss-osx-x64 -i ./../wwwroot/css/site.css -o ./../wwwroot/css/output.css --minify" />
</Target>
<Target Name="TailwindOsxArm64" BeforeTargets="Build" Condition="$(IsOsxArm64) == true">
  <Exec WorkingDirectory="./Tailwind" Command="chmod +x ./tailwindcss-osx-arm64" />
  <Exec WorkingDirectory="./Tailwind" Command="./tailwindcss-osx-arm64 -i ./../wwwroot/css/site.css -o ./../wwwroot/css/output.css --minify" />
</Target>
<Target Name="TailwindWindowsX64" BeforeTargets="Build" Condition="$(IsWindowsX64) == true">
  <Exec WorkingDirectory="./Tailwind" Command="tailwindcss-windows-x64.exe -i ./../wwwroot/css/site.css -o ./../wwwroot/css/output.css --minify" />
</Target>
<Target Name="TailwindWindowsArm64" BeforeTargets="Build" Condition="$(IsWindowsArm64) == true">
  <Exec WorkingDirectory="./Tailwind" Command="tailwindcss-windows-arm64.exe -i ./../wwwroot/css/site.css -o ./../wwwroot/css/output.css --minify" />
</Target>
```

If you now build on your machine, whether it's OSX, Linux or Windows, you should still see the same result.
You can try and change some utility classes to verify it's working.

Your final `csproj` XML file should look like this:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

	<PropertyGroup>
		<TargetFramework>net8.0</TargetFramework>
		<Nullable>enable</Nullable>
		<ImplicitUsings>enable</ImplicitUsings>
	</PropertyGroup>

	<ItemGroup>
		<UpToDateCheckBuilt Include="wwwroot/css/site.css" Set="Css" />
		<UpToDateCheckBuilt Include="wwwroot/css/output.css" Set="Css" />
		<UpToDateCheckBuilt Include="Tailwind/tailwind.config.js" Set="Css" />
	</ItemGroup>

	<PropertyGroup>
		<IsLinuxX64 Condition="$([MSBuild]::IsOsPlatform('Linux')) And $([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture) == X64">true</IsLinuxX64>
		<IsLinuxArm64 Condition="$([MSBuild]::IsOsPlatform('Linux')) And $([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture) == Arm64">true</IsLinuxArm64>
		<IsOsxX64 Condition="$([MSBuild]::IsOsPlatform('OSX')) And $([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture) == X64">true</IsOsxX64>
		<IsOsxArm64 Condition="$([MSBuild]::IsOsPlatform('OSX')) And $([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture) == Arm64">true</IsOsxArm64>
		<IsWindowsX64 Condition="$([MSBuild]::IsOsPlatform('Windows')) And $([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture) == X64">true</IsWindowsX64>
		<IsWindowsArm64 Condition="$([MSBuild]::IsOsPlatform('Windows')) And $([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture) == Arm64">true</IsWindowsArm64>
	</PropertyGroup>

	<Target Name="TailwindLinuxX64" BeforeTargets="Build" Condition="$(IsLinuxX64) == true">
		<Exec WorkingDirectory="./Tailwind" Command="chmod +x ./tailwindcss-linux-x64" />
		<Exec WorkingDirectory="./Tailwind" Command="./tailwindcss-linux-x64 -i ./../wwwroot/css/site.css -o ./../wwwroot/css/output.css --minify" />
	</Target>
	<Target Name="TailwindLinuxArm64" BeforeTargets="Build" Condition="$(IsLinuxArm64) == true">
		<Exec WorkingDirectory="./Tailwind" Command="chmod +x ./tailwindcss-linux-arm64" />
		<Exec WorkingDirectory="./Tailwind" Command="./tailwindcss-linux-arm64 -i ./../wwwroot/css/site.css -o ./../wwwroot/css/output.css --minify" />
	</Target>
	<Target Name="TailwindOsxX64" BeforeTargets="Build" Condition="$(IsOsxX64) == true">
		<Exec WorkingDirectory="./Tailwind" Command="chmod +x ./tailwindcss-osx-x64" />
		<Exec WorkingDirectory="./Tailwind" Command="./tailwindcss-osx-x64 -i ./../wwwroot/css/site.css -o ./../wwwroot/css/output.css --minify" />
	</Target>
	<Target Name="TailwindOsxArm64" BeforeTargets="Build" Condition="$(IsOsxArm64) == true">
		<Exec WorkingDirectory="./Tailwind" Command="chmod +x ./tailwindcss-osx-arm64" />
		<Exec WorkingDirectory="./Tailwind" Command="./tailwindcss-osx-arm64 -i ./../wwwroot/css/site.css -o ./../wwwroot/css/output.css --minify" />
	</Target>
	<Target Name="TailwindWindowsX64" BeforeTargets="Build" Condition="$(IsWindowsX64) == true">
		<Exec WorkingDirectory="./Tailwind" Command="tailwindcss-windows-x64.exe -i ./../wwwroot/css/site.css -o ./../wwwroot/css/output.css --minify" />
	</Target>
	<Target Name="TailwindWindowsArm64" BeforeTargets="Build" Condition="$(IsWindowsArm64) == true">
		<Exec WorkingDirectory="./Tailwind" Command="tailwindcss-windows-arm64.exe -i ./../wwwroot/css/site.css -o ./../wwwroot/css/output.css --minify" />
	</Target>

</Project>
```

## Conclusion

In this blogpost we've seen how to set up the Tailwind CLI for .NET projects using MSBuild and even how to support different platforms through conditional targets.

This allows developers that would otherwise install NodeJS and create NPM projects to easily use Tailwind CSS in their projects without installing extra dependencies.

As always with my blogposts, the full code is available in the repository of this site: [physer.github.io](https://github.com/Physer/physer.github.io/tree/main/code/2024-06-13-tailwind-using-msbuild-in-dotnet).

## References

- [Tailwind CSS](https://tailwindcss.com/)
- [MSBuild](https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild?view=vs-2022)
