using System.Collections.Concurrent;
using System.Net.Http;
using System.Text.Json;
using Caelus.Core;

namespace Caelus.Services;

/// <summary>
/// Looks up Octane place titles for Discord Rich Presence. Uses the same games API the website uses
/// (<c>games.octane.wtf/.../multiget-place-details</c>).
/// </summary>
public static class PlaceInfoService
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(8)
    };

    private static readonly ConcurrentDictionary<string, string> Cache = new(StringComparer.OrdinalIgnoreCase);

    private static readonly string[] Endpoints =
    {
        "https://games.octane.wtf/v1/games/multiget-place-details?placeIds={0}",
        "https://octane.wtf/apisite/games/v1/games/multiget-place-details?placeIds={0}"
    };

    /// <summary>Returns the place name, or null if it cannot be resolved.</summary>
    public static async Task<string?> ResolveNameAsync(string? placeId, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(placeId) || !placeId.All(char.IsDigit))
            return null;

        if (Cache.TryGetValue(placeId, out var cached))
            return cached;

        foreach (var template in Endpoints)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, string.Format(template, placeId));
                request.Headers.TryAddWithoutValidation("User-Agent", "XBootstrapper/" + AppInfo.Version);
                request.Headers.TryAddWithoutValidation("Accept", "application/json");

                using var response = await Http.SendAsync(request, token);
                if (!response.IsSuccessStatusCode)
                {
                    Logger.Write("PlaceInfo", $"{response.StatusCode} from {response.RequestMessage?.RequestUri?.Host}");
                    continue;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(token);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: token);
                if (doc.RootElement.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    if (!item.TryGetProperty("placeId", out var idEl))
                        continue;

                    var id = idEl.ValueKind == JsonValueKind.Number
                        ? idEl.GetInt64().ToString()
                        : idEl.GetString();
                    if (!string.Equals(id, placeId, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var name = item.TryGetProperty("name", out var nameEl) ? nameEl.GetString()?.Trim() : null;
                    if (string.IsNullOrWhiteSpace(name))
                        return null;

                    Cache[placeId] = name;
                    Logger.Write("PlaceInfo", $"Place {placeId} is \"{name}\".");
                    return name;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.Write("PlaceInfo", $"Could not resolve place {placeId}: {ex.Message}");
            }
        }

        return null;
    }

    /// <summary>Discord state/details fields are capped at 128 characters.</summary>
    public static string TruncateForDiscord(string text, int max = 128)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= max)
            return text;
        if (max <= 1)
            return text[..max];
        return text[..(max - 1)] + "…";
    }
}
