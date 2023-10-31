namespace API;

public class UsersRepository
{
    private readonly HttpClient _httpClient;

    public UsersRepository(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<IEnumerable<User>> GetUsersAsync() => await _httpClient.GetFromJsonAsync<IEnumerable<User>>("/users") ?? Array.Empty<User>();
}
