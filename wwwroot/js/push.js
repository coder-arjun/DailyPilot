// DayPilot — register the browser for Web Push so reminders arrive when the app is closed.
(function () {
    const tokenEl = document.querySelector('input[name="__RequestVerificationToken"]');
    const vapid = window.dpVapidKey;
    if (!tokenEl || !vapid) return;                                   // signed-out or push not configured
    if (!('serviceWorker' in navigator) || !('PushManager' in window)) return;

    function urlBase64ToUint8Array(base64String) {
        const padding = '='.repeat((4 - base64String.length % 4) % 4);
        const base64 = (base64String + padding).replace(/-/g, '+').replace(/_/g, '/');
        const raw = atob(base64);
        const out = new Uint8Array(raw.length);
        for (let i = 0; i < raw.length; i++) out[i] = raw.charCodeAt(i);
        return out;
    }

    async function subscribe() {
        try {
            if (Notification.permission !== 'granted') return;       // only after the user allows notifications
            const reg = await navigator.serviceWorker.ready;
            let sub = await reg.pushManager.getSubscription();
            if (!sub) {
                sub = await reg.pushManager.subscribe({
                    userVisibleOnly: true,
                    applicationServerKey: urlBase64ToUint8Array(vapid)
                });
            }
            await fetch('/api/push/subscribe', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': tokenEl.value },
                body: JSON.stringify(sub)
            });
        } catch (e) { /* push unavailable / blocked */ }
    }

    window.addEventListener('load', () => {
        if (Notification.permission === 'granted') {
            subscribe();
        } else if (Notification.permission === 'default') {
            // subscribe right after the user grants permission (reminders.js triggers the prompt)
            const t = setInterval(() => {
                if (Notification.permission === 'granted') { clearInterval(t); subscribe(); }
            }, 3000);
            setTimeout(() => clearInterval(t), 120000);
        }
    });
})();
