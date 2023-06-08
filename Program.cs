using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using System.Buffers;
using System.Net;

var builder = WebApplication.CreateBuilder(args);


var corsPolicy = "someCorsPolicy";
builder.Services.AddCors((options) =>
    { options.AddPolicy(corsPolicy, policy => { policy.WithOrigins("*").WithHeaders("*").WithMethods("*"); }); });

var app = builder.Build();
app.UseCors(corsPolicy);


var handler = new HttpClientHandler() { AllowAutoRedirect = false };
var httpClient = new HttpClient(handler);

// Create the list of microservices
var services = new List<Service>()
{
};

var host = "localhost";

// If we are not in production environment
if(app.Environment.IsDevelopment())
{
    // Add dev services
    services.AddRange(new[]{
        new Service("/payment", new() { $"http://{host}:5555" }),
        new Service("/storage", new() { $"http://{host}:6000" }),
        new Service("/auth", new() { $"http://{host}:7000" }),
        new Service("/search", new() { $"http://{host}:8000" }),
        new Service("/appartement", new() { $"http://{host}:8800" }),
        new Service("/ms-reservation", new() { $"http://{host}:9000" }),
    });
}
// Otherwise...
else
{
    // Add production services
    services.AddRange(new[]{
        new Service("/auth", new() { Environment.GetEnvironmentVariable("AuthApp")! }),
        new Service("/storage", new() { Environment.GetEnvironmentVariable("StorageApp")! }),
        new Service("/ms-reservation", new() { Environment.GetEnvironmentVariable("ReservationApp")! }),
        new Service("/appartement", new() { Environment.GetEnvironmentVariable("AppartementApp")! }),
        new Service("/payment", new() { Environment.GetEnvironmentVariable("PaymentApp")! }),
    });
}

// TODO: Add authorization before sending any request
// For all routes that hit the app
app.MapFallback(async (HttpRequest Request, HttpResponse Response) =>
{
    // For each microservice we registered in our services
    foreach(var service in services)
    {
        // If the request path starts with the microservice path
        if(Request.Path.Value is not null &&
            Request.Path.Value.StartsWith(service.Prefix, StringComparison.OrdinalIgnoreCase))
        {
            // Create a new http request
            var newRequest = new HttpRequestMessage();

            // Set the method of the request
            newRequest.Method = new HttpMethod(Request.Method);

            // Create the request uri
            var requestUri = service.GetUrl() + Request.Path.Value.Substring(service.Prefix.Length);

            // If there are any query parameters
            if(Request.Query.Any())
                // Add them to the request uri
                requestUri = QueryHelpers.AddQueryString(requestUri, Request.Query);

            // Set the uri of the request
            newRequest.RequestUri = new Uri(requestUri);

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


            try
            {
                // Send the request to the microservice and get a response
                var serviceResponse = await httpClient.SendAsync(newRequest);

                // Set the status code of the response to return
                Response.StatusCode = ((int)serviceResponse.StatusCode);

                // Add the service response headers to the response
                foreach(var header in serviceResponse.Headers)
                    Response.Headers.Append(header.Key, new StringValues(header.Value.ToArray()));

                // Add content headers to the response
                foreach(var header in serviceResponse.Content.Headers)
                    Response.Headers.Append(header.Key, new StringValues(header.Value.ToArray()));

                // Remove transfer encoding header cuz it gets removed by httpClient.SendAsync()
                Response.Headers.Remove(HeaderNames.TransferEncoding);

                // If the request is not a redirect
                if(((int)serviceResponse.StatusCode / 100) != 3)

                    // Write the service response body to the response we want to return
                    await serviceResponse.Content.CopyToAsync(Response.Body);

                // Stop the request here
                return;
            }
            // If there was an error sending the request
            catch(HttpRequestException)
            {
                // Return an error, indicating the service is not available
                Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
                return;
            }
        }
    }

    // If we matched no microservice, return an error
    await Response.WriteAsync($"Requested Path: [{Request.Path}] doesn't exist");
});

app.Run();
