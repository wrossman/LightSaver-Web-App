using System.Security.Cryptography;
using System.Text;
using HtmlAgilityPack;
public class GlobalHelpers
{
    public static IResult CreateErrorPage(string message, string action = "")
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(File.ReadAllText("./wwwroot/Error.html"));

        var errorNode = doc.GetElementbyId("ErrorMessage");
        var actionNode = doc.GetElementbyId("Action");
        if (errorNode is not null && actionNode is not null)
        {
            errorNode.InnerHtml = message;
            actionNode.InnerHtml = action;
        }
        string errorPage = doc.DocumentNode.OuterHtml;
        return Results.Content(errorPage, "text/html");
    }
    public static IResult CreateLightroomOverflowPage(string message, int maxFiles, string action = "")
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(File.ReadAllText("./wwwroot/Error.html"));

        var errorNode = doc.GetElementbyId("ErrorMessage");
        var actionNode = doc.GetElementbyId("Action");
        if (errorNode is not null && actionNode is not null)
        {
            errorNode.InnerHtml = message;
            actionNode.InnerHtml = action;
        }
        string html = doc.DocumentNode.OuterHtml;
        html = html.Replace("MAXFILES", maxFiles.ToString());
        return Results.Content(html, "text/html");
    }
    public static string ComputeHashFromString(string data)
    {
        //thanks copilot
        using var sha256 = SHA256.Create();
        byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(data));

        return Convert.ToHexString(hashBytes);
    }
    public static bool VerifyImageUpload(List<IFormFile> images, int maxFiles)
    {
        long maxFileSize = 10L * 1000 * 1000; // 5 MB

        if (images == null || images.Count == 0)
            return false;

        if (images.Count > maxFiles)
            return false;

        foreach (var file in images)
        {
            if (file.Length <= 0)
                return false;

            if (file.Length > maxFileSize)
                return false;
        }

        return true;
    }
    public static string BuildGoogleOAuthUrl(IConfiguration config, string state)
    {
        string clientId = config["OAuth:ClientId"] ?? string.Empty;
        string redirect = config["OAuth:RedirectUri"] ?? string.Empty;
        string responseType = config["OAuth:ResponseType"] ?? string.Empty;
        string scope = config["OAuth:PickerScope"] ?? string.Empty;
        string googleAuthServer = config["OAuth:GoogleAuthServer"] ?? string.Empty;
        string googleQuery = $"{googleAuthServer}?scope={scope}&response_type={responseType}&state={state}&redirect_uri={redirect}&client_id={clientId}";
        return googleQuery;
    }
    public static async Task<string> ReadRokuPost(HttpContext context)
    {
        const int maxBytes = 512;
        var buffer = new byte[32];
        int totalBytes = 0;

        using var memoryStream = new MemoryStream();
        int bytesRead;

        do
        {
            bytesRead = await context.Request.Body.ReadAsync(buffer, 0, buffer.Length);
            totalBytes += bytesRead;

            if (totalBytes > maxBytes)
                throw new ArgumentException();

            await memoryStream.WriteAsync(buffer, 0, bytesRead);
        }
        while (bytesRead > 0);

        return Encoding.UTF8.GetString(memoryStream.ToArray());
    }
}