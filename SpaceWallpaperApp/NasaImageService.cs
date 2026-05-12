using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace SpaceWallpaperApp;

public sealed class NasaImageService
{
    private static readonly HttpClient HttpClient = new()
    {
        BaseAddress = new Uri("https://images-api.nasa.gov/")
    };

    private static readonly string[] AstronomyCenters =
    {
        "GSFC",
        "JPL",
        "STSCI",
        "CHANDRA",
        "ESA"
    };

    private static readonly string[] SpaceQueries =
    {
        "nebula",
        "galaxy",
        "star cluster",
        "supernova",
        "planetary nebula",
        "spiral galaxy",
        "elliptical galaxy",
        "NGC",
        "Messier",
        "globular cluster",
        "interstellar",
        "cosmic",
        "constellation",
        "stellar",
        "deep space",
        "universe",
        "astronomical",
        "celestial",
        "quasar",
        "pulsar",
        "black hole",
        "star formation",
        "emission nebula",
        "reflection nebula",
        "dark nebula",
        "Saturn rings",
        "Jupiter atmosphere",
        "Mars surface",
        "Moon surface",
        "lunar crater",
        "planetary",
        "asteroid",
        "comet"
    };

    public async Task<NasaImageCandidate> GetHighResolutionImageAsync(string downloadDirectory, CancellationToken cancellationToken)
    {
        Debug.WriteLine("[NASA] Starting random image search");
        Directory.CreateDirectory(downloadDirectory);

        var result = await TryQueryAsync(downloadDirectory, cancellationToken);
        if (result is not null)
        {
            Debug.WriteLine($"[NASA] ✓ Found image: {result.Title} ({result.Width}x{result.Height})");
            return result;
        }

        throw new InvalidOperationException("NASA did not return a suitable image. Please try again.");
    }

