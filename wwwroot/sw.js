// DayPilot service worker — app-shell caching + offline fallback (PRD Phase 3 / PWA).
const CACHE = 'daypilot-v15';
const APP_SHELL = [
    '/offline.html',
    '/manifest.webmanifest',
    '/css/site.css',
    '/js/site.js',
    '/lib/bootstrap/dist/css/bootstrap.min.css',
    '/lib/bootstrap/dist/js/bootstrap.bundle.min.js',
    '/lib/jquery/dist/jquery.min.js',
    '/icons/logo-192.png',
    '/icons/logo-512.png'
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
        badge: '/icons/badge.png',
        tag: data.tag,
        renotify: !!data.tag,
        requireInteraction: true,
        data: { url: data.url || '/', speak: title + '. ' + (data.body || '') },
        vibrate: [300, 150, 300]
    };
    event.waitUntil((async () => {
        await self.registration.showNotification(title, options);
        // If a tab is open, ask it to read the reminder aloud (TTS works only in a page).
        const clients = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
        for (const c of clients) c.postMessage({ type: 'dp-speak', text: title + '. ' + (data.body || '') });
    })());
});

self.addEventListener('notificationclick', event => {
    event.notification.close();
    const d = event.notification.data || {};
    const url = d.url || '/';
    const speak = d.speak ? ('#speak=' + encodeURIComponent(d.speak)) : '';
    event.waitUntil((async () => {
        const list = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
        for (const c of list) {
            if ('focus' in c) {
                await c.focus();
                if (d.speak) c.postMessage({ type: 'dp-speak', text: d.speak });
                return;
            }
        }
        // App was closed: open it with the text in the hash so the page reads it on load.
        if (self.clients.openWindow) return self.clients.openWindow(url + speak);
    })());
});

self.addEventListener('fetch', event => {
    const req = event.request;
    if (req.method !== 'GET') return;

    let url;
    try { url = new URL(req.url); } catch { return; }
    if (url.origin !== self.location.origin) return;   // leave CDN / dev tooling (Browser Link) alone

    // Never let the service worker touch auth pages — a cached login page can serve a
    // stale antiforgery token/cookie, which makes the first PWA login fail. Always go
    // straight to the network for Identity so the token and cookie are fresh.
    if (url.pathname.startsWith('/Identity/')) return;

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
