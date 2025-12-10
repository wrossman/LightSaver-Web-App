using System.Text.Json;
using System.Security.Cryptography;
public sealed class LightroomService
{
    private readonly ILogger<LightroomService> _logger;
    private readonly GlobalStore _store;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly LightroomUpdateSessions _updateSessions;
    private readonly IResourceSave _resourceSave;
    private readonly HmacHelper _hmacService;
    private readonly IConfiguration _config;
    private readonly GlobalImageStoreDbContext _resourceDb;
    public LightroomService(IResourceSave resourceSave, ILogger<LightroomService> logger, GlobalImageStoreDbContext resourceDb, HmacHelper hmacService, IConfiguration config, GlobalStore store, IServiceScopeFactory scopeFactory, LightroomUpdateSessions updateSessions)
    {
        _logger = logger;
        _store = store;
        _resourceSave = resourceSave;
        _scopeFactory = scopeFactory;
        _updateSessions = updateSessions;
        _config = config;
        _resourceDb = resourceDb;
        _hmacService = hmacService;
    }
    public async Task<(Dictionary<string, string?>?, string)> GetImageUrisFromShortCodeAsync(string shortCode, int maxScreenSize)
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
            return (null, "Failed to send request.");
        }

        var location = response.Headers.Location?.ToString();

        if (string.IsNullOrWhiteSpace(location))
            return (new Dictionary<string, string?>(), "No Location header found on redirect.");

        if (string.Equals(location, "http://www.adobe.com", StringComparison.OrdinalIgnoreCase))
            return (null, "Location was www.adobe.com (invalid album).");

        _logger.LogInformation($"Found long url for Lightroom album: {location}");

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
            return (null, "Failed to get album HTML.");
        }

        string? json = ExtractAlbumAttributesJson(lightroomAlbumHtml);
        if (json is null)
            return (null, "Failed to parse albumAttributes");

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse albumAttributes JSON: {Json}", json);
            return (null, "Album attributes were not valid JSON.");
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
                return (null, "Failed to get asset URLs from Lightroom album.");

            int idx = selfHref.IndexOf("/albums", StringComparison.OrdinalIgnoreCase);
            if (idx <= 0)
                return (null, "Failed to get space URL from album.");

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
                return (null, "Failed to get album data.");
            }

            if (string.IsNullOrWhiteSpace(albumResponse))
                return (null, "Album data response was empty.");

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
                return (null, "Failed to parse album data.");
            }

            using (final)
            {
                JsonElement finalJson = final.RootElement;

                if (!finalJson.TryGetProperty("resources", out JsonElement resources) ||
                    resources.ValueKind != JsonValueKind.Array)
                {
                    return (null, "Album data does not contain a resources array.");
                }

                Dictionary<string, string?> outputArr = new();

                foreach (JsonElement item in resources.EnumerateArray())
                {
                    // /rels/rendition_type/2048
                    // /rels/rendition_type/1280
                    // /rels/rendition_type/640

                    if (outputArr.Count >= _config.GetValue<int>("MaxImages"))
                    {
                        _logger.LogInformation($"User tried to load more than max images from lightroom.");
                        return (outputArr, "overflow");
                    }

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

                    outputArr.Add(itemUrl, null);
                }

                if (outputArr.Count == 0)
                    return (outputArr, "Failed to retrieve any images from album.");

                return (outputArr, "success");
            }
        }
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
    public async Task<(Guid, string)?> UpdateRokuLinks(ResourceRequest resourceReq)
    {
        string albumUrl = _store.GetResourceLightroomAlbum(resourceReq);
        var result = await GetImageUrisFromShortCodeAsync(albumUrl, resourceReq.MaxScreenSize);
        var newImgs = result.Item1;

        if (result.Item2 == "Failed to retrieve any images from album.")
        {
            await _store.RemoveByLightroomAlbum(albumUrl);
            return null;
        }

        if (result.Item2 == "overflow")
        {
            throw new InvalidOperationException();
        }

        if (newImgs is null)
        {
            _logger.LogWarning("Failed to get url list from lightroom album");
            _logger.LogWarning("Failed with error: " + result.Item2);
            return null;
        }

        // create a dictionary that pairs a key of the hashed url, with the value as the url

        var newImgsDic = new Dictionary<string, string>();
        foreach (var item in newImgs)
        {
            newImgsDic.Add(GlobalHelpers.ComputeHashFromString(item.Key), item.Key);
        }

        var oldImgs = await _store.GetLightroomOriginByRokuId(resourceReq.RokuId);
        if (oldImgs is null)
            return null;

        bool equal = !oldImgs.Except(newImgsDic.Keys.ToList()).Any() && !newImgsDic.Keys.ToList().Except(oldImgs).Any();

        _logger.LogInformation($"Image list equality check result: {equal}");

        if (equal)
            return null;
        else
        {
            var sessionResult = _updateSessions.CreateUpdateSession(resourceReq.RokuId);
            _ = Task.Run(async () =>
            {
                try
                {
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var lightroom = scope.ServiceProvider.GetRequiredService<LightroomService>();
                        await lightroom.UpdateLightroomImagesAsync(oldImgs, newImgsDic, albumUrl, resourceReq, sessionResult.Item1);
                    }
                }
                catch (Exception e)
                {
                    _logger.LogInformation($"Fire and forget failed with error {e.Message}");
                }

            });

            return (sessionResult.Item1, sessionResult.Item2);
        }
    }
    public async Task UpdateLightroomImagesAsync(List<string> oldImgs, Dictionary<string, string> newImgs, string albumUrl, ResourceRequest resourceReq, Guid sessionId)
    {
        List<string>? imgsToRemove = new();
        List<string>? imgsToKeep = new();

        foreach (var item in oldImgs)
        {
            if (!newImgs.ContainsKey(item))
                imgsToRemove.Add(item);
            else
                imgsToKeep.Add(newImgs[item]);
        }

        List<string>? imgsToAdd = new();

        foreach (var item in newImgs)
        {
            if (!oldImgs.Contains(item.Key))
                imgsToAdd.Add(item.Value);
        }

        _logger.LogInformation($"Found {imgsToRemove.Count} to remove.");
        _logger.LogInformation($"Found {imgsToKeep.Count} to keep.");
        _logger.LogInformation($"Found {imgsToAdd.Count} to add.");

        await _store.RemoveByOrigin(imgsToRemove);

        Dictionary<Guid, string> updatePackage = await _store.GetOldImgsForUpdatePackage(imgsToKeep);

        List<ImageShare> sharesToAdd = new();

        using HttpClient client = new();
        foreach (var item in imgsToAdd)
        {
            if (updatePackage.Count >= _config.GetValue<int>("MaxImages"))
            {
                _logger.LogInformation($"User tried to load more than max images from lightroom.");
                break;
            }

            var bytes = new byte[32];
            RandomNumberGenerator.Fill(bytes);
            var newKey = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            var keyDerivation = _hmacService.Hash(newKey);

            byte[] data = await client.GetByteArrayAsync(item);


            Guid shareId = Guid.NewGuid();
            ImageShare share = new()
            {
                Id = shareId,
                Key = keyDerivation,
                KeyCreated = DateTime.UtcNow,
                SessionCode = "",
                ImageUri = await _resourceSave.SaveResource(shareId, data, null, resourceReq.MaxScreenSize),
                CreatedOn = DateTime.UtcNow,
                FileType = "",
                RokuId = resourceReq.RokuId,
                Source = ImageShareSource.Lightroom,
                Origin = GlobalHelpers.ComputeHashFromString(item),
                LightroomAlbum = albumUrl
            };
            sharesToAdd.Add(share);
            updatePackage.Add(share.Id, newKey);
        }
        await _resourceDb.AddRangeAsync(sharesToAdd);
        await _resourceDb.SaveChangesAsync();
        _updateSessions.WriteLinksToSession(updatePackage, sessionId);
        _updateSessions.SetReadyToTransfer(sessionId);
    }
}
