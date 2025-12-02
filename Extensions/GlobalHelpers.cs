using System.Security.Cryptography;
using System.Text;
using HtmlAgilityPack;
public class GlobalHelpers
{
    public static IResult CreateErrorPage(string message, string action = "")
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(File.ReadAllText("./wwwroot/Error.html"));

        var erroNode = doc.GetElementbyId("ErrorMessage");
        var actionNode = doc.GetElementbyId("Action");
        if (erroNode is not null && actionNode is not null)
        {
            erroNode.InnerHtml = message;
            actionNode.InnerHtml = action;
        }
        string errorPage = doc.DocumentNode.OuterHtml;
        return Results.Content(errorPage, "text/html");
    }
    public static string CreateLightroomOverflowPage(string message, string action = "")
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(File.ReadAllText("./wwwroot/Error.html"));

        var erroNode = doc.GetElementbyId("ErrorMessage");
        var actionNode = doc.GetElementbyId("Action");
        if (erroNode is not null && actionNode is not null)
        {
            erroNode.InnerHtml = message;
            actionNode.InnerHtml = action;
        }
        return doc.DocumentNode.OuterHtml;
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
    public static string BuildGoogleOAuthUrl(IConfiguration config)
    {
        string clientId = config["OAuth:ClientId"] ?? string.Empty;
        string redirect = config["OAuth:RedirectUri"] ?? string.Empty;
        string responseType = config["OAuth:ResponseType"] ?? string.Empty;
        string scope = config["OAuth:PickerScope"] ?? string.Empty;
        string googleAuthServer = config["OAuth:GoogleAuthServer"] ?? string.Empty;
        string googleQuery = $"{googleAuthServer}?scope={scope}&response_type={responseType}&redirect_uri={redirect}&client_id={clientId}";
        return googleQuery;
    }
}