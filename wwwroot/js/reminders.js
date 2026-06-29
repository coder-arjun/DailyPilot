// DayPilot — task reminders delivered as REAL OS notifications (phone/desktop
// notification drawer), never an in-app popup. While the app is open this fires
// the notification precisely on time; when the app is closed, the server's Web
// Push job delivers it. Both paths de-dupe via the server (ReminderFiredOn).
(function () {
    const tokenEl = document.querySelector('input[name="__RequestVerificationToken"]');
    if (!tokenEl) return;                                  // signed-out
    const token = () => tokenEl.value;
    const timers = {};

    // Ask for notification permission on first interaction (browsers require a gesture).
    if ('Notification' in window && Notification.permission === 'default') {
        const ask = () => { Notification.requestPermission().catch(() => { }); window.removeEventListener('pointerdown', ask); };
        window.addEventListener('pointerdown', ask, { once: true });
    }

    function ack(taskId) {
        fetch('/api/reminders/ack/' + taskId, { method: 'POST', headers: { 'RequestVerificationToken': token() } }).catch(() => { });
    }

    async function notify(r) {
        // Only fire if the user allows notifications and a service worker is available;
        // otherwise leave it for the server push (no in-app fallback by design).
        if (!('Notification' in window) || Notification.permission !== 'granted') return;
        try {
            const reg = await navigator.serviceWorker.ready;
            await reg.showNotification('⏰ ' + r.title, {
                body: 'Reminder · ' + r.time,
                icon: '/icons/icon-192.png',
                badge: '/icons/icon-192.png',
                tag: 'dp-task-' + r.taskId,
                renotify: true,
                requireInteraction: true,
                vibrate: [300, 150, 300],
                data: { url: '/Tasks' }
            });
            ack(r.taskId);                                  // stop the server from re-pushing the same one
        } catch (e) { /* SW not ready — server push will cover it */ }
    }

    function schedule(list) {
        const now = Date.now();
        list.forEach(r => {
            const at = Date.parse(r.dueAtUtc);
            const delta = at - now;
            if (timers[r.taskId]) clearTimeout(timers[r.taskId]);
            if (delta > 1000) {
                timers[r.taskId] = setTimeout(() => notify(r), delta);   // fire exactly on time
            } else if (delta > -2 * 60 * 60 * 1000) {
                notify(r);                                               // due within the last 2h → fire now
            }
            // older than 2h: leave it; not worth a late alarm
        });
    }

    async function load() {
        try {
            const res = await fetch('/api/reminders/today', { headers: { 'Accept': 'application/json' } });
            if (res.ok) schedule(await res.json());
        } catch (e) { /* offline */ }
    }

    window.addEventListener('DOMContentLoaded', () => { load(); setInterval(load, 5 * 60 * 1000); });
})();
