using System.Net;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;

namespace Pakkeshop.Services;

public sealed class PostNordPickupLinkService : IPostNordPickupLinkService
{
    private const string ShortLinkHost = "l.postnord.com";
    private const string PickupPageHost = "pickupcode.aws.postnord.com";

    private readonly HttpClient _http;
    private readonly ILogger<PostNordPickupLinkService> _logger;

    public PostNordPickupLinkService(HttpClient http, ILogger<PostNordPickupLinkService> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<PostNordResolvedPickup?> ResolveFromShortLinkAsync(string candidateUrl,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetInitialShortLinkUri(candidateUrl, out var current))
            return null;

        for (var hop = 0; hop < 8; hop++)
        {
            if (TryParsePickupFromUri(current, out var fromUri))
                return fromUri;

            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            using var response =
                await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (IsRedirect(response.StatusCode))
            {
                if (response.Headers.Location is null)
                {
                    _logger.LogWarning("PostNord redirect uden Location fra {Uri}", current);
                    return null;
                }

                var next = response.Headers.Location.IsAbsoluteUri
                    ? response.Headers.Location
                    : new Uri(current, response.Headers.Location);

                if (!IsAllowedRedirectHost(next.Host))
                {
                    _logger.LogWarning("PostNord redirect til ikke-tilladt host {Host}", next.Host);
                    return null;
                }

                if (TryParsePickupFromUri(next, out var fromLocation))
                    return fromLocation;

                current = next;
                continue;
            }

            if (response.IsSuccessStatusCode)
            {
                _logger.LogWarning("PostNord link {Uri} returnerede {Status} uden hentedata i URL",
                    current, (int)response.StatusCode);
                return null;
            }

            _logger.LogWarning("PostNord link {Uri} fejlede med {Status}", current, (int)response.StatusCode);
            return null;
        }

        _logger.LogWarning("PostNord redirect-kæde for lang for {Uri}", current);
        return null;
    }

    private static bool TryParsePickupFromUri(Uri uri, out PostNordResolvedPickup? result)
    {
        result = null;
        if (!uri.Host.Equals(PickupPageHost, StringComparison.OrdinalIgnoreCase))
            return false;

        var q = QueryHelpers.ParseQuery(uri.Query);
        if (!q.TryGetValue("itemid", out var itemValues) || !q.TryGetValue("code", out var codeValues))
            return false;

        var itemid = itemValues.ToString().Trim();
        var code = codeValues.ToString().Trim();
        if (itemid.Length == 0 || code.Length == 0)
            return false;

        result = new PostNordResolvedPickup { ShipmentId = itemid, PickupCode = code };
        return true;
    }

    private static bool TryGetInitialShortLinkUri(string candidateUrl, out Uri uri)
    {
        uri = null!;
        if (string.IsNullOrWhiteSpace(candidateUrl))
            return false;

        var trimmed = candidateUrl.Trim();

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var parsed))
            return false;

        if (!parsed.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!parsed.Host.Equals(ShortLinkHost, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.IsNullOrEmpty(parsed.UserInfo))
            return false;

        if (parsed.AbsolutePath is "/" or "\\" or "")
            return false;

        uri = parsed;
        return true;
    }

    private static bool IsAllowedRedirectHost(string host) =>
        host.Equals(ShortLinkHost, StringComparison.OrdinalIgnoreCase)
        || host.Equals(PickupPageHost, StringComparison.OrdinalIgnoreCase);

    private static bool IsRedirect(HttpStatusCode statusCode)
    {
        var n = (int)statusCode;
        return n is 301 or 302 or 303 or 307 or 308;
    }
}
