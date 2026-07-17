self.importScripts("./service-worker-assets.js");

const cacheNamePrefix = "pianomapper-offline-";
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;
const cacheableAssets = [
    /\.html$/,
    /\.css$/,
    /\.js$/,
    /\.json$/,
    /\.mp3$/,
    /\.wasm$/,
    /\.dll$/,
    /\.dat$/,
    /\.webmanifest$/,
    /\.svg$/,
];

self.addEventListener("install", event => event.waitUntil(cachePublishedAssets()));
self.addEventListener("activate", event => event.waitUntil(removeOldCaches()));
self.addEventListener("fetch", event => event.respondWith(loadFromCache(event.request)));

async function cachePublishedAssets() {
    const requests = self.assetsManifest.assets
        .filter(asset => cacheableAssets.some(pattern => pattern.test(asset.url)))
        .filter(asset => !asset.url.endsWith("service-worker.js"))
        .map(asset => new Request(asset.url, {
            cache: "no-cache",
            integrity: asset.hash,
        }));
    const cache = await caches.open(cacheName);
    await cache.addAll(requests);
}

async function removeOldCaches() {
    const cacheKeys = await caches.keys();
    await Promise.all(cacheKeys
        .filter(key => key.startsWith(cacheNamePrefix) && key !== cacheName)
        .map(key => caches.delete(key)));
}

async function loadFromCache(request) {
    const cache = await caches.open(cacheName);
    if (request.mode === "navigate") {
        return await cache.match("index.html") ?? fetch(request);
    }

    return await cache.match(request) ?? fetch(request);
}
