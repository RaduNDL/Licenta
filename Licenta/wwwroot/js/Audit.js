(function () {
    function $(id) {
        return document.getElementById(id);
    }

    function toRomaniaLocalParts(utcIso) {
        var d = new Date(utcIso);
        var dateStr = new Intl.DateTimeFormat('en-US', {
            timeZone: 'Europe/Bucharest',
            month: 'short',
            day: '2-digit',
            year: 'numeric'
        }).format(d);

        var timeStr = new Intl.DateTimeFormat('en-GB', {
            timeZone: 'Europe/Bucharest',
            hour: '2-digit',
            minute: '2-digit',
            second: '2-digit',
            hour12: false
        }).format(d);

        return { dateStr: dateStr, timeStr: timeStr };
    }

    function safeInitials(name) {
        if (!name || !name.trim()) return "?";
        return name.trim().substring(0, 1).toUpperCase();
    }

    function ensureContentVisible() {
        var empty = $("emptyState");
        var content = $("auditContent");
        if (empty) empty.style.display = "none";
        if (content) content.style.display = "";
    }

    function bumpCounts() {
        var countEl = $("eventsCount");
        var recEl = $("recordsCount");

        var current = parseInt((countEl && countEl.innerText) ? countEl.innerText : "0", 10);
        if (isNaN(current)) current = 0;

        var next = current + 1;

        if (countEl) countEl.innerText = String(next);
        if (recEl) recEl.innerText = String(next);
    }

    function prependRow(payload) {
        var tbody = $("auditTbody");
        if (!tbody) return;

        var parts = toRomaniaLocalParts(payload.timestampUtc);
        var initials = safeInitials(payload.userName);

        var tr = document.createElement("tr");

        tr.innerHTML =
            '<td class="ps-4">' +
            '  <div class="d-flex align-items-center">' +
            '    <div class="bg-light rounded p-2 me-3 text-primary"><i class="bi bi-clock"></i></div>' +
            '    <div>' +
            '      <div class="fw-bold text-dark">' + parts.dateStr + '</div>' +
            '      <div class="small text-muted font-monospace">' + parts.timeStr + '</div>' +
            '    </div>' +
            '  </div>' +
            '</td>' +
            '<td class="ps-4">' +
            '  <div class="d-flex align-items-center">' +
            '    <div class="avatar-circle" style="width: 32px; height: 32px; font-size: 0.8rem;">' + initials + '</div>' +
            '    <span class="fw-medium text-dark ms-2">' + (payload.userName || "") + '</span>' +
            '  </div>' +
            '</td>' +
            '<td class="ps-4"><span class="text-muted font-monospace">' + (payload.remoteIp || "") + '</span></td>' +
            '<td class="text-end pe-4"><span class="badge bg-light text-secondary border">' + (payload.scheme || "") + '</span></td>';

        tbody.prepend(tr);
        filterTable();
    }

    function filterTable() {
        var input = $("searchInput");
        var table = $("auditTable");
        if (!input || !table) return;

        var q = input.value.trim().toLowerCase();
        var rows = Array.from(table.querySelectorAll("tbody tr"));

        if (!q) {
            rows.forEach(function (r) { r.style.display = ""; });
            return;
        }

        rows.forEach(function (r) {
            var timeText = (r.cells[0] && r.cells[0].innerText ? r.cells[0].innerText : "").toLowerCase();
            var userText = (r.cells[1] && r.cells[1].innerText ? r.cells[1].innerText : "").toLowerCase();
            var ipText = (r.cells[2] && r.cells[2].innerText ? r.cells[2].innerText : "").toLowerCase();
            var schemeText = (r.cells[3] && r.cells[3].innerText ? r.cells[3].innerText : "").toLowerCase();

            r.style.display = (timeText.includes(q) || userText.includes(q) || ipText.includes(q) || schemeText.includes(q)) ? "" : "none";
        });
    }

    function wireSearch() {
        var input = $("searchInput");
        if (!input) return;

        var t = null;
        input.addEventListener("input", function () {
            clearTimeout(t);
            t = setTimeout(filterTable, 150);
        });
    }

    function startHub() {
        if (!window.signalR) return;

        var connection = new signalR.HubConnectionBuilder()
            .withUrl("/notificationHub")
            .withAutomaticReconnect()
            .build();

        connection.on("audit:signin", function (payload) {
            ensureContentVisible();
            prependRow(payload);
            bumpCounts();
        });

        connection.start()
            .then(function () {
                return connection.invoke("JoinAuditAdmins");
            })
            .catch(function () { });
    }

    function init() {
        wireSearch();
        startHub();
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", init);
    } else {
        init();
    }
})();
