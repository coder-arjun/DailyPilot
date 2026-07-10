// DayPilot client-side enhancements.

(function () {
    function setCookie(name, value) {
        document.cookie = `${name}=${value};path=/;max-age=${60 * 60 * 24 * 365};samesite=lax`;
    }

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
