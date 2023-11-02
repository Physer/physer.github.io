---
layout: post
title:  "Mocking HTTP calls in typed clients in Unit Tests - Part 2"
date:   2023-10-31 15:33 +0200
categories: unit-testing
---

## Introduction

Welcome to the second part of the series on how to mock HTTP calls in typed HTTP clients.
In case you missed part 1, here's a link: [Mocking HTTP calls in typed clients in Unit Tests - Part 1](https://blog.alexschouls.com/unit-testing/2023/10/31/unit-testing-mocking-httpclient-part-1.html).

In this part we'll try to write a unit test the way we would normally do with dependencies.
We'll dive deeper into the problem and why it doesn't work.

In part 3 and the last part of this series, we'll fix this problem and change our unit test so it's mocking an HTTP response.

A quick table of contents:

* [Introduction](#introduction)
* [Writing a unit test - the regular way](#writing-a-unit-test---the-regular-way)
* [The problem](#the-problem)

## Writing a unit test - the regular way

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

## The problem

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

As you can see from this approach, this won't work the way we want to!
Now we're stuck with a non-working unit test that doesn't really do anything for us.

Let's take a look in the next part of the series on how to properly fix this and turn this around!

Go to part 3: [Mocking HTTP calls in typed clients in Unit Tests - Part 3](https://blog.alexschouls.com/unit-testing/2023/10/31/unit-testing-mocking-httpclient-part-3.html).