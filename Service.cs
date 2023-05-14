
/// <summary>
/// Contains information about a certain microservice 
/// </summary>
/// <param name="Prefix">The prefix of the path used to access the microservice</param>
/// <param name="ServiceURLs">The URLs that point to instances of the specified microservice</param>
public record Service(string Prefix, List<string> ServiceURLs)
{
    /// <summary>
    /// Returns a URL to use through a specific algorithm
    /// </summary>
    /// <returns></returns>
    public string GetUrl() => ServiceURLs.First();
}
