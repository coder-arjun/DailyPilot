// Route Planner — geolocate A, resolve pasted Google Maps links via the server,
// rank stops by straight-line distance, hand the ordered route to Google Maps.
(() => {
    'use strict';

    const cfg = window.dpRoutePlanner;
    if (!cfg) return;

    const tokenEl = document.querySelector('input[name="__RequestVerificationToken"]');
    const token = () => tokenEl ? tokenEl.value : '';
    const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    const MAX_STOPS = 5;
    const MIN_STOPS = 2;

    const rail = document.getElementById('rpRail');
    const template = document.getElementById('rpStopTemplate');
    const addBtn = document.getElementById('rpAddBtn');
    const compareBtn = document.getElementById('rpCompareBtn');
    const hint = document.getElementById('rpHint');
    const results = document.getElementById('rpResults');
    const emptyEl = document.getElementById('rpEmpty');
    const staleEl = document.getElementById('rpStale');
    const rankedEl = document.getElementById('rpRanked');
    const goEl = document.getElementById('rpGo');
    const noteEl = document.getElementById('rpNote');

    const you = {
        point: null,
        idle: document.getElementById('rpYouIdle'),
        manual: document.getElementById('rpYouManual'),
        input: document.getElementById('rpYouInput'),
        chip: document.getElementById('rpYouChip'),
        error: document.getElementById('rpYouError'),
    };

    let stops = [];        // [{ el, point: {lat,lng,label}|null }]
    let hasResults = false;
    let nextStopId = 0;

    // ---------- Server resolve ----------

    async function resolveText(text) {
        const res = await fetch(cfg.resolveUrl, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': token(),
                'X-Requested-With': 'XMLHttpRequest',
            },
            body: JSON.stringify({ text }),
        });
        if (!res.ok) throw new Error('HTTP ' + res.status);
        return res.json();
    }

    // ---------- Geometry ----------

    function haversineKm(a, b) {
        const rad = d => d * Math.PI / 180;
        const dLat = rad(b.lat - a.lat), dLng = rad(b.lng - a.lng);
        const s = Math.sin(dLat / 2) ** 2 +
            Math.cos(rad(a.lat)) * Math.cos(rad(b.lat)) * Math.sin(dLng / 2) ** 2;
        return 2 * 6371.0088 * Math.asin(Math.min(1, Math.sqrt(s)));
    }

    function formatKm(km) {
        if (km < 1) return { value: Math.round(km * 1000), unit: 'm' };
        return { value: km < 10 ? Math.round(km * 10) / 10 : Math.round(km), unit: 'km' };
    }

    const coordText = p => `${p.lat.toFixed(5)}, ${p.lng.toFixed(5)}`;

    // ---------- "You" (point A) ----------

    const locateBtn = document.getElementById('rpLocateBtn');
    const resetLocateBtn = () => {
        locateBtn.disabled = false;
        locateBtn.innerHTML = '<i class="bi bi-crosshair"></i> Use my location';
    };

    locateBtn.addEventListener('click', () => {
        hide(you.error);
        if (!navigator.geolocation) {
            showYouError('Location is not available in this browser — enter your start manually.');
            return;
        }
        const btn = locateBtn;
        btn.disabled = true;
        btn.innerHTML = '<span class="rp-spinner rp-spinner-btn"></span> Locating…';
        navigator.geolocation.getCurrentPosition(
            pos => {
                you.point = { lat: pos.coords.latitude, lng: pos.coords.longitude, label: 'Your location' };
                setYouResolved();
            },
            err => {
                resetLocateBtn();
                showYouError(err.code === 1
                    ? 'Location permission was denied — allow it in your browser, or enter your start manually.'
                    : 'Couldn’t get a fix on your location — try again, or enter your start manually.');
                show(you.manual);
            },
            { enableHighAccuracy: true, timeout: 10000, maximumAge: 60000 });
    });

    document.getElementById('rpYouManualBtn').addEventListener('click', () => {
        show(you.manual);
        you.input.focus();
    });

    const youResolver = wireResolveInput(you.input, you.manual.querySelector('.rp-spinner'), you.error, point => {
        you.point = { ...point, label: point.label || 'Your start' };
        setYouResolved();
    });

    you.chip.addEventListener('click', () => {
        you.point = null;
        youResolver.reset();
        resetLocateBtn();
        hide(you.chip);
        show(you.idle);
        show(you.manual);
        you.input.focus();
        onStateChanged();
    });

    function setYouResolved() {
        hide(you.idle); hide(you.manual); hide(you.error);
        you.chip.innerHTML = `<i class="bi bi-check-circle-fill"></i> <strong>${escapeHtml(you.point.label)}</strong> <span>${coordText(you.point)}</span>`;
        show(you.chip, 'rp-chip-pop');
        onStateChanged();
    }

    function showYouError(msg) {
        you.error.textContent = msg;
        show(you.error);
    }

    // ---------- Stops ----------

    function addStop() {
        if (stops.length >= MAX_STOPS) return;
        const el = template.content.firstElementChild.cloneNode(true);
        el.dataset.stopId = String(nextStopId++);
        const stop = { el, point: null };
        stops.push(stop);
        rail.appendChild(el);

        const input = el.querySelector('.rp-input');
        const spinner = el.querySelector('.rp-spinner');
        const error = el.querySelector('.rp-error');
        const chip = el.querySelector('.rp-chip');

        const resolver = wireResolveInput(input, spinner, error, point => {
            stop.point = point;
            hide(el.querySelector('.rp-resolve')); hide(error);
            chip.innerHTML = `<i class="bi bi-check-circle-fill"></i> <strong>${escapeHtml(point.label || stopName(stop))}</strong> <span>${coordText(point)}</span>`;
            show(chip, 'rp-chip-pop');
            onStateChanged();
        });

        chip.addEventListener('click', () => {
            stop.point = null;
            resolver.reset();
            hide(chip);
            show(el.querySelector('.rp-resolve'));
            input.focus();
            onStateChanged();
        });

        el.querySelector('.rp-remove').addEventListener('click', () => {
            if (stops.length <= MIN_STOPS) return;
            stops = stops.filter(s => s !== stop);
            el.remove();
            renumber();
            onStateChanged();
        });

        renumber();
        onStateChanged();
        return stop;
    }

    const stopName = stop => 'Stop ' + (stops.indexOf(stop) + 1);

    function renumber() {
        stops.forEach((s, i) => {
            s.el.querySelector('.rp-marker-num').textContent = i + 1;
            s.el.querySelector('.rp-stop-name').textContent = 'Stop ' + (i + 1);
            s.el.querySelector('.rp-remove').classList.toggle('d-none', stops.length <= MIN_STOPS);
        });
        addBtn.classList.toggle('d-none', stops.length >= MAX_STOPS);
    }

    function wireResolveInput(input, spinner, error, onResolved) {
        let lastResolved = '';
        const run = async () => {
            const text = input.value.trim();
            if (!text || text === lastResolved) return;
            hide(error);
            show(spinner);
            try {
                const r = await resolveText(text);
                if (r.ok) {
                    lastResolved = text;
                    onResolved({ lat: r.lat, lng: r.lng, label: r.label });
                } else {
                    error.textContent = r.error;
                    show(error);
                }
            } catch {
                error.textContent = 'Couldn’t reach the server — check your connection and try again.';
                show(error);
            } finally {
                hide(spinner);
            }
        };
        input.addEventListener('paste', () => setTimeout(run, 50));
        input.addEventListener('blur', run);
        input.addEventListener('keydown', e => { if (e.key === 'Enter') { e.preventDefault(); run(); } });
        return { reset: () => { lastResolved = ''; } };
    }

    // ---------- Readiness / staleness ----------

    function onStateChanged() {
        const resolved = stops.filter(s => s.point);
        const ready = !!you.point && resolved.length >= MIN_STOPS;
        compareBtn.disabled = !ready;
        hint.textContent = ready
            ? `Comparing ${resolved.length} stops from ${you.point.label.toLowerCase() === 'your location' ? 'where you are' : you.point.label}.`
            : !you.point
                ? 'Share your location and add at least two stops.'
                : `Add ${MIN_STOPS - resolved.length === 1 ? 'one more stop' : 'at least two stops'} to compare.`;
        if (hasResults) {
            results.classList.add('is-stale');
            show(staleEl);
        }
    }

    // ---------- Compare & results ----------

    compareBtn.addEventListener('click', () => {
        const ranked = stops
            .filter(s => s.point)
            .map(s => ({ label: s.point.label || stopName(s), point: s.point, km: haversineKm(you.point, s.point) }))
            .sort((a, b) => a.km - b.km);

        rankedEl.innerHTML = '';
        ranked.forEach((r, i) => {
            const li = document.createElement('li');
            li.className = 'rp-rank';
            li.style.setProperty('--i', i);
            li.innerHTML = `
                <span class="rp-rank-pos" aria-hidden="true">${i + 1}</span>
                <div class="rp-rank-info">
                    <div class="rp-rank-name">${escapeHtml(r.label)}</div>
                    <div class="rp-rank-sub">${coordText(r.point)}</div>
                </div>
                ${i === 0 ? '<span class="rp-nearest">Nearest</span>' : ''}
                <div class="rp-rank-dist"><span class="rp-rank-km"></span><span class="rp-rank-unit"></span></div>`;
            rankedEl.appendChild(li);
            const f = formatKm(r.km);
            li.querySelector('.rp-rank-unit').textContent = f.unit;
            animateNumber(li.querySelector('.rp-rank-km'), f.value);
        });

        const origin = `${you.point.lat},${you.point.lng}`;
        const pts = ranked.map(r => `${r.point.lat},${r.point.lng}`);
        const dest = pts[pts.length - 1];
        const waypoints = pts.slice(0, -1);
        let url = `https://www.google.com/maps/dir/?api=1&origin=${encodeURIComponent(origin)}&destination=${encodeURIComponent(dest)}&travelmode=driving`;
        if (waypoints.length) url += `&waypoints=${encodeURIComponent(waypoints.join('|'))}`;
        goEl.href = url;

        hide(emptyEl); hide(staleEl);
        results.classList.remove('is-stale');
        show(rankedEl); show(goEl); show(noteEl);
        hasResults = true;
    });

    function animateNumber(el, target) {
        const decimals = Number.isInteger(target) ? 0 : 1;
        if (reducedMotion) { el.textContent = target.toFixed(decimals); return; }
        const start = performance.now(), duration = 700;
        const frame = now => {
            const t = Math.min(1, (now - start) / duration);
            const eased = 1 - Math.pow(1 - t, 3);
            el.textContent = (target * eased).toFixed(decimals);
            if (t < 1) requestAnimationFrame(frame);
        };
        requestAnimationFrame(frame);
    }

    // ---------- Helpers ----------

    function show(el, popClass) {
        el.classList.remove('d-none');
        if (popClass && !reducedMotion) {
            el.classList.remove(popClass);
            void el.offsetWidth;           // restart the animation
            el.classList.add(popClass);
        }
    }
    function hide(el) { el.classList.add('d-none'); }

    function escapeHtml(s) {
        const div = document.createElement('div');
        div.textContent = s ?? '';
        return div.innerHTML;
    }

    // ---------- Init ----------

    addStop();
    addStop();
    addBtn.addEventListener('click', () => {
        const stop = addStop();
        if (stop) stop.el.querySelector('.rp-input').focus();
    });
})();
