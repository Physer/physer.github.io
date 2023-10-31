using API;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient<UsersRepository>(configuration => configuration.BaseAddress = new Uri("https://jsonplaceholder.typicode.com"));

var app = builder.Build();

app.MapGet("/", () => "Hello World!");
app.MapGet("/users", async ([FromServices]UsersRepository usersRepository) => await usersRepository.GetUsersAsync());

app.Run();
