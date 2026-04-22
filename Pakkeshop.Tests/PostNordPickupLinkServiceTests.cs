using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Pakkeshop.Services;
using Xunit;

namespace Pakkeshop.Tests;

public sealed class PostNordPickupLinkServiceTests
{
    [Fact]
    public async Task ResolveFromShortLinkAsync_Follows302AndReadsShipmentAndPickupFromLocation()
    {
        var handler = new Redirect302Handler();
        using var http = new HttpClient(handler);
        var svc = new PostNordPickupLinkService(http, NullLogger<PostNordPickupLinkService>.Instance);

        var result = await svc.ResolveFromShortLinkAsync("https://l.postnord.com/testtoken");

        Assert.NotNull(result);
        Assert.Equal("00373325386653349965", result.ShipmentId);
        Assert.Equal("99654329", result.PickupCode);
        Assert.Equal(1, handler.SendCount);
    }

    [Fact]
    public async Task ResolveFromShortLinkAsync_ReturnsNullOn404()
    {
        var handler = new NotFoundHandler();
        using var http = new HttpClient(handler);
        var svc = new PostNordPickupLinkService(http, NullLogger<PostNordPickupLinkService>.Instance);

        var result = await svc.ResolveFromShortLinkAsync("https://l.postnord.com/bad");

        Assert.Null(result);
    }

    [Fact]
    public void PostNordLinkMatcher_FindsFirstShortLink()
    {
        const string body = "Se https://l.postnord.com/AbCd12 eller app";
        Assert.Equal("https://l.postnord.com/AbCd12", PostNordLinkMatcher.FirstMatch(body));
    }

    private sealed class Redirect302Handler : HttpMessageHandler
    {
        public int SendCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            SendCount++;
            Assert.Equal("l.postnord.com", request.RequestUri!.Host);
            var res = new HttpResponseMessage(HttpStatusCode.Found);
            res.Headers.Location =
                new Uri("https://pickupcode.aws.postnord.com/?itemid=00373325386653349965&code=99654329");
            return Task.FromResult(res);
        }
    }

    private sealed class NotFoundHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}
