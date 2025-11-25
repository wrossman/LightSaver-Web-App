using System.Text.Json;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
public sealed class LightroomService
{
    private readonly ILogger<LightroomService> _logger;
    private readonly UserSessionDbContext _userSessionDb;
    private readonly RokuSessionDbContext _rokuSessionDb;
    private readonly GlobalImageStoreDbContext _resourceDb;
    private readonly GlobalStoreHelpers _store;

    public LightroomService(UserSessionDbContext userSessionDb, GlobalImageStoreDbContext resourceDb, RokuSessionDbContext rokuSessionDb, ILogger<LightroomService> logger, GlobalStoreHelpers store)
    {
        _userSessionDb = userSessionDb;
        _resourceDb = resourceDb;
        _rokuSessionDb = rokuSessionDb;
        _logger = logger;
        _store = store;
    }
    public async Task<(List<string>, string)> GetImageUrisFromShortCodeAsync(string shortCode)
    {
        const string lightroomShortPrefix = "https://adobe.ly/";
        var url = lightroomShortPrefix + shortCode;

        using var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false // we only want the first redirect here
        };

        using var client = new HttpClient(handler);

        HttpResponseMessage response;
        try
        {
            response = await client.GetAsync(url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send request to {Url}", url);
            return (new List<string>(), "Failed to send request.");
        }

        var location = response.Headers.Location?.ToString();

        if (string.IsNullOrWhiteSpace(location))
            return (new List<string>(), "No Location header found on redirect.");

        if (string.Equals(location, "http://www.adobe.com", StringComparison.OrdinalIgnoreCase))
            return (new List<string>(), "Location was www.adobe.com (invalid album).");

        _logger.LogInformation("Found long url for Lightroom album: {Location}", location);

        // Now we actually want redirects to follow, so use a normal client
        using var htmlClient = new HttpClient();

        string lightroomAlbumHtml;
        try
        {
            lightroomAlbumHtml = await htmlClient.GetStringAsync(location);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get album HTML from {Location}", location);
            return (new List<string>(), "Failed to get album HTML.");
        }

        string? json = ExtractAlbumAttributesJson(lightroomAlbumHtml);
        if (json is null)
            return (new List<string> { }, "Failed to parse albumAttributes");

        // If this is not strictly JSON (e.g. single quotes, unquoted keys, trailing commas),
        // System.Text.Json will throw. You may need to normalize it first or use a more lenient parser.
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse albumAttributes JSON: {Json}", json);
            return (new List<string>(), "Album attributes were not valid JSON.");
        }

