using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using System.Buffers;

var builder = WebApplication.CreateBuilder(args);


var corsPolicy = "someCorsPolicy";
builder.Services.AddCors((options) =>
    { options.AddPolicy(corsPolicy, policy => { policy.WithOrigins("*").WithHeaders("*").WithMethods("*"); }); });

var app = builder.Build();
app.UseCors(corsPolicy);

var httpClient = new HttpClient();

var services = new List<(string path, string url)>()
{
    ("/auth", Environment.GetEnvironmentVariable("AuthApp")!),
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

            // Set the content of the request
            newRequest.Content = new StreamContent(Request.BodyReader.AsStream());

            // Add all the requests headers to the new request
            foreach(var header in Request.Headers)
            {
                // If the header is a content header
                if(header.Key.Equals(HeaderNames.ContentType, StringComparison.OrdinalIgnoreCase) ||
                    header.Key.Equals(HeaderNames.ContentDisposition, StringComparison.OrdinalIgnoreCase) ||
                    header.Key.Equals(HeaderNames.ContentEncoding, StringComparison.OrdinalIgnoreCase) ||
                    header.Key.Equals(HeaderNames.ContentLanguage, StringComparison.OrdinalIgnoreCase) ||
                    header.Key.Equals(HeaderNames.ContentMD5, StringComparison.OrdinalIgnoreCase) ||
                    header.Key.Equals(HeaderNames.ContentRange, StringComparison.OrdinalIgnoreCase) ||
                    header.Key.Equals(HeaderNames.ContentLocation, StringComparison.OrdinalIgnoreCase) ||
                    header.Key.Equals(HeaderNames.ContentLength, StringComparison.OrdinalIgnoreCase))
                {
                    // Add it to the content headers
                    newRequest.Content.Headers.Add(header.Key, header.Value.AsEnumerable());
                }
                // Otherwise...
                else
                    // Add it to the request headers
                    newRequest.Headers.Add(header.Key, header.Value.AsEnumerable());
            }


            // Send the request to the microservice and get a response
            // TODO: try catch this b
            var serviceResponse = await httpClient.SendAsync(newRequest);

            // Set the status code of the response to return
            Response.StatusCode = ((int)serviceResponse.StatusCode);

            // Add the service response headers to the response
            foreach(var header in serviceResponse.Headers)
                Response.Headers.Append(header.Key, new StringValues(header.Value.ToArray()));

            // Remove transfer encoding header cuz it gets removed by httpClient.SendAsync()
            Response.Headers.Remove(HeaderNames.TransferEncoding);

            // Write the service response body to the response we want to return
            await serviceResponse.Content.CopyToAsync(Response.Body);

            // Stop the request here
            return;
        }
    }

    // If we matched no microservice, return an error
    await Response.WriteAsync($"Requested Path: [{Request.Path}] doesn't exist");
});

app.Run();
