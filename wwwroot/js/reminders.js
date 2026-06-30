// DayPilot — task reminders delivered as REAL OS notifications (phone/desktop
// notification drawer), never an in-app popup. While the app is open this fires
// the notification precisely on time; when the app is closed, the server's Web
// Push job delivers it. Both paths de-dupe via the server (ReminderFiredOn).
(function () {
    const tokenEl = document.querySelector('input[name="__RequestVerificationToken"]');
    if (!tokenEl) return;                                  // signed-out
    const token = () => tokenEl.value;
    const timers = {};

    // Auto-correct the account timezone from the browser (registration may have
    // defaulted to the server's zone, which would make reminders fire at the wrong time).
    try {
        const tz = Intl.DateTimeFormat().resolvedOptions().timeZone;
        if (tz && localStorage.getItem('dp_tz') !== tz) {
            fetch('/api/profile/timezone?tz=' + encodeURIComponent(tz), {
                method: 'POST', headers: { 'RequestVerificationToken': token() }
            }).then(r => { if (r.ok) localStorage.setItem('dp_tz', tz); }).catch(() => { });
        }
    } catch (e) { /* Intl unavailable */ }

    // Ask for notification permission on first interaction (browsers require a gesture).
    if ('Notification' in window && Notification.permission === 'default') {
        const ask = () => { Notification.requestPermission().catch(() => { }); window.removeEventListener('pointerdown', ask); };
        window.addEventListener('pointerdown', ask, { once: true });
    }

    function ack(taskId) {
        fetch('/api/reminders/ack/' + taskId, { method: 'POST', headers: { 'RequestVerificationToken': token() } }).catch(() => { });
    }

    // Mobile browsers only allow speech after the page has had a user gesture, so
    // "prime" the engine with a silent utterance on first interaction.
    let ttsPrimed = false;
    function primeTts() {
        if (ttsPrimed || !('speechSynthesis' in window)) return;
        try {
            window.speechSynthesis.getVoices();
            const u = new SpeechSynthesisUtterance(' ');
            u.volume = 0;
            window.speechSynthesis.speak(u);
            ttsPrimed = true;
        } catch (e) { }
    }
    window.addEventListener('pointerdown', primeTts);
    window.addEventListener('keydown', primeTts);

    function speak(text, force) {
        if (!force && !window.dpSpeakReminders) return;
        if (!('speechSynthesis' in window)) { if (force) alert('Your browser does not support text-to-speech.'); return; }
        try {
            window.speechSynthesis.resume();
            const u = new SpeechSynthesisUtterance(text);
            const voices = window.speechSynthesis.getVoices();
            const en = voices.find(v => /^en/i.test(v.lang));
            if (en) u.voice = en;
            u.rate = 1; u.pitch = 1; u.volume = 1;
            window.speechSynthesis.cancel();
            window.speechSynthesis.speak(u);
        } catch (e) { /* TTS unavailable */ }
    }

    // "Test voice" button (Settings) — explicit gesture, so it always speaks.
    document.addEventListener('click', (e) => {
        if (e.target.closest('[data-tts-test]')) speak('This is how your DayPilot reminders will sound.', true);
    });

    // The service worker asks the page to speak when a push arrives or its notification is tapped.
    if ('serviceWorker' in navigator) {
        navigator.serviceWorker.addEventListener('message', (e) => {
            if (e.data && e.data.type === 'dp-speak' && e.data.text) speak(e.data.text, true);
        });
    }

    // When the app is opened by tapping a reminder notification, read it aloud (the tap is a user gesture).
    window.addEventListener('DOMContentLoaded', () => {
        if (location.hash.startsWith('#speak=')) {
            const text = decodeURIComponent(location.hash.slice(7));
            history.replaceState(null, '', location.pathname + location.search);
            if (text) setTimeout(() => speak(text, true), 400);
        }
    });

    async function notify(r) {
        // Only fire if the user allows notifications and a service worker is available;
        // otherwise leave it for the server push (no in-app fallback by design).
        if (!('Notification' in window) || Notification.permission !== 'granted') return;
        try {
            const reg = await navigator.serviceWorker.ready;
            await reg.showNotification('⏰ ' + r.title, {
                body: 'Reminder · ' + r.time,
                icon: '/icons/icon-192.png',
                badge: '/icons/badge.png',
                tag: 'dp-task-' + r.taskId,
                renotify: true,
                requireInteraction: true,
                vibrate: [300, 150, 300],
                data: { url: '/Tasks', speak: 'Reminder. ' + r.title }
            });
            ack(r.taskId);                                  // stop the server from re-pushing the same one
            speak('Reminder. ' + r.title);
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
