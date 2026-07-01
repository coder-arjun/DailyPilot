// DayPilot — Pomodoro focus timer with actual-time tracking.
(function () {
    const tokenEl = document.querySelector('input[name="__RequestVerificationToken"]');
    if (!tokenEl) return;                       // signed-out
    const token = () => tokenEl.value;

    let modal, display, label, startBtn, resetBtn, closeBtn, ring;
    let taskId = null, taskTitle = '';
    let totalSec = 25 * 60, remaining = totalSec, timer = null, running = false, elapsedSec = 0;

    function build() {
        modal = document.createElement('div');
        modal.className = 'dp-pomo';
        modal.innerHTML =
            '<div class="dp-pomo-panel" role="dialog" aria-label="Focus timer">' +
            '  <button class="dp-pomo-x" aria-label="Close">&times;</button>' +
            '  <div class="dp-pomo-title"><i class="bi bi-hourglass-split"></i> Focus</div>' +
            '  <div class="dp-pomo-task"></div>' +
            '  <div class="dp-pomo-clock"><svg viewBox="0 0 120 120"><circle class="track" cx="60" cy="60" r="54"/>' +
            '    <circle class="prog" cx="60" cy="60" r="54"/></svg><div class="dp-pomo-time">25:00</div></div>' +
            '  <div class="dp-pomo-presets">' +
            '    <button data-min="15">15m</button><button data-min="25" class="active">25m</button>' +
            '    <button data-min="45">45m</button><button data-min="60">60m</button>' +
            '  </div>' +
            '  <div class="dp-pomo-actions">' +
            '    <button class="btn btn-primary dp-pomo-start"><i class="bi bi-play-fill"></i> Start</button>' +
            '    <button class="btn btn-outline-secondary dp-pomo-reset"><i class="bi bi-arrow-counterclockwise"></i></button>' +
            '  </div>' +
            '  <div class="dp-pomo-hint">Time is logged to the task when you finish or close.</div>' +
            '</div>';
        document.body.appendChild(modal);
        display = modal.querySelector('.dp-pomo-time');
        label = modal.querySelector('.dp-pomo-task');
        startBtn = modal.querySelector('.dp-pomo-start');
        resetBtn = modal.querySelector('.dp-pomo-reset');
        closeBtn = modal.querySelector('.dp-pomo-x');
        ring = modal.querySelector('.prog');

        const R = 54, C = 2 * Math.PI * R;
        ring.style.strokeDasharray = C;
        ring.style.strokeDashoffset = 0;

        startBtn.addEventListener('click', toggle);
        resetBtn.addEventListener('click', () => { stop(false); setDuration(totalSec / 60); });
        closeBtn.addEventListener('click', hide);
        modal.addEventListener('click', e => { if (e.target === modal) hide(); });
        modal.querySelectorAll('.dp-pomo-presets button').forEach(b =>
            b.addEventListener('click', () => {
                modal.querySelectorAll('.dp-pomo-presets button').forEach(x => x.classList.remove('active'));
                b.classList.add('active');
                setDuration(+b.dataset.min);
            }));
    }

    function setDuration(min) { stop(false); totalSec = min * 60; remaining = totalSec; paint(); }

    function paint() {
        const m = Math.floor(remaining / 60), s = remaining % 60;
        display.textContent = (m < 10 ? '0' : '') + m + ':' + (s < 10 ? '0' : '') + s;
        const R = 54, C = 2 * Math.PI * R;
        ring.style.strokeDashoffset = C * (1 - remaining / totalSec);
    }

    function tick() {
        remaining--;
        elapsedSec++;
        paint();
        if (remaining <= 0) {
            finish(true);
        }
    }

    function toggle() {
        if (running) { pause(); } else { start(); }
    }
    function start() {
        running = true;
        startBtn.innerHTML = '<i class="bi bi-pause-fill"></i> Pause';
        timer = setInterval(tick, 1000);
    }
    function pause() {
        running = false;
        startBtn.innerHTML = '<i class="bi bi-play-fill"></i> Resume';
        clearInterval(timer); timer = null;
    }
    function stop(logIt) {
        running = false;
        clearInterval(timer); timer = null;
        startBtn.innerHTML = '<i class="bi bi-play-fill"></i> Start';
        if (logIt) logElapsed();
        elapsedSec = 0;
    }

    function finish(completed) {
        stop(false);
        logElapsed();
        elapsedSec = 0;
        remaining = totalSec; paint();
        if (completed) {
            try { new Audio('data:audio/wav;base64,UklGRl9vT19XQVZFZm10IBAAAAABAAEAQB8AAEAfAAABAAgAZGF0YQAAAAA=').play(); } catch (e) { }
            if ('Notification' in window && Notification.permission === 'granted') {
                navigator.serviceWorker?.ready.then(r => r.showNotification('✅ Focus session complete', {
                    body: taskTitle ? 'Great work on “' + taskTitle + '”.' : 'Great work!', icon: '/icons/icon-192.png', badge: '/icons/badge.png'
                })).catch(() => { });
            }
        }
    }

    function logElapsed() {
        const mins = Math.round(elapsedSec / 60);
        if (!taskId || mins <= 0) return;
        const body = new URLSearchParams();
        body.set('id', taskId); body.set('minutes', mins);
        fetch('/Tasks/LogTime', {
            method: 'POST',
            headers: { 'RequestVerificationToken': token(), 'X-Requested-With': 'XMLHttpRequest', 'Content-Type': 'application/x-www-form-urlencoded' },
            body: body.toString()
        }).then(r => r.ok ? r.json() : null).then(d => {
            if (d && d.actualMinutes != null) {
                const chip = document.querySelector('[data-actual-for="' + taskId + '"]');
                if (chip) { chip.textContent = d.actualMinutes + 'm actual'; chip.classList.remove('d-none'); }
            }
        }).catch(() => { });
    }

    function show(id, title) {
        if (!modal) build();
        taskId = id || null;
        taskTitle = title || '';
        label.textContent = taskTitle;
        label.style.display = taskTitle ? 'block' : 'none';
        stop(false); remaining = totalSec; paint();
        modal.classList.add('show');
    }
    function hide() {
        if (running || elapsedSec > 0) stop(true);
        if (modal) modal.classList.remove('show');
    }

    // Launch from any [data-focus-task] button.
    document.addEventListener('click', function (e) {
        const btn = e.target.closest('[data-focus-task]');
        if (!btn) return;
        e.preventDefault();
        show(btn.getAttribute('data-focus-task'), btn.getAttribute('data-focus-title') || '');
    });

    // Expose a task-less timer for the command palette.
    window.dpFocusTimer = () => show(null, '');
})();
