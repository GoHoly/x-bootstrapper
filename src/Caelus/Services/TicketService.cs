using System.Net.Http;
using System.Text;
using System.Text.Json;
using Caelus.Core;

namespace Caelus.Services;

public static class TicketService
{
    // UNVERIFIED: unlike the version-check endpoint, no auth/ticket-redemption URL showed up in
    // OctanePlayerLauncher.exe's strings - that call is probably made by octane.wtf's own website
    // JS or by the client itself, not the bootstrapper, so it wasn't in this binary to find.
    // Left overridable via settings; if you have Octane's real endpoint, set it there instead of
    // relying on this guess.
    public static string RedeemUrl { get; set; } = "https://octane.wtf/v1/authentication-ticket/redeem";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    static TicketService()
    {
        Http.DefaultRequestHeaders.UserAgent.ParseAdd("Caelus/1.0");
    }

    public static async Task<string?> RedeemAsync(string ticket, CancellationToken token)
    {
        var bodies = new[]
        {
            JsonSerializer.Serialize(new { ticket }),
            JsonSerializer.Serialize(new { authenticationTicket = ticket }),
            ticket
        };

        foreach (var body in bodies)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, RedeemUrl);
                request.Content = new StringContent(body, Encoding.UTF8, body.StartsWith('{') ? "application/json" : "text/plain");
                request.Headers.TryAddWithoutValidation("RBXAuthenticationNegotiation", "https://octane.wtf");
                using var response = await Http.SendAsync(request, token);
                var text = (await response.Content.ReadAsStringAsync(token)).Trim();
                if (!response.IsSuccessStatusCode || string.IsNullOrWhiteSpace(text) || text.StartsWith('<'))
                    continue;

                try
                {
                    using var doc = JsonDocument.Parse(text);
                    foreach (var name in new[] { "ticket", "authenticationTicket", "gameinfo", "value" })
                    {
                        if (doc.RootElement.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
                            return value.GetString();
                    }
                }
                catch (JsonException)
                {
                    return text.Trim('"');
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Logger.Write("Ticket", $"Redeem attempt failed: {ex.Message}");
            }
        }

        return null;
    }
}
