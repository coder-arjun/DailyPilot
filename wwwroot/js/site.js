// DayPilot client-side enhancements.

(function () {
    function setCookie(name, value) {
        document.cookie = `${name}=${value};path=/;max-age=${60 * 60 * 24 * 365};samesite=lax`;
    }

    window.addEventListener('DOMContentLoaded', function () {
        // ---- Theme picker (5 premium palettes) ----
        const root = document.documentElement;
        document.querySelectorAll('[data-theme-set]').forEach(function (btn) {
            btn.addEventListener('click', function () {
                const key = btn.getAttribute('data-theme-set');
                const bs = btn.getAttribute('data-theme-bs') || 'dark';
                root.setAttribute('data-dp-theme', key);
                root.setAttribute('data-bs-theme', bs);
                setCookie('dp_palette', key);

                // Persist to the user's profile (cross-device) when signed in.
                const token = document.querySelector('input[name="__RequestVerificationToken"]');
                if (token) {
                    fetch('/Settings/SetTheme?theme=' + encodeURIComponent(key), {
                        method: 'POST',
                        headers: { 'RequestVerificationToken': token.value }
                    }).catch(function () { });
                }

                document.querySelectorAll('.theme-option').forEach(function (b) {
                    const on = b === btn;
                    b.classList.toggle('active', on);
                    const check = b.querySelector('.bi-check2');
                    if (check) check.classList.toggle('invisible', !on);
                });
            });
        });

        // ---- Server-side TempData toasts ----
        document.querySelectorAll('.dp-toast').forEach(function (el) {
            try { new bootstrap.Toast(el, { delay: 4000 }).show(); } catch (e) { }
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