    private async Task<NasaImageCandidate?> TryQueryAsync(string downloadDirectory, CancellationToken cancellationToken)
    {
        var centerFilter = string.Join(",", AstronomyCenters);
        var randomQuery = SpaceQueries[Random.Shared.Next(SpaceQueries.Length)];
        var apiUrl = $"search?q={Uri.EscapeDataString(randomQuery)}&media_type=image&page_size=100&center={Uri.EscapeDataString(centerFilter)}";
        Debug.WriteLine($"[NASA] API call with query '{randomQuery}': {apiUrl}");

        var response = await HttpClient.GetFromJsonAsync<SearchResponse>(apiUrl, cancellationToken);

        var items = response?.Collection?.Items ?? [];
        Debug.WriteLine($"[NASA] Received {items.Count} results from API");

        var candidatesChecked = 0;
        const int MaxCandidatesToCheck = 50;

        foreach (var item in items.OrderBy(_ => Guid.NewGuid()))
        {
            if (candidatesChecked >= MaxCandidatesToCheck)
            {
                break;
            }

            var data = item.Data?.FirstOrDefault();
            if (data is null)
            {
                continue;
            }

            candidatesChecked++;
            Debug.WriteLine($"[NASA] Checking candidate #{candidatesChecked}: {data.Title} (ID: {data.NasaId})");

            var manifest = await HttpClient.GetFromJsonAsync<AssetResponse>($"asset/{Uri.EscapeDataString(data.NasaId)}", cancellationToken);
            var assetUrls = manifest?.Collection?.Items?.Select(x => x.Href).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray() ?? [];

            var originalUrl = assetUrls
                .Where(IsDownloadableImage)
                .OrderByDescending(GetPriority)
                .FirstOrDefault();

            if (originalUrl is null)
            {
                Debug.WriteLine($"[NASA] No downloadable image URL found for {data.NasaId}");
                continue;
            }

            var fileName = Path.GetFileName(new Uri(originalUrl).LocalPath);
            var localPath = Path.Combine(downloadDirectory, fileName);
            Debug.WriteLine($"[NASA] Target file: {fileName}");

            if (!File.Exists(localPath))
            {
                Debug.WriteLine($"[NASA] File not cached, downloading...");
                var previewUrl = assetUrls
                    .Where(IsDownloadableImage)
                    .Where(url => url.Contains("large", StringComparison.OrdinalIgnoreCase) ||
                                  url.Contains("medium", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(GetPriority)
                    .FirstOrDefault();

                var urlToCheck = previewUrl ?? originalUrl;
                Debug.WriteLine($"[NASA] Downloading {(previewUrl != null ? "preview" : "original")} to check dimensions...");
                var tempFileName = Path.GetFileName(new Uri(urlToCheck).LocalPath);
                var tempPath = Path.Combine(downloadDirectory, $"temp_{tempFileName}");

                try
                {
                    await using (var remoteStream = await HttpClient.GetStreamAsync(urlToCheck, cancellationToken))
                    await using (var localStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await remoteStream.CopyToAsync(localStream, cancellationToken);
                    }

                    // Validate it's actually an image by trying to load it
                    int width, height;
                    try
                    {
                        using var testImage = Image.FromFile(tempPath);
                        width = testImage.Width;
                        height = testImage.Height;
                        Debug.WriteLine($"[NASA] Image dimensions: {width}x{height}");
                    }
                    catch (Exception imgEx)
                    {
                        Debug.WriteLine($"[NASA] Downloaded file is not a valid image: {imgEx.Message}");
                        File.Delete(tempPath);
                        continue;
                    }

                    if (previewUrl is not null && urlToCheck == previewUrl)
                    {
                        Debug.WriteLine($"[NASA] Downloading full resolution original...");
                        File.Delete(tempPath);

                        await using (var origStream = await HttpClient.GetStreamAsync(originalUrl, cancellationToken))
                        await using (var origLocalStream = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            await origStream.CopyToAsync(origLocalStream, cancellationToken);
                        }

                        // Validate the final downloaded image
                        try
                        {
                            using var finalTest = Image.FromFile(localPath);
                            _ = finalTest.Width; // Just access to ensure it loads
                        }
                        catch
                        {
                            Debug.WriteLine($"[NASA] Final downloaded image is not valid");
                            File.Delete(localPath);
                            continue;
                        }
                    }
                    else
                    {
                        File.Move(tempPath, localPath, overwrite: true);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[NASA] Error downloading/checking image: {ex.Message}");
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                    continue;
                }
            }

            Debug.WriteLine($"[NASA] Using cached file: {fileName}");

            // Validate cached file is still a valid image
            try
            {
                using var image = Image.FromFile(localPath);
                var finalWidth = image.Width;
                var finalHeight = image.Height;

                return new NasaImageCandidate(
                    data.Title,
                    data.Description,
                    finalWidth,
                    finalHeight,
                    localPath,
                    $"https://images.nasa.gov/details-{data.NasaId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NASA] Cached file is corrupt or invalid: {ex.Message}");
                // Delete corrupt cached file and try next candidate
                try { File.Delete(localPath); } catch { }
                continue;
            }
        }

        return null;
    }

    private static bool IsDownloadableImage(string url)
    {
        return url.EndsWith("~orig.jpg", StringComparison.OrdinalIgnoreCase)
            || url.EndsWith("~orig.png", StringComparison.OrdinalIgnoreCase)
            || url.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
            || url.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            || url.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase);
    }

    private static int GetPriority(string url)
    {
        if (url.Contains("~orig", StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        if (url.Contains("large", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        if (url.Contains("medium", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return 0;
    }

    private sealed class SearchResponse
    {
        [JsonPropertyName("collection")]
        public SearchCollection? Collection { get; init; }
    }

    private sealed class SearchCollection
    {
        [JsonPropertyName("items")]
        public List<SearchItem>? Items { get; init; }
    }

    private sealed class SearchItem
    {
        [JsonPropertyName("data")]
        public List<SearchData>? Data { get; init; }
    }

    private sealed class SearchData
    {
        [JsonPropertyName("nasa_id")]
        public string NasaId { get; init; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; init; } = string.Empty;

        [JsonPropertyName("description")]
        public string? Description { get; init; }
    }

    private sealed class AssetResponse
    {
        [JsonPropertyName("collection")]
        public AssetCollection? Collection { get; init; }
    }

    private sealed class AssetCollection
    {
        [JsonPropertyName("items")]
        public List<AssetItem>? Items { get; init; }
    }

    private sealed class AssetItem
    {
        [JsonPropertyName("href")]
        public string Href { get; init; } = string.Empty;
    }
}