        using (doc)
        {
            var root = doc.RootElement;

            string? selfHref =
                root.GetProperty("links")
                    .GetProperty("self")
                    .GetProperty("href")
                    .GetString();

            if (selfHref is null)
                return (new List<string>(), "Failed to get asset URLs from Lightroom album.");

            int idx = selfHref.IndexOf("/albums", StringComparison.OrdinalIgnoreCase);
            if (idx <= 0)
                return (new List<string>(), "Failed to get space URL from album.");

            string spaceLink = selfHref.Substring(0, idx);

            // selfHref is typically something like: /v2/spaces/{spaceId}/albums/{albumId}
            // The API root is https://photos.adobe.io (without /v2 here).
            const string apiRoot = "https://photos.adobe.io/v2/";

            string endpointUrlEnd = "/assets?embed=asset&subtype=image%3Bvideo";

            // Build album assets endpoint: https://photos.adobe.io + selfHref + endpoint
            string albumUrl = apiRoot + selfHref + endpointUrlEnd;

            string albumResponse;
            try
            {
                albumResponse = await htmlClient.GetStringAsync(albumUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get album data from {AlbumUrl}", albumUrl);
                return (new List<string>(), "Failed to get album data.");
            }

            if (string.IsNullOrWhiteSpace(albumResponse))
                return (new List<string>(), "Album data response was empty.");

            string raw = albumResponse;

            // 1. Skip leading whitespace
            raw = raw.TrimStart();

            // 2. If it starts with "while (1) {}", skip that
            const string sentinel = "while (1) {}";
            if (raw.StartsWith(sentinel, StringComparison.Ordinal))
            {
                raw = raw.Substring(sentinel.Length);
                raw = raw.TrimStart(); // remove the newline/space after it
            }

            // 3. Just to be safe, start exactly at the first '{'
            int firstBrace = raw.IndexOf('{');
            if (firstBrace < 0)
                throw new InvalidOperationException("No JSON object found in response.");

            string albumAttributesJson = raw.Substring(firstBrace);

            JsonDocument final;
            try
            {
                final = JsonDocument.Parse(albumAttributesJson);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse album data JSON: {Result}", albumAttributesJson);
                return (new List<string>(), "Failed to parse album data.");
            }

            using (final)
            {
                JsonElement finalJson = final.RootElement;

                if (!finalJson.TryGetProperty("resources", out JsonElement resources) ||
                    resources.ValueKind != JsonValueKind.Array)
                {
                    return (new List<string>(), "Album data does not contain a resources array.");
                }

                List<string> outputArr = new();

                foreach (JsonElement item in resources.EnumerateArray())
                {
                    // item.asset.links["/rels/rendition_type/1280"].href
                    if (!item.TryGetProperty("asset", out var asset) ||
                        !asset.TryGetProperty("links", out var links) ||
                        !links.TryGetProperty("/rels/rendition_type/1280", out var rendition) ||
                        !rendition.TryGetProperty("href", out var hrefElement))
                    {
                        continue;
                    }

                    var href = hrefElement.GetString();
                    if (string.IsNullOrWhiteSpace(href))
                        continue;

                    // href is usually already a full API path like /v2/assets/...
                    // Just prepend https://photos.adobe.io
                    string itemUrl = apiRoot + spaceLink + "/" + href;

                    outputArr.Add(itemUrl);
                }

                if (outputArr.Count == 0)
                    return (outputArr, "Failed to retrieve any images from album.");

                return (outputArr, "success");
            }
        }
    }
    public async Task<bool> LightroomFlow(List<string> urls, string userSessionId, string shortCode)
    {
        var sessionCode = await _userSessionDb.Sessions
                    .Where(s => s.Id == userSessionId)
                    .Select(s => s.SessionCode)
                    .FirstOrDefaultAsync();
        if (sessionCode is null)
        {
            _logger.LogWarning("Failed to locate user session with sessionId " + userSessionId);
            return false;
        }

        var rokuId = await _rokuSessionDb.Sessions
                .Where(s => s.SessionCode == sessionCode)
                .Select(s => s.RokuId)
                .FirstOrDefaultAsync();
        if (rokuId is null)
        {
            _logger.LogWarning("Failed to locate roku session with session code " + sessionCode);
            return false;
        }


        using HttpClient client = new();
        foreach (var item in urls)
        {
            var bytes = new byte[32];
            RandomNumberGenerator.Fill(bytes);
            var key = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            _logger.LogInformation("Trying to get image from: " + item);
            byte[] data = await client.GetByteArrayAsync(item);
            string hash = GlobalHelpers.ComputeHashFromBytes(data);
            hash = hash + "-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");

            ImageShare share = new()
            {
                Id = hash,
                Key = key,
                SessionCode = sessionCode,
                ImageStream = data,
                CreatedOn = DateTime.UtcNow,
                FileType = "",
                RokuId = rokuId,
                Source = "lightroom",
                OriginUrl = item,
                LightroomAlbum = shortCode
            };
            _resourceDb.Resources.Add(share);
            await _resourceDb.SaveChangesAsync();
        }

