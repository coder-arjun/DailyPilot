// DayPilot client-side enhancements.

(function () {
    function setCookie(name, value) {
        document.cookie = `${name}=${value};path=/;max-age=${60 * 60 * 24 * 365};samesite=lax`;
    }

    // ---- Gentle "task complete" chime (Web Audio — no asset needed) ----
    function playCompleteChime() {
        try {
            var Ctx = window.AudioContext || window.webkitAudioContext;
            if (!Ctx) return;
            var ac = new Ctx();
            if (ac.state === 'suspended' && ac.resume) ac.resume();
            var t0 = ac.currentTime;
            function note(freq, at, dur, vol) {
                var o = ac.createOscillator(), g = ac.createGain();
                o.type = 'sine'; o.frequency.value = freq;
                var t = t0 + at;
                g.gain.setValueAtTime(0.0001, t);
                g.gain.exponentialRampToValueAtTime(vol, t + 0.015);
                g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
                o.connect(g); g.connect(ac.destination);
                o.start(t); o.stop(t + dur + 0.02);
            }
            note(1174.66, 0, 0.34, 0.20);      // D6
            note(1567.98, 0.10, 0.42, 0.16);   // G6 — soft ascending two-note bell
            setTimeout(function () { try { ac.close(); } catch (e) { } }, 1000);
        } catch (e) { /* audio unavailable */ }
    }
    window.dpPlayComplete = playCompleteChime;

    // ---- PWA install prompt (must be registered early, before DOMContentLoaded) ----
    let deferredInstallPrompt = null;
    window.addEventListener('beforeinstallprompt', function (e) {
        e.preventDefault();
        deferredInstallPrompt = e;
        const item = document.getElementById('pwaInstallItem');
        if (item) item.classList.remove('d-none');
    });
    window.addEventListener('appinstalled', function () {
        deferredInstallPrompt = null;
        const item = document.getElementById('pwaInstallItem');
        if (item) item.classList.add('d-none');
    });

    window.addEventListener('DOMContentLoaded', function () {
        // ---- PWA "Install app" menu item ----
        const installItem = document.getElementById('pwaInstallItem');
        if (installItem) {
            installItem.addEventListener('click', async function (e) {
                e.preventDefault();
                if (!deferredInstallPrompt) {
                    alert('To install: use your browser’s install icon in the address bar (or "Add to Home screen" on mobile).');
                    return;
                }
                deferredInstallPrompt.prompt();
                await deferredInstallPrompt.userChoice;
                deferredInstallPrompt = null;
                installItem.classList.add('d-none');
            });
        }

        // ---- Invite a friend (share / copy a professional invitation) ----
        const inviteItem = document.getElementById('inviteItem');
        if (inviteItem) {
            inviteItem.addEventListener('click', async function (e) {
                e.preventDefault();
                const url = window.location.origin;
                // Keep the link out of the message text; the share sheet adds `url` itself.
                const message =
                    "I’ve been using DayPilot to plan my day, build better habits, and keep on top of my tasks — " +
                    "it automatically carries forward anything I don’t finish and even has an AI assistant and coach. " +
                    "I think you’d get a lot out of it. Create your free account here:";
                try {
                    if (navigator.share) {
                        await navigator.share({ title: 'DayPilot — Plan. Complete. Move Forward.', text: message, url: url });
                    } else {
                        await navigator.clipboard.writeText(message + ' ' + url);
                        alert('Invitation copied to clipboard — paste it into an email or message.');
                    }
                } catch (err) { /* user cancelled share */ }
            });
        }

        // ---- Play a chime when a task is marked complete (Today list) ----
        document.addEventListener('click', function (e) {
            var btn = e.target.closest('.dp-task-check');
            if (!btn || btn.classList.contains('done') || btn.dataset.dpBusy) return;   // only when marking complete
            var form = btn.closest('form');
            if (!form) return;
            e.preventDefault();
            btn.dataset.dpBusy = '1';
            playCompleteChime();
            // instant visual feedback while the tone plays, then persist
            btn.classList.add('done');
            var icon = btn.querySelector('i'); if (icon) icon.className = 'bi bi-check-lg';
            var card = btn.closest('.dp-task-card'); if (card) card.classList.add('is-done');
            setTimeout(function () { form.requestSubmit ? form.requestSubmit() : form.submit(); }, 480);
        });

        // ---- Server-side TempData toasts ----
        document.querySelectorAll('.dp-toast').forEach(function (el) {
            try { new bootstrap.Toast(el, { delay: 3000 }).show(); } catch (e) { }
        });

        // ---- Password visibility toggles ----
        document.querySelectorAll('[data-pw-toggle]').forEach(function (btn) {
            btn.addEventListener('click', function () {
                const sel = btn.getAttribute('data-pw-toggle');
                const input = document.querySelector(sel);
                if (!input) return;
                const show = input.type === 'password';
                input.type = show ? 'text' : 'password';
                const icon = btn.querySelector('i');
                if (icon) icon.className = show ? 'bi bi-eye-slash' : 'bi bi-eye';
            });
        });
    });
})();
