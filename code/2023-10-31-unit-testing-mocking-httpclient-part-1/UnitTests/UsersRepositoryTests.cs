using API;
using FluentAssertions;
using Xunit;

namespace UnitTests;

public class UsersRepositoryTests
{
    [Fact]
    public async Task GetUsersAsync_WithSuccessResponse_ShouldReturnUsers()
    {
        // Arrange
        var expectedUsers = new List<User>
        {
            new("Glenna Reichert", "Chaim_McDermott@dana.io"),
            new("Clementina DuBuque", "Rey.Padberg@karina.biz")
        };

        var httpMessageHandler = new FakeHttpMessageHandler();
        var httpClient = new HttpClient(httpMessageHandler) { BaseAddress = new Uri("http://unit.testing.local") };
        var usersRepository = new UsersRepository(httpClient);

        // Act
        var actualUsers = await usersRepository.GetUsersAsync();

        // Assert
        actualUsers.Should().BeEquivalentTo(expectedUsers);
    }
}
