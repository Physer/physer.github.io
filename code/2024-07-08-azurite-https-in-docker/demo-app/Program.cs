using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Azure;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAzureClients(clientBuilder =>
{
  clientBuilder.AddBlobServiceClient(builder.Configuration.GetSection("Storage"));
  clientBuilder.UseCredential(new DefaultAzureCredential());
});
var app = builder.Build();

app.MapGet("/blob", ([FromServices] BlobServiceClient blobServiceClient) =>
{
  BlobContainerClient blobContainerClient = blobServiceClient.GetBlobContainerClient("demo");
  blobContainerClient.CreateIfNotExists();

  var blob = blobContainerClient.GetBlobs()?.FirstOrDefault();
  return blob is null ? Results.Ok("No blob item available") : Results.Ok(blob);
});
app.MapGet("/", () => "Hello World!");

app.Run();
