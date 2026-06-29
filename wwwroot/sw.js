// DayPilot service worker — app-shell caching + offline fallback (PRD Phase 3 / PWA).
const CACHE = 'daypilot-v10';
const APP_SHELL = [
    '/offline.html',
    '/manifest.webmanifest',
    '/css/site.css',
    '/js/site.js',
    '/lib/bootstrap/dist/css/bootstrap.min.css',
    '/lib/bootstrap/dist/js/bootstrap.bundle.min.js',
    '/lib/jquery/dist/jquery.min.js',
    '/icons/icon-192.png',
    '/icons/icon-512.png'
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

// ---- Web Push: show notification when one arrives (works with the app closed) ----
self.addEventListener('push', event => {
    let data = {};
    try { data = event.data ? event.data.json() : {}; } catch (e) { data = { title: 'DayPilot', body: event.data ? event.data.text() : '' }; }
    const title = data.title || 'DayPilot';
    const options = {
        body: data.body || '',
        icon: '/icons/icon-192.png',
        badge: '/icons/icon-192.png',
        tag: data.tag,
        renotify: !!data.tag,
        requireInteraction: true,
        data: { url: data.url || '/' },
        vibrate: [300, 150, 300]
    };
    event.waitUntil(self.registration.showNotification(title, options));
});

self.addEventListener('notificationclick', event => {
    event.notification.close();
    const url = (event.notification.data && event.notification.data.url) || '/';
    event.waitUntil(
        self.clients.matchAll({ type: 'window', includeUncontrolled: true }).then(list => {
            for (const c of list) { if ('focus' in c) { c.navigate(url); return c.focus(); } }
            if (self.clients.openWindow) return self.clients.openWindow(url);
        })
    );
});

self.addEventListener('fetch', event => {
    const req = event.request;
    if (req.method !== 'GET') return;

    let url;
    try { url = new URL(req.url); } catch { return; }
    if (url.origin !== self.location.origin) return;   // leave CDN / dev tooling (Browser Link) alone

    // Navigations: network-first, fall back to the cached offline page.
    if (req.mode === 'navigate') {
        event.respondWith((async () => {
            try {
                return await fetch(req);
            } catch {
                const offline = await caches.match('/offline.html');
                return offline || new Response('You are offline.', {
                    status: 503, headers: { 'Content-Type': 'text/plain' }
                });
            }
        })());
        return;
    }

    // Static assets: network-first (so CSS/JS updates always apply), cache fallback for offline.
    event.respondWith((async () => {
        try {
            const res = await fetch(req);
            if (res && res.status === 200 && res.type === 'basic') {
                const copy = res.clone();
                caches.open(CACHE).then(c => c.put(req, copy)).catch(() => { });
            }
            return res;
        } catch {
            const cached = await caches.match(req);
            return cached || new Response('', { status: 504, statusText: 'Offline' });
        }
    })());
});
