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
