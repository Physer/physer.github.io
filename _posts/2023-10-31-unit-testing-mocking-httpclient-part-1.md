---
layout: post
title:  "Mocking HTTP calls in typed clients in Unit Tests - Part 1"
date:   2023-10-31 15:33 +0200
categories: unit-testing
---

## Introduction

We all know and love our unit tests. They're excellent for providing your code with maintainability and separation of concerns.
Obviously, unit tests are also great to prevent changes from breaking existing code.

One of the more interesting parts of unit testing is to mock HTTP calls in [Typed HTTP Clients](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/implement-resilient-applications/use-httpclientfactory-to-implement-resilient-http-requests#how-to-use-typed-clients-with-ihttpclientfactory).

In this series of blog posts, I will explain how we can easily mock any HTTP call made in a typed HTTP client.
We will create a basic implementation in these series. In a future blog post, we'll refactor it to a reusable utility for one or multiple projects.

This blog post will assume you have basic knowledge about unit testing and mocking, including mocking frameworks such as Moq (not version 4.20! 😉) or NSubstitute.
Additionally, this blog post assumes you understand how the Arrange, Act and Assert pattern works.
In case any of these concepts are new to you, I'll make a blog post in the future about the basics of unit testing and how to properly structure them.

In this first part of the series, we'll focus on building an implementation of an API that retrieves user information.

In my examples I will use the following libraries and frameworks:

* [XUnit](https://xunit.net/)
* [FluentAssertions](https://fluentassertions.com/)

A quick table of contents:

* [Introduction](#introduction)
* [Terminology](#terminology)
* [User API implementation](#user-api-implementation)

## Terminology

Let's make sure we're all on the same page here when we're referring to things using the words 'fake', 'stub' or 'mock'.
According to [Microsoft's best practices on Unit Testing](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices#lets-speak-the-same-language), the following definitions apply:

> Fake - A fake is a generic term that can be used to describe either a stub or a mock object. Whether it's a stub or a mock depends on the context in which it's used. So in other words, a fake can be a stub or a mock.
>
> Mock - A mock object is a fake object in the system that decides whether or not a unit test has passed or failed. A mock starts out as a Fake until it's asserted against.
>
> Stub - A stub is a controllable replacement for an existing dependency (or collaborator) in the system. By using a stub, you can test your code without dealing with the dependency directly. By default, a stub starts out as a fake.

We will proceed using these definitions for our components.

## User API implementation

Let's take a look at how we would typically implement a Typed HTTP Client.
For this example, we'll create an API that retrieves some user information from an online datastore with fake data.

If you're following along but you'd like to use different templates, frameworks or libraries - feel free to do so.
I'll explain the things I do in code to make it tool-agnostic.

We'll begin by creating an empty ASP.NET Core project: `dotnet new web --name API`.

Now that we have our empty project, let's add a Unit Test project with XUnit: `dotnet new xunit --name UnitTests`.
Additionally, we'll install NSubstitute and its analyzers in our `UnitTests` project: `dotnet add package NSubstitute` and `dotnet add package NSubstitute.Analyzers.CSharp`.
Note that these analyzers aren't mandatory by any means, I just like having them in my project because they can warn me upfront when I'm trying something funky.
We'll also place a reference to our `API` project in our `UnitTests` project so we can access our 'system under test'.

Cool! Now that we've got our projects set up and ready to go, let's create something to retrieve user data with.
When dealing with retrieving data over HTTP, I like to use [JSONPlaceholder](https://jsonplaceholder.typicode.com/). This is a free online REST API that you can use to get fake data with different data sets.

In our `API` project, let's create a `UsersRepository` class. This `UsersRepository` is going to be a typed HTTP client.
Since it's a typed client, we will have direct access to the HTTP Client class through constructor injection.
We'll do the set-up a typed client later, so for now go ahead and add a field for the HTTP Client in the `UsersRepository` like so:

```csharp
namespace API;

public class UsersRepository
{
    private readonly HttpClient _httpClient;

    public UsersRepository(HttpClient httpClient) => _httpClient = httpClient;
}

```

Okay! Now let's create a small model for our user data.
The data we'll be using is located at this endpoint: [https://jsonplaceholder.typicode.com/users](https://jsonplaceholder.typicode.com/users).
We won't need everything for this exercise, so just get a couple of properties here and there.
I'll be using this model:

```csharp
namespace API;

public record struct User(string Name, string Email);
```

Next up, let's create a method to actually get some users from JSONPlaceholder.
We'll create a method called: `GetUsersAsync`.
This method will be responsible for retrieving and deserializing the HTTP response from JSONPlaceholder.
I've implemented it like this:

```csharp
public async Task<IEnumerable<User>> GetUsersAsync() => await _httpClient.GetFromJsonAsync<IEnumerable<User>>("/users") ?? Array.Empty<User>();
```

Okay! Now that we've got our logic set, let's create an endpoint in our API that actually uses this.
Let's go to our `Program.cs` file and create an endpoint for retrieving users.
First we'll have to wire up our `UsersRepository` as a typed HTTP client, as mentioned before!
We can do so using an extension method on the `IServiceCollection` interface:

```csharp
builder.Services.AddHttpClient<UsersRepository>(configuration => configuration.BaseAddress = new Uri("https://jsonplaceholder.typicode.com"));
```

Next, we'll create an endpoint that leverages our service through Dependency Injection and calls the `GetUsersAsync` method:

```csharp
app.MapGet("/users", async ([FromServices]UsersRepository usersRepository) => await usersRepository.GetUsersAsync());
```

This should get you to the point where you can run the API locally, navigate to `/users` and see some user data on your screen.
If you've followed along with me, you should see something along the lines of:

```json
[
    {
        "name": "Leanne Graham",
        "email": "Sincere@april.biz"
    },
    {
        "name": "Ervin Howell",
        "email": "Shanna@melissa.tv"
    },
    {
        "name": "Clementine Bauch",
        "email": "Nathan@yesenia.net"
    },
    {
        "name": "Patricia Lebsack",
        "email": "Julianne.OConner@kory.org"
    },
    {
        "name": "Chelsey Dietrich",
        "email": "Lucio_Hettinger@annie.ca"
    },
    {
        "name": "Mrs. Dennis Schulist",
        "email": "Karley_Dach@jasper.info"
    },
    {
        "name": "Kurtis Weissnat",
        "email": "Telly.Hoeger@billy.biz"
    },
    {
        "name": "Nicholas Runolfsdottir V",
        "email": "Sherwood@rosamond.me"
    },
    {
        "name": "Glenna Reichert",
        "email": "Chaim_McDermott@dana.io"
    },
    {
        "name": "Clementina DuBuque",
        "email": "Rey.Padberg@karina.biz"
    }
]
```

Alright! Whether you've followed along with me for the implementation is actually not very relevant.
The important thing is that you have a method somewhere that uses an _injected_ `HttpClient` class to retrieve some data.
That's what we're going to mock in our Unit Test. Whether you use `GetAsync`, `SendAsync`, `PostAsync` or any other method from the injected `HttpClient` class doesn't matter either.

That's it! We now have an implementation that we can start to write some tests for.

Go to part 2: [Mocking HTTP calls in typed clients in Unit Tests - Part 2](https://blog.alexschouls.com/unit-testing/2023/10/31/unit-testing-mocking-httpclient-part-2.html).