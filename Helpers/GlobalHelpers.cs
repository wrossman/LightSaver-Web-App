using System.Security.Cryptography;
using System.Text;
using HtmlAgilityPack;
public class GlobalHelpers
{
    public static string CreateErrorPage(string message, string action = "")
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
        return errorPage;
    }
    public static string ComputeHashFromBytes(byte[] data)
    {
        //thanks copilot
        using var sha256 = SHA256.Create();
        byte[] hashBytes = sha256.ComputeHash(data);

        // Convert to hex string
        var builder = new StringBuilder();
        foreach (var b in hashBytes)
            builder.Append(b.ToString("x2"));

        return builder.ToString();
    }

}