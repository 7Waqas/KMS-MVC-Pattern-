// Live security alerts: polls KMS for new incidents and shows a toast with a sound.
(function () {
    var POLL_MS = 15000;
    var STORE_KEY = 'kms.kc.sinceId';
    var sinceId = 0;
    try { sinceId = parseInt(sessionStorage.getItem(STORE_KEY) || '0', 10) || 0; } catch (e) { sinceId = 0; }
    function remember(id) { try { sessionStorage.setItem(STORE_KEY, String(id)); } catch (e) { /* ignore */ } }
    var box = document.createElement('div');
    box.className = 'kc-toasts';
    document.body.appendChild(box);

    var audioCtx = null;
    function unlockAudio() {
        try {
            if (!audioCtx) audioCtx = new (window.AudioContext || window.webkitAudioContext)();
            if (audioCtx.state === 'suspended') audioCtx.resume();
        } catch (e) { /* sound not available */ }
    }
    // Browsers only allow sound after the user has clicked once on the page.
    document.addEventListener('click', unlockAudio, { once: true });

    function beep() {
        if (!audioCtx || audioCtx.state !== 'running') return;
        try {
            [0, 0.35].forEach(function (offset) {
                var o = audioCtx.createOscillator(), g = audioCtx.createGain();
                o.type = 'square'; o.frequency.value = 880;
                g.gain.setValueAtTime(0.15, audioCtx.currentTime + offset);
                g.gain.exponentialRampToValueAtTime(0.001, audioCtx.currentTime + offset + 0.25);
                o.connect(g); g.connect(audioCtx.destination);
                o.start(audioCtx.currentTime + offset); o.stop(audioCtx.currentTime + offset + 0.26);
            });
        } catch (e) { /* ignore */ }
    }

    function esc(s) {
        return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
        });
    }

    var labels = {
        NOT_AUTHORIZED: 'NOT AUTHORIZED', NO_FACE: 'KEY WITHOUT FACE',
        TAKEN_WITHOUT_SCAN: 'TAKEN WITHOUT SCAN', RETURN_NOT_SCANNED: 'RETURN NOT SCANNED'
    };

    function show(item) {
        var t = document.createElement('div');
        t.className = 'kc-toast';
        var photo = item.personEnroll
            ? '<img class="kc-toast__photo" alt="" src="/Security/Photo/' + encodeURIComponent(item.personEnroll) + '" onerror="this.remove()">'
            : '';
        t.innerHTML = '<button class="kc-toast__close" aria-label="Close">&times;</button>' +
            '<div class="kc-toast__title"><i class="bi bi-shield-exclamation"></i> ' + esc(labels[item.type] || item.type) +
            ' — ' + esc(item.key) + '</div>' +
            '<div class="kc-toast__body">' + photo +
            '<div class="small">' + esc(item.summary) + '</div></div>';
        t.querySelector('.kc-toast__close').addEventListener('click', function (e) { e.stopPropagation(); t.remove(); });
        t.addEventListener('click', function () { location.href = '/Security/Incident/' + item.id; });
        box.prepend(t);
        setTimeout(function () { t.remove(); }, 120000);
    }

    function poll() {
        fetch('/Security/Alerts?sinceId=' + sinceId, { credentials: 'same-origin', headers: { 'Accept': 'application/json' } })
            .then(function (r) { return r.ok ? r.json() : null; })
            .then(function (data) {
                if (!data) return;
                if (sinceId > 0 && data.items && data.items.length) {
                    data.items.forEach(show);
                    beep();
                }
                sinceId = Math.max(sinceId, data.maxId || 0);
                remember(sinceId);
            })
            .catch(function () { /* offline: try again next time */ })
            .finally(function () { setTimeout(poll, POLL_MS); });
    }
    poll();
})();
