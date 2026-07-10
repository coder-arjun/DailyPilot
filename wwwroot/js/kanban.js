// DayPilot — Kanban drag-and-drop. Moves a task between status columns and
// persists the change via /Tasks/SetStatus.
(function () {
    const board = document.getElementById('dpKanban');
    if (!board) return;
    const tokenEl = document.querySelector('input[name="__RequestVerificationToken"]');
    const token = () => (tokenEl ? tokenEl.value : '');

    let dragged = null;

    board.querySelectorAll('.dp-kan-card').forEach(card => {
        card.addEventListener('dragstart', e => {
            dragged = card;
            card.classList.add('dragging');
            e.dataTransfer.effectAllowed = 'move';
            e.dataTransfer.setData('text/plain', card.dataset.id);
        });
        card.addEventListener('dragend', () => { card.classList.remove('dragging'); dragged = null; });
    });

    board.querySelectorAll('.dp-kan-col').forEach(col => {
        const drop = col.querySelector('.dp-kan-drop');
        col.addEventListener('dragover', e => { e.preventDefault(); col.classList.add('over'); });
        col.addEventListener('dragleave', () => col.classList.remove('over'));
        col.addEventListener('drop', e => {
            e.preventDefault();
            col.classList.remove('over');
            if (!dragged) return;
            const status = col.dataset.status;
            const id = dragged.dataset.id;
            if (dragged.parentElement === drop) return;
            drop.appendChild(dragged);
            recount();
            persist(id, status, dragged);
        });
    });

    function persist(id, status, card) {
        const body = new URLSearchParams();
        body.set('id', id); body.set('status', status);
        fetch('/Tasks/SetStatus', {
            method: 'POST',
            headers: { 'RequestVerificationToken': token(), 'X-Requested-With': 'XMLHttpRequest', 'Content-Type': 'application/x-www-form-urlencoded' },
            body: body.toString()
        }).then(r => {
            if (!r.ok) throw new Error('failed');
            card.classList.toggle('done', status === 'Completed');
            if (status === 'Completed' && window.dpPlayComplete) window.dpPlayComplete();
        }).catch(() => { location.reload(); });   // reconcile on failure
    }

    function recount() {
        board.querySelectorAll('.dp-kan-col').forEach(col => {
            const n = col.querySelectorAll('.dp-kan-card').length;
            const badge = col.querySelector('.dp-kan-count');
            if (badge) badge.textContent = n;
            const empty = col.querySelector('.dp-kan-empty');
            if (empty) empty.classList.toggle('d-none', n > 0);
        });
    }
})();
