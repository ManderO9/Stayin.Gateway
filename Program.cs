using System.Buffers;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var httpClient = new HttpClient();

var services = new List<(string path, string url)>()
{
    ("/auth", "https://localhost:7000"),
    ("/reservation", "https://localhost:5010"),
};


app.MapFallback(async (context) =>
{

    foreach(var service in services)
    {
        if(context.Request.Path.Value is not null &&
            context.Request.Path.Value.StartsWith(service.path, StringComparison.OrdinalIgnoreCase))
        {
            var newRequest = new HttpRequestMessage();

            newRequest.Method = new HttpMethod(context.Request.Method);
            newRequest.RequestUri = new Uri(service.url + context.Request.Path.Value.Substring(service.path.Length));

            foreach(var header in context.Request.Headers)
            {
                newRequest.Headers.Add(header.Key, header.Value.AsEnumerable());
            }

            var readResult = await context.Request .BodyReader.ReadAsync();

            newRequest.Content = new ByteArrayContent(readResult.Buffer.ToArray());

            var response = await httpClient.SendAsync(newRequest);



            context.Response.StatusCode = ((int)response.StatusCode);

            foreach(var header in response.Headers)
            {
                context.Response.Headers.Add(header.Key, new Microsoft.Extensions.Primitives.StringValues(header.Value.ToArray()));
            }

            await context.Response.Body.WriteAsync((await response.Content.ReadAsByteArrayAsync()));
            return;
        }
    }


    await context.Response.WriteAsync($"Requested Path: [{context.Request.Path}]" +
        " -- The requested path doesn't exist, something went wrong");




});

app.Run();
