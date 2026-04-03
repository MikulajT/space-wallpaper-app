using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace SpaceWallpaperApp;

public sealed class NasaImageService
{
    private const int MinimumWallpaperWidth = 2560;
    private const int MinimumWallpaperHeight = 1440;
    private const double MinimumAspectRatio = 1.2;
    private const double MaximumAspectRatio = 2.25;

    private static readonly HttpClient HttpClient = new()
    {
        BaseAddress = new Uri("https://images-api.nasa.gov/")
    };

    private static readonly string[] GenericQueries =
    {
        "nebula",
        "galaxy",
        "deep space",
        "star cluster",
        "milky way",
        "saturn",
        "jupiter",
        "earth from space",
        "mars",
        "moon"
    };

    public async Task<NasaImageCandidate> GetHighResolutionImageAsync(string topic, string downloadDirectory, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(downloadDirectory);

        var queries = BuildQueries(topic).ToArray();

        foreach (var query in queries)
        {
            var result = await TryQueryAsync(query, downloadDirectory, cancellationToken);
            if (result is not null)
            {
                return result;
            }
        }

        throw new InvalidOperationException("NASA did not return a suitable 2K+ image. Please try again.");
    }

    private async Task<NasaImageCandidate?> TryQueryAsync(string query, string downloadDirectory, CancellationToken cancellationToken)
    {
        var response = await HttpClient.GetFromJsonAsync<SearchResponse>(
            $"search?q={Uri.EscapeDataString(query)}&media_type=image&page_size=100",
            cancellationToken);

        var items = response?.Collection?.Items ?? [];

        foreach (var item in items.OrderBy(_ => Guid.NewGuid()))
        {
            var data = item.Data?.FirstOrDefault();
            if (data is null || !LooksPhotographic(data))
            {
                continue;
            }

            var manifest = await HttpClient.GetFromJsonAsync<AssetResponse>($"asset/{Uri.EscapeDataString(data.NasaId)}", cancellationToken);
            var assetUrls = manifest?.Collection?.Items?.Select(x => x.Href).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray() ?? [];

            var originalUrl = assetUrls
                .Where(IsDownloadableImage)
                .OrderByDescending(GetPriority)
                .FirstOrDefault();

            if (originalUrl is null)
            {
                continue;
            }

            var fileName = Path.GetFileName(new Uri(originalUrl).LocalPath);
            var localPath = Path.Combine(downloadDirectory, fileName);

            if (!File.Exists(localPath))
            {
                await using var remoteStream = await HttpClient.GetStreamAsync(originalUrl, cancellationToken);
                await using var localStream = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None);
                await remoteStream.CopyToAsync(localStream, cancellationToken);
            }

            using var image = Image.FromFile(localPath);
            var width = image.Width;
            var height = image.Height;
            var aspectRatio = (double)width / height;

            if (width < MinimumWallpaperWidth
                || height < MinimumWallpaperHeight
                || width < height
                || aspectRatio < MinimumAspectRatio
                || aspectRatio > MaximumAspectRatio)
            {
                continue;
            }

            return new NasaImageCandidate(
                data.Title,
                CleanDescription(data.Description),
                width,
                height,
                localPath,
                $"https://images.nasa.gov/details-{data.NasaId}");
        }

        return null;
    }

    private static IEnumerable<string> BuildQueries(string topic)
    {
        var cleanTopic = topic.Trim();

        if (!string.IsNullOrWhiteSpace(cleanTopic))
        {
            yield return cleanTopic;
            yield return $"{cleanTopic} space";
            yield return $"{cleanTopic} telescope";
        }

        foreach (var query in GenericQueries)
        {
            yield return query;
        }
    }

    private static bool LooksPhotographic(SearchData data)
    {
        var combined = $"{data.Title} {data.Description}".ToLowerInvariant();
        var blockedTerms = new[]
        {
            "illustration",
            "artist",
            "concept",
            "rendering",
            "poster",
            "patch",
            "diagram",
            "infographic",
            "simulation",
            "composite logo",
            "mosaic",
            "collage",
            "triptych",
            "three-panel",
            "panel",
            "montage"
        };

        return blockedTerms.All(term => !combined.Contains(term, StringComparison.Ordinal));
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

    private static string CleanDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return "Real NASA space image.";
        }

        var normalized = description.ReplaceLineEndings(" ").Trim();
        return normalized.Length <= 260 ? normalized : $"{normalized[..257]}...";
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
