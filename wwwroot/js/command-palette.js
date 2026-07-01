// DayPilot — command palette (Ctrl/Cmd+K) + global keyboard shortcuts.
(function () {
    const tokenEl = document.querySelector('input[name="__RequestVerificationToken"]');
    if (!tokenEl) return;                       // signed-out
    const token = () => tokenEl.value;

    const NAV = [
        { icon: 'bi-check2-square', label: 'Today', url: '/Tasks', keys: 'today tasks list' },
        { icon: 'bi-kanban', label: 'Board (Kanban)', url: '/Tasks/Board', keys: 'board kanban columns' },
        { icon: 'bi-grid-3x3-gap', label: 'Eisenhower Matrix', url: '/Tasks/Matrix', keys: 'matrix eisenhower priority urgent' },
        { icon: 'bi-plus-lg', label: 'New task (form)', url: '/Tasks/Create', keys: 'new create add task' },
        { icon: 'bi-graph-up-arrow', label: 'Dashboard', url: '/Dashboard', keys: 'dashboard stats analytics' },
        { icon: 'bi-trophy', label: 'Habits', url: '/Habits', keys: 'habits streak' },
        { icon: 'bi-stars', label: 'AI Assistant', url: '/Ai', keys: 'ai assistant coach plan' },
        { icon: 'bi-calendar3', label: 'Calendar', url: '/Calendar', keys: 'calendar month' },
        { icon: 'bi-tags', label: 'Categories', url: '/Categories', keys: 'categories' },
        { icon: 'bi-people', label: 'Workspaces', url: '/Workspaces', keys: 'workspace team' },
        { icon: 'bi-clock-history', label: 'History', url: '/History', keys: 'history log' },
        { icon: 'bi-gear', label: 'Settings', url: '/Settings', keys: 'settings profile preferences' }
    ];

    let overlay, input, list, items = [], sel = 0;

    function build() {
        overlay = document.createElement('div');
        overlay.className = 'dp-cmdk';
        overlay.innerHTML =
            '<div class="dp-cmdk-panel" role="dialog" aria-label="Command palette">' +
            '  <div class="dp-cmdk-search"><i class="bi bi-search"></i>' +
            '    <input type="text" placeholder="Type a command, add a task, or search…" aria-label="Command" />' +
            '    <kbd>Esc</kbd>' +
            '  </div>' +
            '  <ul class="dp-cmdk-list"></ul>' +
            '  <div class="dp-cmdk-foot"><span><kbd>↑</kbd><kbd>↓</kbd> navigate</span><span><kbd>↵</kbd> select</span></div>' +
            '</div>';
        document.body.appendChild(overlay);
        input = overlay.querySelector('input');
        list = overlay.querySelector('.dp-cmdk-list');

        overlay.addEventListener('click', e => { if (e.target === overlay) close(); });
        input.addEventListener('input', render);
        input.addEventListener('keydown', onKey);
    }

    function open() {
        if (!overlay) build();
        overlay.classList.add('show');
        input.value = '';
        render();
        setTimeout(() => input.focus(), 20);
    }
    function close() { if (overlay) overlay.classList.remove('show'); }
    function isOpen() { return overlay && overlay.classList.contains('show'); }

    function render() {
        const q = input.value.trim();
        const ql = q.toLowerCase();
        items = [];

        NAV.filter(n => !ql || (n.label + ' ' + n.keys).toLowerCase().includes(ql))
            .forEach(n => items.push({ type: 'nav', icon: n.icon, label: n.label, sub: n.url, run: () => location.assign(n.url) }));

        if (q) {
            items.unshift({ type: 'add', icon: 'bi-stars', label: 'Add task: “' + q + '”', sub: 'Natural language quick-add', run: () => quickAdd(q) });
            items.push({ type: 'search', icon: 'bi-search', label: 'Search “' + q + '”', sub: 'Search everything', run: () => location.assign('/Search?q=' + encodeURIComponent(q)) });
        }
        if (window.dpFocusTimer) {
            items.push({ type: 'focus', icon: 'bi-hourglass-split', label: 'Start focus timer', sub: '25-minute Pomodoro', run: () => { close(); window.dpFocusTimer(); } });
        }

        sel = 0;
        paint();
    }

    function paint() {
        list.innerHTML = items.map((it, i) =>
            '<li class="dp-cmdk-item' + (i === sel ? ' active' : '') + '" data-i="' + i + '">' +
            '<i class="bi ' + it.icon + '"></i><span class="dp-cmdk-label">' + escapeHtml(it.label) + '</span>' +
            '<span class="dp-cmdk-sub">' + escapeHtml(it.sub || '') + '</span></li>'
        ).join('');
        [...list.children].forEach(li => {
            li.addEventListener('mousemove', () => { sel = +li.dataset.i; highlight(); });
            li.addEventListener('click', () => items[+li.dataset.i].run());
        });
    }

    function highlight() {
        [...list.children].forEach((li, i) => li.classList.toggle('active', i === sel));
    }

    function onKey(e) {
        if (e.key === 'Escape') { close(); }
        else if (e.key === 'ArrowDown') { e.preventDefault(); sel = Math.min(sel + 1, items.length - 1); highlight(); ensureVisible(); }
        else if (e.key === 'ArrowUp') { e.preventDefault(); sel = Math.max(sel - 1, 0); highlight(); ensureVisible(); }
        else if (e.key === 'Enter') { e.preventDefault(); if (items[sel]) items[sel].run(); }
    }

    function ensureVisible() {
        const el = list.children[sel];
        if (el) el.scrollIntoView({ block: 'nearest' });
    }

    function quickAdd(text) {
        const f = document.createElement('form');
        f.method = 'post';
        f.action = '/Ai/QuickAdd';
        f.innerHTML = '<input type="hidden" name="__RequestVerificationToken" value="' + token() + '">' +
            '<input type="hidden" name="text">';
        f.querySelector('input[name="text"]').value = text;
        document.body.appendChild(f);
        f.submit();
    }

    function escapeHtml(s) { const d = document.createElement('div'); d.textContent = s; return d.innerHTML; }

    // ---- global shortcuts ----
    document.addEventListener('keydown', function (e) {
        const inField = /^(INPUT|TEXTAREA|SELECT)$/.test((e.target.tagName || '')) || e.target.isContentEditable;
        if ((e.ctrlKey || e.metaKey) && (e.key === 'k' || e.key === 'K')) { e.preventDefault(); isOpen() ? close() : open(); return; }
        if (inField || e.ctrlKey || e.metaKey || e.altKey) return;
        if (e.key === '/') { e.preventDefault(); open(); }
        else if (e.key === 'n') { location.assign('/Tasks/Create'); }
        else if (e.key === 'b') { location.assign('/Tasks/Board'); }
        else if (e.key === 'm') { location.assign('/Tasks/Matrix'); }
        else if (e.key === 't') { location.assign('/Tasks'); }
        else if (e.key === 'd') { location.assign('/Dashboard'); }
    });

    // expose for a nav button
    window.dpOpenPalette = open;
})();
