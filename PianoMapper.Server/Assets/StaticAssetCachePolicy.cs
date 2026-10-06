using Microsoft.Net.Http.Headers;

namespace PianoMapper.Server.Assets;

/// <summary>
/// Makes the browser revalidate the app's own static files on every use. Without a Cache-Control header a browser
/// reuses a copy it fetched earlier for a heuristic share of the file's age (hours to days) without asking the server,
/// so after an update the new WebAssembly (always revalidated) could run against a stale JavaScript module it imports.
/// A revalidation is a cheap 304 while the file is unchanged.
/// </summary>
internal static class StaticAssetCachePolicy
{
    private const string RevalidateEveryUse = "no-cache";

    // Framework files are fingerprinted and set their own caching. The piano samples are large and never change in place.
    private static readonly PathString[] CacheablePrefixes = ["/_framework", "/audio"];

    internal static void Apply(HttpContext context)
    {
        foreach (PathString prefix in CacheablePrefixes)
        {
            if (context.Request.Path.StartsWithSegments(prefix))
            {
                return;
            }
        }

        context.Response.Headers[HeaderNames.CacheControl] = RevalidateEveryUse;
    }
}
