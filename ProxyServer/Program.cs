using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.Run(async context =>
{
    // Construct the target URL based on the incoming request
    var targetUri = new Uri($"https://www.reddit.com{context.Request.Path}{context.Request.QueryString}");

    // Initialize HttpClient with default headers
    using var httpClient = new HttpClient();
    httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");

    try
    {
        // Fetch content from the target URL
        var response = await httpClient.GetByteArrayAsync(targetUri);

        // Read the content as string
        var content = Encoding.UTF8.GetString(response);

        // Modify content (add "™" to six-letter words)
        var modifiedContent = ModifyContent(content);

        // Rewrite links to point back to the proxy server
        modifiedContent = RewriteLinks(modifiedContent, context.Request);

        var modifiedBytes = Encoding.UTF8.GetBytes(modifiedContent);

        // Respond with the modified content
        context.Response.StatusCode = 200;
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.Body.WriteAsync(modifiedBytes, 0, modifiedBytes.Length);
    }
    catch (HttpRequestException ex)
    {
        // Handle HTTP request errors
        context.Response.StatusCode = 500;
        await context.Response.WriteAsync($"Error fetching Reddit content: {ex.Message}");
    }
});

static string ModifyContent(string content)
{
    var doc = new HtmlDocument();
    doc.LoadHtml(content);

    ModifyTextNodes(doc.DocumentNode);

    using (var sw = new StringWriter())
    {
        doc.Save(sw);
        return sw.ToString();
    }
}

static  void ModifyTextNodes(HtmlNode node)
{
    if (node.NodeType == HtmlNodeType.Text)
    {
        var words = node.InnerText.Split(' ');
        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Length == 6)
            {
                words[i] += "&trade;";
            }
        }
        node.InnerHtml = string.Join(' ', words);
    }
    foreach (var child in node.ChildNodes)
    {
        ModifyTextNodes(child);
    }
}

static string RewriteLinks(string content, HttpRequest request)
{
    var proxyBaseUri = $"{request.Scheme}://{request.Host}/";
    return Regex.Replace(content, @"https?://(?:www\.)?reddit\.com", proxyBaseUri);
}

app.Run();
