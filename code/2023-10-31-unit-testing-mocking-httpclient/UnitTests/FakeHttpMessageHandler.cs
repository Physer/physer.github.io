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
