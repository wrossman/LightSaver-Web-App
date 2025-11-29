using System.Text.Json;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
public sealed class LightroomService
{
    private readonly ILogger<LightroomService> _logger;
    private readonly GlobalStoreHelpers _store;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly LightrooomUpdateSessions _updateSessions;
    public LightroomService(ILogger<LightroomService> logger, GlobalStoreHelpers store, IServiceScopeFactory scopeFactory, LightrooomUpdateSessions updateSessions)
    {
        _logger = logger;
        _store = store;
        _scopeFactory = scopeFactory;
        _updateSessions = updateSessions;
    }
    public async Task<(List<string>, string)> GetImageUrisFromShortCodeAsync(string shortCode, int maxScreenSize)
    {
        // thanks chat for helping me translate from brs, even though you did a bad job
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
                    // /rels/rendition_type/2048
                    // /rels/rendition_type/1280
                    // /rels/rendition_type/640

                    if (!item.TryGetProperty("asset", out var asset) ||
                        !asset.TryGetProperty("links", out var links))
                    {
                        continue;
                    }

                    string[] preferredRendition =
                    {
                        "/rels/rendition_type/2048",
                        "/rels/rendition_type/1280",
                        "/rels/rendition_type/640"
                    };

                    JsonElement rendition;
                    JsonElement hrefElement = default;

                    int renditionStart = 0;
                    if (maxScreenSize <= 1280)
                    {
                        renditionStart = 1;
                    }

                    for (int i = renditionStart; i < preferredRendition.Length; i++)
                    {
                        if (links.TryGetProperty(preferredRendition[i], out rendition) &&
                        rendition.TryGetProperty("href", out hrefElement))
                        {
                            _logger.LogInformation($"Got lightrooom item from rendition {preferredRendition[i]}");
                            break;
                        }
                    }

                    if (hrefElement.ValueKind == JsonValueKind.Undefined)
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
    public async Task<bool> LightroomFlow(List<string> urls, UserSession userSession, string shortCode)
    {
        using HttpClient client = new();
        foreach (var item in urls)
        {
            var bytes = new byte[32];
            RandomNumberGenerator.Fill(bytes);
            var key = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            byte[] data = await client.GetByteArrayAsync(item);
            string hash = GlobalHelpers.ComputeHashFromBytes(data);
            hash = hash + "-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");

            ImageShare share = new()
            {
                Id = hash,
                Key = key,
                SessionCode = userSession.SessionCode,
                ImageStream = data,
                CreatedOn = DateTime.UtcNow,
                FileType = "",
                RokuId = userSession.RokuId,
                Source = "lightroom",
                OriginUrl = item,
                LightroomAlbum = shortCode
            };
            await _store.WriteResourceToStore(share, userSession.MaxScreenSize);
        }

        UserSessions.CodesReadyForTransfer.Enqueue(userSession.SessionCode);
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
    public async Task<string?> UpdateRokuLinks(ResourceRequest resourceReq, int maxScreenSize)
    {
        string albumUrl = _store.GetResourceLightroomAlbum(resourceReq);
        var result = await GetImageUrisFromShortCodeAsync(albumUrl, maxScreenSize);
        var newImgs = result.Item1;

        if (result.Item2 == "Failed to retrieve any images from album.")
        {
            await _store.RemoveByLightroomAlbum(albumUrl);
            return null;
        }

        if (result.Item2 != "success" || newImgs is null)
        {
            _logger.LogWarning("Failed to get url list from lightroom album");
            _logger.LogWarning("Failed with error: " + result.Item2);
            return null;
        }

        var oldImgs = await _store.GetLightroomOriginUrlsByRokuId(resourceReq.RokuId);
        if (oldImgs is null)
            return null;

        bool equal = !oldImgs.Except(newImgs).Any() && !newImgs.Except(oldImgs).Any();

        _logger.LogInformation("Image list equality check result: {Equal}", equal);

        if (equal)
            return null;
        else
        {
            LightroomUpdateSession session = await _updateSessions.CreateUpdateSession(resourceReq.RokuId);
            _ = Task.Run(async () =>
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var lightroom = scope.ServiceProvider.GetRequiredService<LightroomService>();
                    await lightroom.UpdateLightroomImagesAsync(oldImgs, newImgs, albumUrl, resourceReq, session);
                }
            });

            return session.Id;
        }
    }
    public async Task UpdateLightroomImagesAsync(List<string> oldImgs, List<string> newImgs, string albumUrl, ResourceRequest resourceReq, LightroomUpdateSession session)
    {
        List<string>? imgsToRemove = new();
        List<string>? imgsToKeep = new();

        foreach (var item in oldImgs)
        {
            if (!newImgs.Contains(item))
                imgsToRemove.Add(item);
            else
                imgsToKeep.Add(item);
        }

        List<string>? imgsToAdd = new();

        foreach (var item in newImgs)
        {
            if (!oldImgs.Contains(item))
                imgsToAdd.Add(item);
        }

        _logger.LogInformation($"Found {imgsToRemove.Count} to remove.");
        _logger.LogInformation($"Found {imgsToKeep.Count} to keep.");
        _logger.LogInformation($"Found {imgsToAdd.Count} to add.");

        await _store.RemoveByOriginUrls(imgsToRemove);

        Dictionary<string, string> updatePackage = await _store.GetOldImgsForUpdatePackage(imgsToKeep);

        using HttpClient client = new();
        foreach (var item in imgsToAdd)
        {
            _logger.LogInformation($"Downloading image {item} to add to updatePackage");
            var bytes = new byte[32];
            RandomNumberGenerator.Fill(bytes);
            var newKey = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
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
                RokuId = resourceReq.RokuId,
                Source = "lightroom",
                OriginUrl = item,
                LightroomAlbum = albumUrl
            };
            updatePackage.Add(share.Id, share.Key);
            await _store.WriteResourceToStore(share, resourceReq.MaxScreenSize);
        }
        session.Links = updatePackage;
        session.ReadyForTransfer = true;
        _updateSessions.SetReadyForTransfer(session);
    }
}
