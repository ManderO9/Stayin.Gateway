using Microsoft.Extensions.Primitives;
using System.Buffers;
using System.IO.Pipelines;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var httpClient = new HttpClient();

var services = new List<(string path, string url)>()
{
    ("/auth", "https://localhost:7000"),
    ("/reservation", "https://localhost:5010"),
};


// TODO: Add authorization before sending any request
// For all routes that hit the app
app.MapFallback(async (HttpRequest Request, HttpResponse Response) =>
{
    // For each microservice we registered in our services
    foreach(var service in services)
    {
        // If the request path starts with the microservice path
        if(Request.Path.Value is not null &&
            Request.Path.Value.StartsWith(service.path, StringComparison.OrdinalIgnoreCase))
        {
            // Create a new http request
            var newRequest = new HttpRequestMessage();

            // Set the method of the request
            newRequest.Method = new HttpMethod(Request.Method);
            
            // Set the uri of the request
            newRequest.RequestUri = new Uri(service.url + Request.Path.Value.Substring(service.path.Length));

            // Add all the requests headers to the new request
            foreach(var header in Request.Headers)
                newRequest.Headers.Add(header.Key, header.Value.AsEnumerable());

            // Set the content of the request
            newRequest.Content = new StreamContent(Request.BodyReader.AsStream());

            // Send the request to the microservice and get a response
            // TODO: try catch this b
            var response = await httpClient.SendAsync(newRequest);

            // Set the status code of the response to return
            Response.StatusCode = ((int)response.StatusCode);

            // Add the response headers to the response
            foreach(var header in response.Headers)
                Response.Headers.Append(header.Key, new StringValues(header.Value.ToArray()));

            // Write the response body to the response we want to return
            await response.Content.CopyToAsync(Response.Body);

            // Stop the request here
            return;
        }
    }

    // If we matched no microservice, return an error
    await Response.WriteAsync($"Requested Path: [{Request.Path}]" +
        " -- The requested path doesn't exist, something went wrong");

});

app.Run();