        UserSessions.CodesReadyForTransfer.Enqueue(sessionCode);
        return true;
    }
    private static string? ExtractAlbumAttributesJson(string html)
    {
        const string marker = "albumAttributes";

        // 1. Find "albumAttributes"
        int markerIndex = html.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
            return null;

        // 2. Find the first '{' after "albumAttributes"
        int braceStart = html.IndexOf('{', markerIndex);
        if (braceStart < 0)
            return null;

        int depth = 0;
        bool inString = false;
        bool escape = false;

        // 3. Walk forward and balance braces until the matching '}'
        for (int i = braceStart; i < html.Length; i++)
        {
            char c = html[i];

            if (escape)
            {
                escape = false;
                continue;
            }

            if (c == '\\')
            {
                escape = true;
                continue;
            }

            if (c == '"')
            {
                inString = !inString;
                continue;
            }

            if (inString)
                continue;

            if (c == '{')
            {
                depth++;
            }
            else if (c == '}')
            {
                depth--;
                if (depth == 0)
                {
                    // i is the index of the matching closing brace
                    return html.Substring(braceStart, i - braceStart + 1);
                }
            }
        }

        // Never found matching closing brace
        return null;
    }
    public async Task<Dictionary<string, string>?> UpdateRokuLinks(string location, string key, string device)
    {
        string albumUrl = _store.GetResourceLightroomAlbum(location, key, device);
        var result = await GetImageUrisFromShortCodeAsync(albumUrl);
        var newImgs = result.Item1;

        if (result.Item2 == "Failed to retrieve any images from album.")
        {
            var items = await _resourceDb.Resources
            .Where(x => x.LightroomAlbum == albumUrl)
            .ToListAsync();

            _resourceDb.Resources.RemoveRange(items);
            await _resourceDb.SaveChangesAsync();
            return null;
        }

        if (result.Item2 != "success" || newImgs is null)
        {
            _logger.LogWarning("Failed to get url list from lightroom album");
            _logger.LogWarning("Failed with error: " + result.Item2);
            return null;
        }

        // thanks chat for the query and comparison snippet
        var oldImgs = await _resourceDb.Resources
            .Where(x => x.RokuId == device &&
                        x.Source == "lightroom")
            .Select(x => x.OriginUrl)
            .ToListAsync();

        bool equal = !oldImgs.Except(newImgs).Any() && !newImgs.Except(oldImgs).Any();

        //thanks chat for cleaning up my janky writelines
        _logger.LogInformation($"Old images: {string.Join("\n", oldImgs)}");
        _logger.LogInformation($"New images: {string.Join("\n", newImgs)}");

        _logger.LogInformation("Image list equality check result: {Equal}", equal);

        if (equal)
            return null;

        var itemsToRemove = await _resourceDb.Resources
            .Where(x => oldImgs.Contains(x.OriginUrl))
            .ToListAsync();

        _resourceDb.Resources.RemoveRange(itemsToRemove);
        await _resourceDb.SaveChangesAsync();

        _logger.LogInformation(
            "Removed the following URLs from resource database: {RemovedUrls}",
            string.Join("\n", itemsToRemove.Select(i => i.OriginUrl))
        );


        Dictionary<string, string> newPackage = new();

        using HttpClient client = new();
        foreach (var item in newImgs)
        {
            var bytes = new byte[32];
            RandomNumberGenerator.Fill(bytes);
            var newKey = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            _logger.LogInformation("Trying to get image from: " + item);
            byte[] data = await client.GetByteArrayAsync(item);
            string hash = GlobalHelpers.ComputeHashFromBytes(data);
            hash = hash + "-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");

            ImageShare share = new()
            {
                Id = hash,
                Key = newKey,
                SessionCode = "",
                ImageStream = data,
                CreatedOn = DateTime.UtcNow,
                FileType = "",
                RokuId = device,
                Source = "lightroom",
                OriginUrl = item,
                LightroomAlbum = albumUrl
            };
            newPackage.Add(share.Id, share.Key);
            _resourceDb.Resources.Add(share);
            await _resourceDb.SaveChangesAsync();
        }

        return newPackage;

    }
}
