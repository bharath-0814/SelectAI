// SelectAI Service Worker — PWA & Auto-Update Engine
const CACHE_NAME = 'selectai-cache-v1.1.0';
const STATIC_ASSETS = [
    './',
    './index.html',
    './version.json',
    './manifest.json',
    './app_icon.png'
];

// Install: Cache core application assets
self.addEventListener('install', (event) => {
    event.waitUntil(
        caches.open(CACHE_NAME).then((cache) => {
            return cache.addAll(STATIC_ASSETS);
        }).then(() => self.skipWaiting())
    );
});

// Activate: Purge obsolete cache versions
self.addEventListener('activate', (event) => {
    event.waitUntil(
        caches.keys().then((keys) => {
            return Promise.all(
                keys.map((key) => {
                    if (key !== CACHE_NAME) {
                        return caches.delete(key);
                    }
                })
            );
        }).then(() => self.clients.claim())
    );
});

// Fetch: Network-first for version.json (to get instant updates), cache-first for other assets
self.addEventListener('fetch', (event) => {
    const requestUrl = new URL(event.request.url);

    // Network-first for version checking
    if (requestUrl.pathname.endsWith('version.json')) {
        event.respondWith(
            fetch(event.request)
                .then((networkResponse) => {
                    const clonedResponse = networkResponse.clone();
                    caches.open(CACHE_NAME).then((cache) => cache.put(event.request, clonedResponse));
                    return networkResponse;
                })
                .catch(() => caches.match(event.request))
        );
        return;
    }

    // Cache-first for general assets with background revalidation
    event.respondWith(
        caches.match(event.request).then((cachedResponse) => {
            if (cachedResponse) {
                // Fetch in background to update cache
                fetch(event.request).then((networkResponse) => {
                    if (networkResponse && networkResponse.status === 200) {
                        caches.open(CACHE_NAME).then((cache) => cache.put(event.request, networkResponse));
                    }
                }).catch(() => { /* Offline fallback */ });

                return cachedResponse;
            }

            return fetch(event.request).then((networkResponse) => {
                if (networkResponse && networkResponse.status === 200 && event.request.method === 'GET') {
                    const cloned = networkResponse.clone();
                    caches.open(CACHE_NAME).then((cache) => cache.put(event.request, cloned));
                }
                return networkResponse;
            });
        })
    );
});

// Handle update check requests from web clients
self.addEventListener('message', (event) => {
    if (event.data && event.data.action === 'skipWaiting') {
        self.skipWaiting();
    }
});
