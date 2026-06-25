// DayPilot service worker — app-shell caching + offline fallback (PRD Phase 3 / PWA).
const CACHE = 'daypilot-v1';
const APP_SHELL = [
    '/offline.html',
    '/css/site.css',
    '/js/site.js',
    '/lib/bootstrap/dist/css/bootstrap.min.css',
    '/lib/bootstrap/dist/js/bootstrap.bundle.min.js',
    '/lib/jquery/dist/jquery.min.js',
    '/icons/icon-192.png'
];

self.addEventListener('install', event => {
    event.waitUntil(
        caches.open(CACHE)
            .then(cache => cache.addAll(APP_SHELL))
            .then(() => self.skipWaiting())
            .catch(() => { /* some assets may 404 in dev; ignore */ })
    );
});

self.addEventListener('activate', event => {
    event.waitUntil(
        caches.keys()
            .then(keys => Promise.all(keys.filter(k => k !== CACHE).map(k => caches.delete(k))))
            .then(() => self.clients.claim())
    );
});

self.addEventListener('fetch', event => {
    const req = event.request;
    if (req.method !== 'GET') return;

    const url = new URL(req.url);
    if (url.origin !== self.location.origin) return;       // leave CDN/cross-origin alone

    // Navigations: network-first, fall back to the offline page when offline.
    if (req.mode === 'navigate') {
        event.respondWith(fetch(req).catch(() => caches.match('/offline.html')));
        return;
    }

    // Static assets: cache-first, then network (and cache the result).
    event.respondWith(
        caches.match(req).then(cached => cached || fetch(req).then(res => {
            if (res && res.status === 200 && res.type === 'basic') {
                const copy = res.clone();
                caches.open(CACHE).then(c => c.put(req, copy));
            }
            return res;
        }))
    );
});
