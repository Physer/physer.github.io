---
layout: post
title:  "Unit testing - Mocking HTTP calls in typed clients (part 1)"
date:   2023-10-31 15:33 +0200
categories: unit-testing
---

## Introduction

We all know and love our unit tests. They're excellent for providing your code with maintainability and separation of concerns.
Obviously, unit tests are also great to prevent changes from breaking existing code.

One of the more interesting parts of unit testing is to mock HTTP calls in [Typed HTTP Clients](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/implement-resilient-applications/use-httpclientfactory-to-implement-resilient-http-requests#how-to-use-typed-clients-with-ihttpclientfactory).

In this series of blog posts, I will explain how we can easily mock any HTTP call made in a typed HTTP client.
We'll start out with some basics and dive into expanding upon our mock implementation in future parts.

This blog post will assume you have basic knowledge about unit testing and mocking, including mocking frameworks such as Moq (not version 4.20! 😉) or NSubstitute.
Additionally, this blog post assumes you understand how the Arrange, Act and Assert pattern works.
In case any of these concepts are new to you, I'll make a blog post in the future about the basics of unit testing and how to properly structure them.

In my examples I will use the following libraries and frameworks:

* [XUnit](https://xunit.net/)
* [FluentAssertions](https://fluentassertions.com/)

A quick table of contents:

* [Introduction](#introduction)
* [Terminology](#terminology)
* [The juicy details](#the-juicy-details)
* [Conclusion](#conclusion)
* [References](#references)

## Terminology

Let's make sure we're all on the same page here when we're referring to things using the works 'fake', 'stub' or 'mock'.
According to [Microsoft's best practices on Unit Testing](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices#lets-speak-the-same-language), the following definitions apply:

> Fake - A fake is a generic term that can be used to describe either a stub or a mock object. Whether it's a stub or a mock depends on the context in which it's used. So in other words, a fake can be a stub or a mock.
>
> Mock - A mock object is a fake object in the system that decides whether or not a unit test has passed or failed. A mock starts out as a Fake until it's asserted against.
>
> Stub - A stub is a controllable replacement for an existing dependency (or collaborator) in the system. By using a stub, you can test your code without dealing with the dependency directly. By default, a stub starts out as a fake.

We will proceed using these definitions for our components.

## The juicy details

Alright, let's get started with some examples and take a look at how we can successfully mock our HTTP calls.

### Our implementation

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

### Unit testing our code

Now we're going to take a look at what we want to test and what we should have to mock.
Since this is a demo project, our code isn't very complicated and doesn't have some business logic.
Regardless of the complexity though, our steps to determine what to test and what to mock will be the same.

We have one file with logic in it: our `UsersRepository` class.
This class has a single method inside of it: `GetUsersAsync`.

This method has no parameters and doesn't depend on any input other than the injected HTTP client.
We're dealing with unit tests. These tests should always be isolated and testing code units as small as possible.
Since all our method does is retrieving some user data from an external source and transforming it our user model, we can state that our test should verify that, when we retrieve data from an external source, it will be transformed into our user model and returned as a collection of `User` objects.

We can write some other tests for our code, like what happens when there is no HTTP connection, or when the data cannot be deserialized but those are out of scope for the purpose of this post.

As we're dealing with a unit test, we want to leave the actual HTTP connection out of scope and 'pretend' like it's been successful. After all, we're testing if we're able to get the right data back when the external API call is giving us the data. That's our hypothesis.

This does mean that we want to mock our HTTP client.
If we look at our method that we're unit testing we can determine we'll have to mock the `GetFromJsonAsync` method:

```csharp
public async Task<IEnumerable<User>> GetUsersAsync() => await _httpClient.GetFromJsonAsync<IEnumerable<User>>("/users") ?? Array.Empty<User>();
```

The way I like to write unit tests is by declaring what is being tested, with any potential data and what it should do.
We'll name our unit test: `GetUsersAsync_WithSuccessResponse_ShouldReturnUsers`.

This naming convention makes it clear what we're testing, under which condition and what it should do.
Let's get to work!

We'll open our `UnitTests` project and create a new file: `UsersRepositoryTests`.
In every test, I always like to put in the AAA comments to make sure I divide my test up properly.
Our test skeleton looks something like this:

```csharp
namespace UnitTests;

public class UsersRepositoryTests
{
    public async Task GetUsersAsync_WithSuccessResponse_ShouldReturnUsers()
    {
        // Arrange

        // Act

        // Assert
    }
}

```

### The problem

As the comments already suggest, we'll start by arranging our test.
We'll do this by creating a 'system under test' object.
When dealing with a unit test (without any specific patterns - we'll cover things like the builder pattern in a later post), we'll just instantiate the class itself.
If we'd like to do so with our `UsersRepository` class, we want to write something like this in our test:

```csharp
var usersRepository = new UsersRepository();
```

However, our UsersRepository has a dependency on the `HttpClient` class, since it's leveraging constructor injection as a typed HTTP client.
Usually, when dealing with constructor injection in your classes, you're injecting interfaces (e.g. `ILogger<T>` or `IHttpClientFactory`).
These _dependencies_ are generally mocked.

Here we arrive at a crucial point with mocking in general. Mocking frameworks are capable of building a fake object of your choice by implementing an interface during runtime. However, our `HttpClient` class is an actual concrete class and not an interface.

This means that our mocking frameworks such as Moq or NSubstitute won't take kindly to creating a mocked object out of this.

<sub>_There are exceptions to this on virtual methods and some frameworks will support it in a limited way, but it's considered bad practice to mock a concrete class due to unexpected side effects affecting your unit test and potential code being executed whilst you don't expect it._</sub>

This means that, whilst you might be tempted to do something like this, there is a better way of mocking the result of an HTTP client's request.

```csharp
using API;
using NSubstitute;

namespace UnitTests;

public class UsersRepositoryTests
{
    private readonly HttpClient _httpClient;

    public UsersRepositoryTests() => _httpClient = Substitute.For<HttpClient>();

    public async Task GetUsersAsync_WithSuccessResponse_ShouldReturnUsers()
    {
        // Arrange
        var response = new HttpResponseMessage();
        _httpClient.SendAsync(Arg.Any<HttpRequestMessage>()).Returns(response);
        var usersRepository = new UsersRepository(_httpClient);

        // Act

        // Assert
    }
}
```

In case you're using NSubstitute and the analyzers, it'll warn you about using a concrete class' method in your mock: `Member SendAsync can not be intercepted. Only interface members and virtual, overriding, and abstract members can be intercepted.`.

### An inside look into the HTTP Client

Okay... Now that we now 'regular' mocking is not an option for our `HttpClient` class, let's take a look at we're actually dealing with.
We can take a look at the innards of our `HttpClient` to see what makes it tick. If we do that, we might just better understand how to make our own response.

Our `HttpClient` seems to be a partial class.

```csharp
public partial class HttpClient : HttpMessageInvoker
```

We have a couple of constructors:

```csharp
public HttpClient() : this(new HttpClientHandler())
{
}

public HttpClient(HttpMessageHandler handler) : this(handler, true)
{
}

public HttpClient(HttpMessageHandler handler, bool disposeHandler) : base(handler, disposeHandler)
{
    _timeout = s_defaultTimeout;
    _maxResponseContentBufferSize = HttpContent.MaxBufferSize;
    _pendingRequestsCts = new CancellationTokenSource();
}
```

And an absolute ton of methods responsible for sending REST requests such as:

```csharp
public Task<HttpResponseMessage> GetAsync([StringSyntax(StringSyntaxAttribute.Uri)] string? requestUri) => GetAsync(CreateUri(requestUri));
```

Alright, so we know that our `HttpClient` class does not leverage any (useful) virtual methods and does not implement an interface.
Mocking this class doesn't seem to do the trick.

### How to unit test it

If we can't mock the class, then how on earth can we create our own response object from a request?
When we dive deeper into the crevices of our `HttpClient` class we find, in its base the following method:

```csharp
public virtual Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) { ... } // omitted for clarity
```

This `SendAsync` method is eventually called in all the previously mentioned REST requests method. In turn, this method has the following line:

```csharp
return _handler.SendAsync(request, cancellationToken);
```

This is referring to a `HttpMessageHandler` object that gets created in the constructor of the `HttpClient` and in turn the `HttpMessageInvoker`, as visible here in the `HttpMessageInvoker` class:

```csharp
public HttpMessageInvoker(HttpMessageHandler handler, bool disposeHandler)
{
    ArgumentNullException.ThrowIfNull(handler);

    if (NetEventSource.Log.IsEnabled()) NetEventSource.Associate(this, handler);

    _handler = handler;
    _disposeHandler = disposeHandler;
}
```

This is good news! We've found a way to actually hook into the `HttpClient` and determine what it's sending!
Because, what if, we create our _very own_ `HttpMessageHandler` for the sake of our tests and we create an `HttpClient` instance with our custom message handler?

_That_, is how we will manipulate the response data for our `HttpClient` calls.

Let's get cracking! We'll create a new file called `FakeHttpMessageHandler.cs` in our `UnitTests` project.
This fake message handler should, for all intents and purposes, behave like a regular HTTP message handler. So let's inherit from the regular one.
Since the regular `HttpMessageHandler` class has an abstract method that every implementation needs to have, we'll have to implement it as well.
The signature of this method is:

```csharp
protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
```

Are things starting to look familiar yet? 😉

Let's back up a little bit and remind ourselves what we needed for our unit test.
We want our HTTP call from our REST API to return some JSON data that contains a bit of user information.
So we can tell our custom-made HTTP message handler to return just that kind of message!

Let's grab some JSON data from our JsonPlaceholder REST API. Of course, you can also write it yourself or do it differently.
We'll use this data as content for our response message.

After we've prepared our data as a string, we can instantiate a new `HttpResponseMessage` with our data of choice.
My `FakeHttpMessageHandler` class now looks like this:

```csharp
using System.Net;

namespace UnitTests;

internal class FakeHttpMessageHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var jsonUserData = @"
        [    
            {
                ""id"": 9,
                ""name"": ""Glenna Reichert"",
                ""username"": ""Delphine"",
                ""email"": ""Chaim_McDermott@dana.io"",
                ""phone"": ""(775)976-6794 x41206"",
                ""website"": ""conrad.com""
            },
            {
                ""id"": 10,
                ""name"": ""Clementina DuBuque"",
                ""username"": ""Moriah.Stanton"",
                ""email"": ""Rey.Padberg@karina.biz"",
                ""phone"": ""024-648-3804"",
                ""website"": ""ambrose.net""
            }
        ]";

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonUserData)
        });
    }
}
```

Now that we've got our fake message handler ready, let's head back to our `UsersRepositoryTests` file.
Instead of mocking our `HttpClient` class using a framework, we'll create an instance of the class using our own fake message handler.
Do note that we'll require setting a base address for validation purposes.
This makes our 'Arrange' section look something like this:

```csharp
// Arrange
var httpMessageHandler = new FakeHttpMessageHandler();
var httpClient = new HttpClient(httpMessageHandler) { BaseAddress = new Uri("http://unit.testing.local") };
var usersRepository = new UsersRepository(httpClient);
```

Let's add some expectations for our test and assert them against our method.
You'll probably notice some flaws with this approach as we start doing this, but we'll address those in the next part of this series where we'll refactor our message handler in a generic reusable HTTP response building tool.

As mentioned before, we'd like to test that we are getting a collection of `User`s. We now know that our HTTP response will contain the above mentioned JSON data.
So we can create some users with those properties:

```csharp
var expectedUsers = new List<User>
{
    new("Glenna Reichert", "Chaim_McDermott@dana.io"),
    new("Clementina DuBuque", "Rey.Padberg@karina.biz")
};
```

We're done 'Arranging' our test, so now it's time to move to the 'Act' part.
Let's call the actual `GetUsersAsync` method of our `UsersRepository` instance.

```csharp
// Act
var actualUsers = await usersRepository.GetUsersAsync();
```

Once we have those users, we want to assert them against our expected users.
I love using `FluentAssertions` for this, but it's definitely not a requirement.
This results in an 'Assert' phase like this:

```csharp
// Assert
actualUsers.Should().BeEquivalentTo(expectedUsers);
```

Now that our test is finished, let's not forget to decorate it with the `Fact` attribute like so:

```csharp
[Fact]
public async Task GetUsersAsync_WithSuccessResponse_ShouldReturnUsers()
```

Let's run the test (`dotnet test`) and see what happens!

Our test is green ✅!

Voila, that's how we can reliably create our own desired responses of an HTTP request made with a typed HTTP client.

## Conclusion

In this blog post, you've seen the basic approach of create a fake HTTP response for usage in Unit Tests. Although this demo project has very little actual logic to contain in a unit test, the concepts and ideas used are applicable to any size and complexity.

This basic approach has some serious flaws in the implementation when it comes to reusability and maintainability for HTTP responses. The next post in this series will about refactoring this `FakeHttpMessageHandler` into an easy-to-reuse building block of mocking HTTP responses.

### Complete code

As with all my posts, you can find the complete solution for this on my Github account, in the repository of this site: [physer.github.io](https://github.com/Physer/physer.github.io/tree/main/code/2023-10-31-unit-testing-mocking-httpclient-part-1)

## References

* [XUnit](https://xunit.net/)
* [NSubstitute](https://nsubstitute.github.io/)
* [FluentAssertions](https://fluentassertions.com/)
* [JSONPlaceholder](https://jsonplaceholder.typicode.com/)
* [Microsoft's documentation on Typed HTTP clients](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/implement-resilient-applications/use-httpclientfactory-to-implement-resilient-http-requests#how-to-use-typed-clients-with-ihttpclientfactory)
* [Microsoft's guidelines for using HttpClient](https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/http/httpclient-guidelines)
* [Microsoft's best practices for unit testing](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices)
