using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using PianoMapper.Server.Assets;

namespace PianoMapper.Tests.UnitTests;

public sealed class StaticAssetCachePolicyTests
{
    [Theory]
    [InlineData("/js/audio.js")]
    [InlineData("/js/canvas.js")]
    [InlineData("/css/app.css")]
    [InlineData("/index.html")]
    [InlineData("/manifest.webmanifest")]
    [InlineData("/icons/piano-192.svg")]
    public void Apply_UnfingerprintedAppAsset_RevalidatesBeforeEveryUse(string path)
    {
        DefaultHttpContext context = CreateContext(path);

        StaticAssetCachePolicy.Apply(context);

        Assert.Equal("no-cache", context.Response.Headers.CacheControl.ToString());
    }

    [Theory]
    [InlineData("/_framework/blazor.webassembly.js")]
    [InlineData("/_framework/dotnet.native.9f3a1c2b7d.wasm")]
    public void Apply_FrameworkFile_LeavesItsOwnCaching(string path)
    {
        DefaultHttpContext context = CreateContext(path);

        StaticAssetCachePolicy.Apply(context);

        Assert.False(context.Response.Headers.ContainsKey(HeaderNames.CacheControl));
    }

    [Fact]
    public void Apply_PianoSample_StaysCacheable()
    {
        DefaultHttpContext context = CreateContext("/audio/piano/salamander/A0v1.mp3");

        StaticAssetCachePolicy.Apply(context);

        Assert.False(context.Response.Headers.ContainsKey(HeaderNames.CacheControl));
    }

    private static DefaultHttpContext CreateContext(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        return context;
    }
}
