// DayPilot — Web Push registration + an explicit "Enable notifications" flow so
// reminders land in the device's OS notification panel (works with the app closed).
(function () {
    const tokenEl = document.querySelector('input[name="__RequestVerificationToken"]');
    const vapid = window.dpVapidKey;
    if (!tokenEl || !vapid) return;
    const supported = ('serviceWorker' in navigator) && ('PushManager' in window) && ('Notification' in window);

    function urlBase64ToUint8Array(base64String) {
        const padding = '='.repeat((4 - base64String.length % 4) % 4);
        const base64 = (base64String + padding).replace(/-/g, '+').replace(/_/g, '/');
        const raw = atob(base64);
        const out = new Uint8Array(raw.length);
        for (let i = 0; i < raw.length; i++) out[i] = raw.charCodeAt(i);
        return out;
    }

    async function subscribe() {
        const reg = await navigator.serviceWorker.ready;
        let sub = await reg.pushManager.getSubscription();
        if (!sub) {
            sub = await reg.pushManager.subscribe({
                userVisibleOnly: true,
                applicationServerKey: urlBase64ToUint8Array(vapid)
            });
        }
        const res = await fetch('/api/push/subscribe', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': tokenEl.value },
            body: JSON.stringify(sub)
        });
        return res.ok;
    }

    async function sendTest() {
        try {
            const res = await fetch('/api/push/test', { method: 'POST', headers: { 'RequestVerificationToken': tokenEl.value } });
            const data = await res.json().catch(() => ({}));
            if (data.sent > 0) { /* the OS notification itself is the confirmation */ }
            else alert(data.message || 'Could not send a test notification.');
        } catch (e) { alert('Could not send a test notification.'); }
    }

    const bar = document.getElementById('dpNotifyBar');
    function showBar() { if (bar && localStorage.getItem('dp_notify_dismissed') !== '1') bar.classList.remove('d-none'); bar?.classList.add('d-flex'); }
    function hideBar() { if (bar) { bar.classList.add('d-none'); bar.classList.remove('d-flex'); } }

    async function enable() {
        if (!supported) { alert('This browser does not support push notifications.'); return; }
        const perm = await Notification.requestPermission();
        if (perm !== 'granted') { alert('Notifications are blocked. Enable them for this site in your browser settings.'); return; }
        try {
            const ok = await subscribe();
            hideBar();
            if (ok) await sendTest();           // immediate confirmation in the notification panel
        } catch (e) { alert('Could not enable notifications: ' + e.message); }
    }

    window.addEventListener('load', async () => {
        if (!supported) return;
        if (bar) {
            bar.querySelector('[data-push-enable]')?.addEventListener('click', enable);
            bar.querySelector('[data-push-test]')?.addEventListener('click', sendTest);
            bar.querySelector('[data-push-dismiss]')?.addEventListener('click', () => { localStorage.setItem('dp_notify_dismissed', '1'); hideBar(); });
        }
        if (Notification.permission === 'granted') {
            try { await subscribe(); } catch (e) { }   // keep subscription fresh
        } else if (Notification.permission === 'default') {
            showBar();                                   // prompt the user to opt in
        }
    });
})();
